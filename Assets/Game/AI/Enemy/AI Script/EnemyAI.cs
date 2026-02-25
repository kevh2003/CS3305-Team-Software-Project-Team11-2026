using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy AI that detects players using line of sight, chases visible players,
/// and searches the player's last known position by glancing left and right when reaching it.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2f;  // Distance within enemy can grab player
    private bool isPaused;          // EnemyAI halts after attacking, waiting for cooldown

    [Header("Detection Settings")]
    public float detectionRange = 24f;   // Range at which player can be detected
    public float updateRate = 0.3f;      // How often to update target
    public float viewAngle = 175f;       // Field of view angle

    [Header("Search Settings")]
    public float alertedDetectionMultiplier = 1.5f; // Detection range multiplier when alerted
    public float alertedViewAngle = 240f;           // View angle when player is lost
    public float searchDuration = 10f;             // Duration of alert state after losing sight of player
    private Vector3 lastKnownPosition;             // Last known position of the player
    private bool isSearching;                      // Whether the enemy is currently searching
    private float searchEndTime;                   // Time when search state ends
    private float originalDetectionRange;          // Saves original detection range
    private float originalViewAngle;               // Saves original view angle
    private bool isGlancing;                       // Whether enemy is currently performing glance
    private float glanceTime = 1f;                 // How long to pause for each glance
    private float glanceTimer = 0f;
    private int glanceDirection = 1;               // 1 = right, -1 = left

    [Header("Lure Settings")]
    public float investigateDuration = 4f;         // How long the enemy investigates the lure point

    // Lure state
    private bool isLured;                          // Whether the enemy is currently lured
    private Vector3 lureDestination;               // World position the enemy is lured to
    private Vector3 preLurePosition;               // Position enemy was at before being lured
    private float lureEndTime;                     // When the investigation ends

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;   // Player layer
    [SerializeField] private LayerMask obstacleMask;   // Obstacle layer blocking line of sight
    public Transform currentTarget;                    // Current target player
    private NavMeshAgent agent;                        // Reference to NavMeshAgent component

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRange * 0.9f;
        agent.updateRotation = true;
        agent.angularSpeed = 1080f;

        originalDetectionRange = detectionRange;
        originalViewAngle = viewAngle;
    }

    void Start()
    {
        InvokeRepeating(nameof(UpdateTarget), 0f, updateRate);
    }

    void Update()
    {
        if (isPaused)
            return;

        // Lure takes priority over patrol but not over direct player sighting
        if (isLured && currentTarget == null)
        {
            HandleLureBehaviour();
            return;
        }

        if (currentTarget != null)
        {
            // Chase the player
            agent.SetDestination(currentTarget.position);
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
                    isGlancing = true;
                    glanceTimer = 0f;
                    glanceDirection = 1;
                }
            }
            else
            {
                // Perform 90° glance left and right
                agent.ResetPath(); // Stop moving while glancing

                glanceTimer += Time.deltaTime;
                float rotationSpeed = 180f; // degrees per second
                transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

                if (glanceTimer >= glanceTime)
                {
                    // Switch direction
                    glanceDirection *= -1;
                    glanceTimer = 0f;

                    // Optional: after one full left-right cycle, end search
                    if (glanceDirection == 1)
                    {
                        StopSearching();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds the closest visible player using vision cone and line-of-sight checks.
    /// </summary>
    void UpdateTarget()
    {
        if (isPaused)
            return;

        Collider[] playersInRange = Physics.OverlapSphere(transform.position, detectionRange, whatIsPlayer);

        Transform closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider playerCollider in playersInRange)
        {
            Vector3 directionToPlayer = (playerCollider.transform.position - transform.position).normalized;
            float distanceToPlayer = Vector3.Distance(transform.position, playerCollider.transform.position);

            // Vision cone check
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer > viewAngle * 0.5f)
                continue;

            // Line of sight check
            if (!Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleMask))
            {
                if (distanceToPlayer < closestDistance)
                {
                    closestDistance = distanceToPlayer;
                    closestPlayer = playerCollider.transform;

                    isSearching = false;
                    isLured = false;      // cancel lure if player spotted
                    lureEndTime = 0f;
                    detectionRange = originalDetectionRange;
                    viewAngle = originalViewAngle;
                    isGlancing = false;
                }
            }
        }

        if (closestPlayer != null)
        {
            currentTarget = closestPlayer;
        }
        else if (currentTarget != null)
        {
            StartSearching();
        }
    }

    /// <summary>
    /// Begins searching the last known player position.
    /// </summary>
    void StartSearching()
    {
        if (currentTarget == null)
            return;

        lastKnownPosition = currentTarget.position;
        currentTarget = null;

        isSearching = true;
        searchEndTime = Time.time + searchDuration;
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle = alertedViewAngle;
        isGlancing = false;
    }

    /// <summary>
    /// Stops searching and returns to idle state.
    /// </summary>
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

        yield return new WaitForSeconds(duration);

        agent.isStopped = false;
        isPaused = false;
    }

    /// <summary>
    /// Called by CameraLure when the player clicks through the CCTV camera.
    /// Interrupts patrol/idle and sends the enemy to investigate the given world position.
    /// After investigating, the enemy returns to its pre-lure position.
    /// </summary>
    public void Lure(Vector3 worldPosition)
    {
        // Don't lure an enemy that is already chasing the player
        if (currentTarget != null || isPaused)
            return;

        preLurePosition = transform.position;
        lureDestination = worldPosition;
        lureEndTime = 0f; // reset; will be set once enemy arrives
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
            // Still travelling to lure point
            agent.SetDestination(lureDestination);

            if (!agent.pathPending && distToLure <= agent.stoppingDistance + 0.2f)
            {
                // Arrived – start investigating timer
                lureEndTime = Time.time + investigateDuration;
                agent.ResetPath();
            }
        }
        else
        {
            // Investigating – look around a little
            glanceTimer += Time.deltaTime;
            float rotationSpeed = 90f;
            transform.Rotate(Vector3.up, glanceDirection * rotationSpeed * Time.deltaTime);

            if (glanceTimer >= glanceTime)
            {
                glanceDirection *= -1;
                glanceTimer = 0f;
            }

            // Once investigate time is up, return to pre-lure position
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
    }
}
