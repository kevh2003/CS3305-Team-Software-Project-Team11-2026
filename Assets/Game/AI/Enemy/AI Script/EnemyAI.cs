using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

/// <summary>
/// Enemy AI that detects players using line of sight, chases visible players,
/// and searches the player's last known position by glancing left and right when reaching it.
/// Fully server-authoritative — AI logic runs only on the host.
/// Alert sound is broadcast to all clients via ClientRpc when a player is first spotted.
/// </summary>
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

    [Header("Alert Sound")]
    [Tooltip("Played once on all clients when this enemy first spots a player.")]
    public AudioClip AlertClip;

    [Tooltip("How far the alert sound carries in world units.")]
    public float HeardRadius = 30f;

    [Range(0f, 1f)]
    public float AlertVolume = 1f;

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;
    [SerializeField] private LayerMask obstacleMask;
    public Transform currentTarget;
    private NavMeshAgent agent;
    private AudioSource alertSound;

    [Header("Movement Speed")]
    [SerializeField] private float chaseSpeed = 5.5f;

    // Tracks whether the enemy had a target last update tick (server only)
    private bool wasChasing = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        alertSound = GetComponent<AudioSource>();

        agent.stoppingDistance = attackRange * 0.9f;
        agent.updateRotation = true;
        agent.angularSpeed = 1080f;
        agent.speed = chaseSpeed;

        originalDetectionRange = detectionRange;
        originalViewAngle = viewAngle;

        ConfigureAudioSource();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the host/server runs the AI update loop.
        // Clients have no business running pathfinding or detection.
        if (!IsServer) return;

        InvokeRepeating(nameof(UpdateTarget), 0f, updateRate);
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
                    isGlancing = true;
                    glanceTimer = 0f;
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
                    glanceTimer = 0f;

                    if (glanceDirection == 1)
                        StopSearching();
                }
            }
        }
    }

    /// <summary>
    /// Finds the closest visible player using vision cone and line-of-sight checks.
    /// Runs only on the server via InvokeRepeating.
    /// </summary>
    void UpdateTarget()
    {
        if (isPaused) return;

        Collider[] playersInRange = Physics.OverlapSphere(transform.position, detectionRange, whatIsPlayer);

        Transform closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider playerCollider in playersInRange)
        {
            Vector3 directionToPlayer = (playerCollider.transform.position - transform.position).normalized;
            float distanceToPlayer = Vector3.Distance(transform.position, playerCollider.transform.position);

            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer > viewAngle * 0.5f)
                continue;

            if (!Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleMask))
            {
                if (distanceToPlayer < closestDistance)
                {
                    closestDistance = distanceToPlayer;
                    closestPlayer = playerCollider.transform;

                    isSearching = false;
                    isLured = false;
                    lureEndTime = 0f;
                    detectionRange = originalDetectionRange;
                    viewAngle = originalViewAngle;
                    isGlancing = false;
                }
            }
        }

        if (closestPlayer != null)
        {
            // Detect the first-spot moment — only play sound when transitioning
            // from no target to having a target, not every tick while chasing
            bool justSpotted = currentTarget == null && !wasChasing;

            currentTarget = closestPlayer;
            wasChasing = true;

            if (justSpotted)
            {
                // Tell all clients to play the sound at this enemy's position
                PlayAlertSoundClientRpc();
            }
        }
        else if (currentTarget != null)
        {
            wasChasing = false;
            StartSearching();
        }
        else
        {
            // No target and wasn't chasing — keep wasChasing false
            wasChasing = false;
        }
    }

    /// <summary>
    /// Executed on every connected client (including the host).
    /// Each client plays the AudioSource locally so Unity's 3D spatial
    /// falloff applies relative to that client's own AudioListener.
    /// </summary>
    [ClientRpc]
    private void PlayAlertSoundClientRpc()
    {

        // Assign the clip and loop it for the entire chase duration
        alertSound.clip = AlertClip;
        alertSound.Play();

    }

    [ClientRpc]
    private void StopAlertSoundClientRpc()
    {
        alertSound.Stop();
    }


    private void ConfigureAudioSource()
    {
        alertSound.spatialBlend = 1f;                       // Full 3D
        alertSound.rolloffMode = AudioRolloffMode.Logarithmic;
        alertSound.maxDistance = HeardRadius;
        alertSound.minDistance = 1f;                       // Full volume within 1 unit
        alertSound.playOnAwake = false;
        alertSound.loop = true;
        alertSound.dopplerLevel = 0f;
    }

    void StartSearching()
    {
        
        if (currentTarget == null) return;

        StopAlertSoundClientRpc();

        lastKnownPosition = currentTarget.position;
        currentTarget = null;

        isSearching = true;
        searchEndTime = Time.time + searchDuration;
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle = alertedViewAngle;
        isGlancing = false;
    }

    void StopSearching()
    {
        isSearching = false;
        detectionRange = originalDetectionRange;
        viewAngle = originalViewAngle;
        agent.ResetPath();
        isGlancing = false;
    }

    public IEnumerator PauseAI(float duration)
    {
        isPaused = true;
        currentTarget = null;
        isSearching = false;
        agent.isStopped = true;
        agent.ResetPath();

        StopAlertSoundClientRpc();

        yield return new WaitForSeconds(duration);

        agent.isStopped = false;
        isPaused = false;
    }

    /// <summary>
    /// Called by CameraLure when the player clicks through the CCTV camera.
    /// Only has effect when called on the server.
    /// </summary>
    public void Lure(Vector3 worldPosition)
    {
        if (!IsServer) return;
        if (currentTarget != null || isPaused) return;

        preLurePosition = transform.position;
        lureDestination = worldPosition;
        lureEndTime = 0f;
        isLured = true;
        isSearching = false;
        isGlancing = false;
        agent.SetDestination(lureDestination);

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
                glanceTimer = 0f;
            }

            if (Time.time >= lureEndTime)
            {
                isLured = false;
                lureEndTime = 0f;
                glanceDirection = 1;
                glanceTimer = 0f;
                agent.SetDestination(preLurePosition);
                Debug.Log($"{name} finished investigating, returning to original position");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * detectionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * detectionRange);

        // Heard radius in blue
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, HeardRadius);
    }
}