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
    public float updateRate = 0.3f;
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

    // Tracks whether the enemy had a target last update tick (server only)
    private bool wasChasing = false;

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

        ConfigureAudioSource();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the host/server runs the AI update loop.
        // Clients have no business running pathfinding or detection.
        if (!IsServer) return;

        InvokeRepeating(nameof(UpdateTarget), 0f, updateRate);

        PlayClipClientRpc(true);
    }

    void Update()
    {
        // All movement and AI logic is server-only.
        // On a LAN listen server the host runs this locally — clients skip it entirely.
        if (!IsServer) return;

        if (isPaused) return;

        if (isLured && currentTarget == null)
        {
            HandleLureBehaviour();
            return;
        }

        if (currentTarget != null)
        {
            agent.SetDestination(currentTarget.position);
        }
        else if (isSearching)
        {
            if (!isGlancing)
            {
                agent.SetDestination(lastKnownPosition);

                if (Vector3.Distance(transform.position, lastKnownPosition) <= agent.stoppingDistance)
                {
                    isGlancing      = true;
                    glanceTimer     = 0f;
                    glanceDirection = 1;
                }
            }
            else
            {
                agent.ResetPath();

                glanceTimer += Time.deltaTime;
                float rotationSpeed = 180f;
                transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

                if (glanceTimer >= glanceTime)
                {
                    glanceDirection *= -1;
                    glanceTimer      = 0f;

                    if (glanceDirection == 1)
                        StopSearching();
                }
            }
        }
    }

    void UpdateTarget()
    {
        if (isPaused) return;

        Collider[] playersInRange = Physics.OverlapSphere(transform.position, detectionRange, whatIsPlayer);

        Transform closestPlayer   = null;
        float     closestDistance = Mathf.Infinity;

        foreach (Collider playerCollider in playersInRange)
        {
            Vector3 directionToPlayer = (playerCollider.transform.position - transform.position).normalized;
            float   distanceToPlayer  = Vector3.Distance(transform.position, playerCollider.transform.position);

            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer > viewAngle * 0.5f)
                continue;

            if (!Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleMask))
            {
                if (distanceToPlayer < closestDistance)
                {
                    closestDistance = distanceToPlayer;
                    closestPlayer   = playerCollider.transform;

                    isSearching    = false;
                    isLured        = false;
                    lureEndTime    = 0f;
                    detectionRange = originalDetectionRange;
                    viewAngle      = originalViewAngle;
                    isGlancing     = false;
                }
            }
        }

        if (closestPlayer != null)
        {
            bool justSpotted = currentTarget == null && !wasChasing;

            currentTarget = closestPlayer;
            wasChasing    = true;

            if (justSpotted)
            {
                PlayClipClientRpc(false);
            }
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

    [ClientRpc]
    private void PlayClipClientRpc(bool usePatrol)
    {
        if (usePatrol)
        {
            if (PatrolClip == null)
            {
                Debug.LogWarning($"⚠️ EnemyAI: No PatrolClip assigned on {gameObject.name}!");
                return;
            }

            enemyAudioSource.volume = PatrolVolume;
            enemyAudioSource.clip   = PatrolClip;
        }
        else
        {
            if (AlertClip == null)
            {
                Debug.LogWarning($"⚠️ EnemyAI: No AlertClip assigned on {gameObject.name}!");
                return;
            }

            enemyAudioSource.volume = AlertVolume;
            enemyAudioSource.clip   = AlertClip;
        }

        // Play picks up the new clip from the beginning cleanly
        enemyAudioSource.Play();
    }

    [ClientRpc]
    private void StopAudioClientRpc()
    {
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

        StopAudioClientRpc();

        lastKnownPosition = currentTarget.position;
        currentTarget     = null;

        isSearching    = true;
        searchEndTime  = Time.time + searchDuration;
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle      = alertedViewAngle;
        isGlancing     = false;
    }

    void StopSearching()
    {
        isSearching    = false;
        detectionRange = originalDetectionRange;
        viewAngle      = originalViewAngle;
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
