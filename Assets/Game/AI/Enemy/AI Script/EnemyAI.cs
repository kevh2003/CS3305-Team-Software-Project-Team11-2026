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

    [Header("Audio")]
    [Tooltip("Looping clip played while the enemy is patrolling. Quieter and calmer.")]
    public AudioClip PatrolClip;

    [Tooltip("Looping clip played while the enemy is chasing a player.")]
    public AudioClip AlertClip;

    [Tooltip("Volume used when playing the patrol clip.")]
    [Range(0f, 1f)]
    public float PatrolVolume = 0.4f;

    [Tooltip("Volume used when playing the alert clip.")]
    [Range(0f, 1f)]
    public float AlertVolume = 1f;

    [Tooltip("How far the sounds carry in world units.")]
    public float HeardRadius = 30f;

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private LayerMask obstacleMask;
    public Transform currentTarget;
    private NavMeshAgent agent;

    // Single shared AudioSource — clip and volume are swapped depending on state
    private AudioSource enemyAudioSource;

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

        // Everyone: stop audio so it can't "stick" across scene/match transitions
        StopAudioClientRpc();
    }

    void Update()
    {
        // All movement and AI logic is server-only.
        // On a LAN listen server the host runs this locally — clients skip it entirely.
        if (!IsServer) return;

        if (isPaused) return;

        // Lure takes priority over patrol but not over direct player sighting
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
                agent.SetDestination(currentTarget.position);
            }
        }
        else if (isSearching)
        {
            if (!isGlancing)
            {
                // Move toward the last known position
                agent.SetDestination(lastKnownPosition);

                if (Vector3.Distance(transform.position, lastKnownPosition) <= agent.stoppingDistance)
                {
                    // Reached last known position → start glancing
                    isGlancing      = true;
                    glanceTimer     = 0f;
                    glanceDirection = 1;
                }
            }
            else
            {
                // 90° glance left and right
                agent.ResetPath(); // Stop moving while glancing

                glanceTimer += Time.deltaTime;
                float rotationSpeed = 180f; // degrees per second
                transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

                if (glanceTimer >= glanceTime)
                {
                    // Switch direction
                    glanceDirection *= -1;
                    glanceTimer      = 0f;

                    // After one full left-right cycle, end search
                    if (glanceDirection == 1)
                        StopSearching();
                }
            }
        }
    }

    void UpdateTarget()
    {
        if (isPaused) return;

        // If current target dies, stop chasing
        if (currentTarget != null)
        {
            var th = currentTarget.GetComponentInParent<PlayerHealth>();
            if (th == null || th.IsDead.Value)
            {
                ServerClearTargetAndCalmDown();
                return;
            }
        }

        // Performance : non-alloc overlap sphere (avoids GC every tick)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRange, _playerHits, whatIsPlayer);

        Transform closestPlayer  = null;
        float closestDistSqr     = Mathf.Infinity;
        float rangeSqr           = detectionRange * detectionRange;

        for (int i = 0; i < hitCount; i++)
        {
            Collider playerCollider = _playerHits[i];
            if (playerCollider == null) continue;

            // Skip dead players
            var ph = playerCollider.GetComponentInParent<PlayerHealth>();
            if (ph != null && ph.IsDead.Value)
                continue;

            // Vector to player
            Vector3 toPlayer = playerCollider.transform.position - transform.position;

            float distSqr = toPlayer.sqrMagnitude;
            if (distSqr > rangeSqr) continue;

            Vector3 dir = toPlayer.normalized;

            // dot-product check instead of Angle()
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < _cosHalfFov) continue;

            float dist = Mathf.Sqrt(distSqr);

            if (!Physics.Raycast(transform.position, dir, dist, obstacleMask))
            {
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestPlayer  = playerCollider.transform;

                    // reset any "alerted" state when sees a valid player
                    isSearching    = false;
                    isLured        = false;
                    lureEndTime    = 0f;
                    detectionRange = originalDetectionRange;
                    viewAngle      = originalViewAngle;
                    _cosHalfFov    = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
                    isGlancing     = false;
                }
            }
        }

        if (closestPlayer != null)
        {
            // Treat as “just spotted” if wasn't currently chasing anyone
            bool justSpotted = (currentTarget == null) || !wasChasing;

            currentTarget = closestPlayer;
            wasChasing = true;

            if (justSpotted)
                PlayClipClientRpc(false);
        }
        else if (currentTarget != null)
        {
            wasChasing = false;
            StartSearching();
        }
        else
        {
            wasChasing = false;
        }
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
            agent.ResetPath();

        // Put audio back to patrol everywhere
        PlayClipClientRpc(true);
    }

    [ClientRpc]
    private void PlayClipClientRpc(bool usePatrol)
    {
        if (enemyAudioSource == null) return;

        AudioClip nextClip;
        float nextVol;

        if (usePatrol)
        {
            if (PatrolClip == null)
            {
                Debug.LogWarning($"EnemyAI: No PatrolClip assigned on {gameObject.name}!");
                return;
            }
            nextClip = PatrolClip;
            nextVol = PatrolVolume;
        }
        else
        {
            if (AlertClip == null)
            {
                Debug.LogWarning($"EnemyAI: No AlertClip assigned on {gameObject.name}!");
                return;
            }
            nextClip = AlertClip;
            nextVol = AlertVolume;
        }

        // If clip already playing, don't restart it
        if (enemyAudioSource.isPlaying && enemyAudioSource.clip == nextClip)
            return;

        enemyAudioSource.volume = nextVol;
        enemyAudioSource.clip = nextClip;

        enemyAudioSource.Play();
    }

    [ClientRpc]
    private void StopAudioClientRpc()
    {
        if (enemyAudioSource == null) return;
        enemyAudioSource.Stop();
    }

    private void ConfigureAudioSource()
    {
        enemyAudioSource.spatialBlend = 1f;                     // Full 3D spatial audio
        enemyAudioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        enemyAudioSource.maxDistance  = HeardRadius;
        enemyAudioSource.minDistance  = 1f;                     // Full volume within 1 unit
        enemyAudioSource.playOnAwake  = false;
        enemyAudioSource.loop         = true;                   // Both clips loop continuously
        enemyAudioSource.dopplerLevel = 0f;
    }

    void StartSearching()
    {
        if (currentTarget == null) return;

        PlayClipClientRpc(false);

        lastKnownPosition = currentTarget.position;
        currentTarget     = null;

        isSearching    = true;
        searchEndTime  = Time.time + searchDuration;
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle      = alertedViewAngle;
        _cosHalfFov    = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        isGlancing     = false;
    }

    void StopSearching()
    {
        isSearching    = false;
        detectionRange = originalDetectionRange;
        viewAngle      = originalViewAngle;
        _cosHalfFov    = Mathf.Cos(viewAngle * 0.5f * Mathf.Deg2Rad);
        agent.ResetPath();
        isGlancing     = false;

        // Enemy has fully calmed down — resume patrol sound
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
        if (currentTarget != null || isPaused) return;

        preLurePosition = transform.position;
        lureDestination = worldPosition;
        lureEndTime     = 0f;
        isLured         = true;
        isSearching     = false;
        isGlancing      = false;

        // Lure: set destination immediately (not per-frame)
        agent.SetDestination(lureDestination);

        StopAudioClientRpc();

        Debug.Log($"{name} lured to {worldPosition}");
    }

    void HandleLureBehaviour()
    {
        float distToLure = Vector3.Distance(transform.position, lureDestination);

        if (lureEndTime == 0f)
        {
            agent.SetDestination(lureDestination);

            if (!agent.pathPending && distToLure <= agent.stoppingDistance + 0.2f)
            {
                lureEndTime = Time.time + investigateDuration;
                agent.ResetPath();
            }
        }
        else
        {
            glanceTimer += Time.deltaTime;
            float rotationSpeed = 90f;
            transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

            if (glanceTimer >= glanceTime)
            {
                glanceDirection *= -1;
                glanceTimer      = 0f;
            }

            if (Time.time >= lureEndTime)
            {
                isLured         = false;
                lureEndTime     = 0f;
                glanceDirection = 1;
                glanceTimer     = 0f;
                agent.SetDestination(preLurePosition);

                PlayClipClientRpc(true);
            }
        }
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