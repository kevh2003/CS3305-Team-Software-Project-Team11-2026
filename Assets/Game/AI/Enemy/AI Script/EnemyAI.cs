using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

[RequireComponent(typeof(AudioSource))]
public class EnemyAI : NetworkBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2f;
    private bool isPaused;

    [Header("Detection Settings")]
    public float detectionRange = 24f;
    public float updateRate = 0.5f;
    public float viewAngle = 175f;

    [Header("Search Settings")]
    private float lastSeenTime = -999f;
    [SerializeField] private float loseSightGrace = 1.0f; // seconds before switching to searching
    public float alertedDetectionMultiplier = 1.5f;
    public float alertedViewAngle = 240f;
    public float searchDuration = 10f;
    private Vector3 lastKnownPosition;
    private bool isSearching;
    private float searchEndTime;
    private float originalDetectionRange;
    private float originalViewAngle;
    private bool isGlancing;
    private float glanceTime = 1f;
    private float glanceTimer = 0f;
    private int glanceDirection = 1;

    [Header("Lure Settings")]
    public float investigateDuration = 4f;
    private bool isLured;
    private Vector3 lureDestination;
    private Vector3 preLurePosition;
    private float lureEndTime;
    private float lureGlanceTimer = 0f;
    private int lureGlanceDirection = 1;

    [Header("Audio")]
    [Tooltip("Looping clip played while the enemy is patrolling. Quieter and calmer.")]
    public AudioClip PatrolClip;

    [Tooltip("Looping clip played while the enemy is chasing a player.")]
    public AudioClip AlertClip;

    [Tooltip("One-shot played when the enemy first spots a player.")]
    public AudioClip SpottedClip;

    [Tooltip("Volume used when playing the patrol clip.")]
    [Range(0f, 1f)]
    public float PatrolVolume = 0.5f;

    [Tooltip("Volume used when playing the alert clip.")]
    [Range(0f, 1f)]
    public float AlertVolume = 0.85f;

    [Tooltip("Volume used when playing the spotted one-shot clip.")]
    [Range(0f, 1f)]
    public float SpottedVolume = 1f;

    [Tooltip("How far the sounds carry in world units.")]
    public float HeardRadius = 30f;

    [Tooltip("Crossfade amount used when looping chase audio to hide loop seams.")]
    [SerializeField, Range(0f, 0.5f)] private float alertLoopCrossfadeSeconds = 0.08f;
    [SerializeField] private bool smoothAlertLoop = true;

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private LayerMask obstacleMask;
    public Transform currentTarget;
    private NavMeshAgent agent;

    // Single shared AudioSource — clip and volume are swapped depending on state
    private AudioSource enemyAudioSource;
    private AudioSource enemyAudioBlendSource;
    private Coroutine alertLoopRoutine;
    private float _lastSpottedSfxServerTime = -999f;

    private enum AudioMode
    {
        None,
        Patrol,
        Alert
    }

    private AudioMode currentAudioMode = AudioMode.None;

    [Header("Movement Speed")]
    [SerializeField] private float chaseSpeed = 5.5f;

    // Performance : How often SetDestination gets updated while chasing
    [Header("Chase Path Refresh")]
    [SerializeField] private float chaseRepathRate = 0.2f; // 5 times/sec

    // Tracks whether the enemy had a target last update tick (server only)
    private bool wasChasing = false;

    // Performance : Non-alloc buffer for overlap hits (avoid GC alloc every scan)
    private const int MaxPlayersInRange = 16; // you have 6 players; 16 is generous
    private readonly Collider[] _playerHits = new Collider[MaxPlayersInRange];

    // Performance : precomputed cos(FOV/2) for dot-product check
    private float _cosHalfFov;

    // Performance : throttle chase destination updates
    private float _nextRepathTime;

    void Awake()
    {
        agent            = GetComponent<NavMeshAgent>();
        enemyAudioSource = GetComponent<AudioSource>();
        enemyAudioBlendSource = CreateBlendSource(enemyAudioSource);

        agent.stoppingDistance = attackRange * 0.9f;
        agent.updateRotation   = true;
        agent.angularSpeed     = 1080f;
        agent.speed            = chaseSpeed;

        originalDetectionRange = detectionRange;
        originalViewAngle      = viewAngle;

        // Precompute cosine threshold for the current FOV
        _cosHalfFov = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        ConfigureAudioSource();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the host/server runs the AI update loop.
        // Clients have no business running pathfinding or detection.
        if (!IsServer) return;

        // Safety: avoid stacking UpdateTarget invokes if this NetworkObject respawns/match restarts
        CancelInvoke(nameof(UpdateTarget));

        // Performance: stagger scans so all enemies don't spike on the same frame
        float initialDelay = Random.Range(0f, updateRate);
        InvokeRepeating(nameof(UpdateTarget), initialDelay, updateRate);

        PlayClipClientRpc(true);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Server: stop the repeating scan when the object despawns
        if (IsServer)
            CancelInvoke(nameof(UpdateTarget));

        // Stop audio so it can't "stick" across scene/match transitions.
        if (IsServer)
            StopAudioClientRpc();

        currentAudioMode = AudioMode.None;
        StopAlertLoopPlaybackLocal();
        if (enemyAudioSource != null)
            enemyAudioSource.Stop();
    }

    void Update()
    {
        // All movement and AI logic is server-only.
        // On a LAN listen server the host runs this locally — clients skip it entirely.
        if (!IsServer) return;

        if (isPaused) return;

        // Lure takes priority over all other states while active.
        if (isLured && currentTarget == null)
        {
            HandleLureBehaviour();
            return;
        }

        if (currentTarget != null)
        {
            // Chase the player
            // Performance : don't SetDestination every frame
            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + chaseRepathRate;

                Vector3 targetPos = currentTarget.position;

                // If player is off the NavMesh (e.g., on a box), chase the closest NavMesh point instead
                if (NavMesh.SamplePosition(targetPos, out var hit, 10f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                var dest = GetBestReachablePoint(currentTarget.position);
                agent.SetDestination(dest);
            }
        }
        else if (isSearching)
        {
            // If search time expired, calm down
            if (Time.time >= searchEndTime)
            {
                StopSearching();
                return;
            }

            if (!isGlancing)
            {
                // Move toward last known position (clamp to navmesh so it doesn't get stuck)
                Vector3 dest = lastKnownPosition;
                if (NavMesh.SamplePosition(dest, out var hit, 3f, agent.areaMask))
                    dest = hit.position;

                agent.SetDestination(dest);

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                {
                    // Start glancing
                    isGlancing = true;
                    glanceTimer = 0f;
                    glanceDirection = 1;

                    agent.ResetPath();            // stop while glancing
                    agent.updateRotation = false; // manual rotate during glance
                }
            }
            else
            {
                glanceTimer += Time.deltaTime;

                float rotationSpeed = 180f;
                transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

                if (glanceTimer >= glanceTime)
                {
                    // Switch direction every glanceTime seconds
                    glanceDirection *= -1;
                    glanceTimer = 0f;

                    // After a full left-right cycle, stop searching
                    if (glanceDirection == 1)
                    {
                        StopSearching();
                    }
                }
            }
        }
    }

    void UpdateTarget()
    {
        if (isPaused) return;

        // Hard lure lock: do not reacquire or keep chase targets until lure completes.
        if (isLured)
        {
            currentTarget = null;
            wasChasing = false;
            return;
        }

        // If current target dies, stop chasing
        if (currentTarget != null)
        {
            var th = currentTarget.GetComponentInParent<PlayerHealth>();
            if (th == null || th.IsDead.Value)
            {
                ServerClearTargetAndCalmDown();
                return;
            }

            // Check LOS to current target
            Vector3 toTarget = currentTarget.position - transform.position;
            float distSqrToTarget = toTarget.sqrMagnitude;
            float rangeSqrToTarget = detectionRange * detectionRange;

            bool canSeeCurrentTarget = false;

            if (distSqrToTarget <= rangeSqrToTarget)
            {
                Vector3 dir = toTarget.normalized;
                float dot = Vector3.Dot(transform.forward, dir);

                if (dot >= _cosHalfFov)
                {
                    float dist = Mathf.Sqrt(distSqrToTarget);
                    if (!Physics.Raycast(transform.position, dir, dist, obstacleMask))
                        canSeeCurrentTarget = true;
                }
            }

            if (canSeeCurrentTarget)
            {
                // still seeing the target -> stay in chase
                lastSeenTime = Time.time;
                lastKnownPosition = currentTarget.position;
                wasChasing = true;
                return;
            }

            // Lost sight -> remember last known position
            lastKnownPosition = currentTarget.position;

            // Grace period before switching to searching (prevents flicker)
            if (Time.time - lastSeenTime < loseSightGrace)
            {
                // keep chasing for a short moment
                wasChasing = true;
                return;
            }

            // Switch to searching
            currentTarget = null;
            wasChasing = false;

            StartSearching();
            return;
        }

        // If searching, don't try to acquire a new target unless we see one
        // (We DO still scan though, so we can reacquire)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, _playerHits, whatIsPlayer);

        Transform closestPlayer = null;
        float closestDistSqr = Mathf.Infinity;
        float rangeSqr = detectionRange * detectionRange;

        for (int i = 0; i < hitCount; i++)
        {
            Collider playerCollider = _playerHits[i];
            if (playerCollider == null) continue;

            var ph = playerCollider.GetComponentInParent<PlayerHealth>();
            if (ph != null && ph.IsDead.Value) continue;

            Vector3 toPlayer = playerCollider.transform.position - transform.position;
            float distSqr = toPlayer.sqrMagnitude;
            if (distSqr > rangeSqr) continue;

            Vector3 dir = toPlayer.normalized;
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < _cosHalfFov) continue;

            float dist = Mathf.Sqrt(distSqr);
            if (Physics.Raycast(transform.position, dir, dist, obstacleMask)) continue;

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestPlayer = playerCollider.transform;
            }
        }

        if (closestPlayer != null)
        {
            bool justSpotted = (currentTarget == null) && !wasChasing;

            currentTarget = closestPlayer;
            wasChasing = true;

            isSearching = false;
            isGlancing = false;

            if (agent != null)
            {
                agent.updateRotation = true;
                agent.isStopped = false;
            }

            lastSeenTime = Time.time;
            lastKnownPosition = closestPlayer.position;

            // Ensure chase audio starts
            PlayClipClientRpc(false);

            if (justSpotted && Time.time - _lastSpottedSfxServerTime >= 0.2f)
            {
                _lastSpottedSfxServerTime = Time.time;
                PlaySpottedSfxClientRpc();
            }
        }
        else
        {
            wasChasing = false;
        }
    }

    private Vector3 GetBestReachablePoint(Vector3 desired)
    {
        // Snap desired point to NavMesh (handles players on boxes / off-mesh)
        Vector3 snapped = desired;
        if (NavMesh.SamplePosition(desired, out var hit, 2.0f, agent.areaMask))
            snapped = hit.position;

        // If we can compute a path, use the last reachable corner
        var path = new NavMeshPath();
        if (agent.CalculatePath(snapped, path) && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 0)
            return path.corners[path.corners.Length - 1];

        // Fallback: go to snapped anyway
        return snapped;
    }

    // Server-only: clears target & returns to calm state
    public void ServerClearTargetAndCalmDown()
    {
        if (!IsServer) return;

        currentTarget = null;
        wasChasing = false;

        isSearching = false;
        isGlancing = false;

        isLured = false;
        lureEndTime = 0f;

        detectionRange = originalDetectionRange;
        viewAngle = originalViewAngle;
        _cosHalfFov = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        if (agent != null)
        {
            agent.updateRotation = true; // give rotation control back to NavMeshAgent
            agent.isStopped = false;
            agent.ResetPath();
        }

        // Put audio back to patrol everywhere
        PlayClipClientRpc(true);
    }

    [ClientRpc]
    private void PlayClipClientRpc(bool usePatrol)
    {
        if (enemyAudioSource == null) return;

        if (usePatrol)
        {
            if (currentAudioMode == AudioMode.Patrol)
                return;

            currentAudioMode = AudioMode.Patrol;
            StopAlertLoopPlaybackLocal();
            PlayLoopingClip(enemyAudioSource, PatrolClip, PatrolVolume);
            return;
        }

        if (currentAudioMode == AudioMode.Alert)
            return;

        currentAudioMode = AudioMode.Alert;

        if (smoothAlertLoop && CanUseSmoothedAlertLoop())
        {
            StartAlertLoopPlaybackLocal();
            return;
        }

        StopAlertLoopPlaybackLocal();
        PlayLoopingClip(enemyAudioSource, AlertClip, AlertVolume);
    }

    [ClientRpc]
    private void PlaySpottedSfxClientRpc()
    {
        if (SpottedClip == null || enemyAudioSource == null)
            return;

        enemyAudioSource.PlayOneShot(SpottedClip, SpottedVolume);
    }

    [ClientRpc]
    private void StopAudioClientRpc()
    {
        currentAudioMode = AudioMode.None;
        StopAlertLoopPlaybackLocal();
        if (enemyAudioSource != null)
            enemyAudioSource.Stop();
    }

    private void ConfigureAudioSource()
    {
        ConfigureSpatialAudioSource(enemyAudioSource);
        ConfigureSpatialAudioSource(enemyAudioBlendSource);
        if (enemyAudioBlendSource != null)
            enemyAudioBlendSource.loop = false;
    }

    private static AudioSource CreateBlendSource(AudioSource template)
    {
        if (template == null)
            return null;

        var go = new GameObject("EnemyAlertBlendSource");
        go.transform.SetParent(template.transform, false);
        return go.AddComponent<AudioSource>();
    }

    private void ConfigureSpatialAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.maxDistance = HeardRadius;
        source.minDistance = 1f;
        source.playOnAwake = false;
        source.loop = true;
        source.dopplerLevel = 0f;
    }

    private void PlayLoopingClip(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null)
            return;

        if (clip == null)
        {
            Debug.LogWarning($"EnemyAI: Missing loop clip on {gameObject.name}.");
            return;
        }

        bool shouldRestart = !source.isPlaying || source.clip != clip || !source.loop;

        if (shouldRestart)
            source.Stop();

        source.loop = true;
        source.volume = volume;
        source.clip = clip;
        if (shouldRestart)
            source.Play();
    }

    private bool CanUseSmoothedAlertLoop()
    {
        if (AlertClip == null || enemyAudioBlendSource == null)
            return false;

        float fade = Mathf.Clamp(alertLoopCrossfadeSeconds, 0f, 0.5f);
        return fade > 0.005f && AlertClip.length > fade * 2f + 0.02f;
    }

    private void StartAlertLoopPlaybackLocal()
    {
        StopAlertLoopPlaybackLocal();

        if (enemyAudioSource == null || enemyAudioBlendSource == null || AlertClip == null)
            return;

        enemyAudioSource.Stop();
        enemyAudioBlendSource.Stop();

        enemyAudioSource.loop = false;
        enemyAudioBlendSource.loop = false;
        enemyAudioSource.clip = AlertClip;
        enemyAudioBlendSource.clip = AlertClip;
        enemyAudioSource.volume = AlertVolume;
        enemyAudioBlendSource.volume = 0f;
        enemyAudioSource.Play();

        alertLoopRoutine = StartCoroutine(AlertLoopCrossfadeRoutine());
    }

    private void StopAlertLoopPlaybackLocal()
    {
        if (alertLoopRoutine != null)
        {
            StopCoroutine(alertLoopRoutine);
            alertLoopRoutine = null;
        }

        if (enemyAudioBlendSource != null)
            enemyAudioBlendSource.Stop();
    }

    private IEnumerator AlertLoopCrossfadeRoutine()
    {
        AudioSource current = enemyAudioSource;
        AudioSource next = enemyAudioBlendSource;
        float crossfade = Mathf.Clamp(alertLoopCrossfadeSeconds, 0.01f, 0.5f);

        while (currentAudioMode == AudioMode.Alert && AlertClip != null)
        {
            if (!current.isPlaying)
            {
                current.clip = AlertClip;
                current.loop = false;
                current.volume = AlertVolume;
                current.Play();
            }

            float switchAt = Mathf.Max(0.01f, AlertClip.length - crossfade);
            while (currentAudioMode == AudioMode.Alert && current.isPlaying && current.time < switchAt)
                yield return null;

            if (currentAudioMode != AudioMode.Alert)
                break;

            next.Stop();
            next.clip = AlertClip;
            next.loop = false;
            next.volume = 0f;
            next.Play();

            float t = 0f;
            while (currentAudioMode == AudioMode.Alert && t < crossfade)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Clamp01(t / crossfade);
                current.volume = Mathf.Lerp(AlertVolume, 0f, alpha);
                next.volume = Mathf.Lerp(0f, AlertVolume, alpha);
                yield return null;
            }

            current.Stop();
            current.volume = AlertVolume;

            var tmp = current;
            current = next;
            next = tmp;
        }

        if (enemyAudioSource != null)
            enemyAudioSource.volume = AlertVolume;
        if (enemyAudioBlendSource != null)
            enemyAudioBlendSource.volume = 0f;
        alertLoopRoutine = null;
    }

    void StartSearching()
    {
        if (!IsServer) return;

        isSearching = true;
        isGlancing = false;

        // Keep alert audio while searching
        PlayClipClientRpc(false);

        searchEndTime = Time.time + searchDuration;

        // widen senses while searching
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle = alertedViewAngle;
        _cosHalfFov = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        if (agent != null)
        {
            agent.isStopped = false;
            // let the agent rotate while moving toward lastKnownPosition
            agent.updateRotation = true;
        }
    }

    void StopSearching()
    {
        if (!IsServer) return;

        isSearching = false;
        isGlancing = false;

        detectionRange = originalDetectionRange;
        viewAngle = originalViewAngle;
        _cosHalfFov = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);

        if (agent != null)
        {
            agent.updateRotation = true; // give control back
            agent.ResetPath();
            agent.isStopped = false;
        }

        // Calm down, patrol audio
        PlayClipClientRpc(true);
    }

    public IEnumerator PauseAI(float duration)
    {
        isPaused        = true;
        currentTarget   = null;
        isSearching     = false;
        agent.isStopped = true;
        agent.ResetPath();

        // Silence the enemy completely while paused
        StopAudioClientRpc();

        yield return new WaitForSeconds(duration);

        agent.isStopped = false;
        isPaused        = false;

        // Resume patrol sound after pause ends
        PlayClipClientRpc(true);
    }

    // Server-only: reset this enemy to its calm patrol state for a new match
    public void ServerResetForNewMatch()
    {
        if (!IsServer) return;

        // Clear all state that could keep it in chase/search/lure
        isPaused = false;
        currentTarget = null;
        wasChasing = false;

        isSearching = false;
        isGlancing = false;

        isLured = false;
        lureEndTime = 0f;
        _lastSpottedSfxServerTime = -999f;
        glanceTimer = 0f;
        glanceDirection = 1;

        detectionRange = originalDetectionRange;
        viewAngle = originalViewAngle;

        if (agent != null)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        // Make sure audio returns to patrol for everyone
        PlayClipClientRpc(true);
    }

    public void Lure(Vector3 worldPosition)
    {
        if (!IsServer) return;
        if (isPaused) return;

        // Lure should be able to interrupt chase/search so players can distract enemies.
        currentTarget = null;
        wasChasing = false;
        isSearching = false;
        isGlancing = false;

        preLurePosition = transform.position;
        lureDestination = worldPosition;
        lureEndTime     = 0f;
        isLured         = true;

        // Lure: set destination immediately (not per-frame)
        agent.SetDestination(lureDestination);

        StopAudioClientRpc();

        Debug.Log($"{name} lured to {worldPosition}");
    }

    public bool CanSeeWorldPoint(Vector3 worldPoint, float maxDistance = float.PositiveInfinity, bool requireFacing = false)
    {
        Vector3 toPoint = worldPoint - transform.position;
        float distSqr = toPoint.sqrMagnitude;
        float safeMaxDistance = maxDistance;
        if (float.IsNaN(safeMaxDistance) || float.IsInfinity(safeMaxDistance) || safeMaxDistance <= 0f)
            safeMaxDistance = 10000f;

        float rangeSqr = safeMaxDistance * safeMaxDistance;
        if (distSqr > rangeSqr) return false;
        if (distSqr <= 0.0001f) return true;

        Vector3 dir = toPoint.normalized;

        if (requireFacing)
        {
            float dot = Vector3.Dot(transform.forward, dir);
            float cosHalfFov = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
            if (dot < cosHalfFov) return false;
        }

        float dist = Mathf.Sqrt(distSqr);
        Vector3 origin = transform.position + Vector3.up * 0.2f;

        return !Physics.Raycast(origin, dir, dist, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    public void ServerForceRepathNow()
    {
        if (!IsServer || agent == null || isPaused)
            return;

        _nextRepathTime = 0f;

        if (currentTarget != null)
        {
            agent.isStopped = false;
            agent.SetDestination(GetBestReachablePoint(currentTarget.position));
            return;
        }

        if (isLured)
        {
            agent.isStopped = false;
            agent.SetDestination(lureDestination);
            return;
        }

        if (isSearching)
        {
            agent.isStopped = false;
            agent.SetDestination(lastKnownPosition);
            return;
        }

        agent.ResetPath();
    }

    void HandleLureBehaviour()
    {
        if (!IsServer) return;

        float distToLure = Vector3.Distance(transform.position, lureDestination);

        // Phase 1: walk to lure point
        if (lureEndTime == 0f)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.SetDestination(lureDestination);

            if (!agent.pathPending && distToLure <= agent.stoppingDistance + 0.2f)
            {
                // arrived -> start "investigating"
                lureEndTime = Time.time + investigateDuration;
                agent.ResetPath();
            }

            return;
        }

        // Phase 2: investigate (stand still)
        if (Time.time < lureEndTime)
        {
            agent.ResetPath();
            return;
        }

        // Phase 3: done investigating -> return
        isLured = false;
        lureEndTime = 0f;

        agent.isStopped = false;
        agent.updateRotation = true;
        agent.SetDestination(preLurePosition);

        PlayClipClientRpc(true);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Vector3 leftBoundary  = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0,  viewAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary  * detectionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * detectionRange);

        // Heard radius in blue
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, HeardRadius);
    }
}