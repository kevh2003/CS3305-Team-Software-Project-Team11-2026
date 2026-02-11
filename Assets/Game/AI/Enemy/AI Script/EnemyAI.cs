using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy AI that detects players using line of sight, chases visible players,
/// and searches the player's last known position when line of sight is lost.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2f;  //Distance within enemy can grab player
    private bool isPaused;      //EnemyAI halts after attacking, waiting for the cooldown to expire

    [Header("Detection Settings")]
    public float detectionRange = 24f;  //Range at which player can be detected
    public float updateRate = 0.3f;    //How often to update target
    public float viewAngle = 175f;        //Field of view angle

    [Header("Search Settings")]
    public float alertedDetectionMultiplier = 1.5f; //Multiplier for detection range when alerted
    public float alertedViewAngle = 240f;       //View angle wheen player is lost
    public float searchDuration = 10f;      //Duration of alert state after losing sight of player
    public float searchOvershootDistance = 10f; //Extra distance over last known location of player
    private Vector3 lastKnownPosition;  // Last known position of the player
    private bool isSearching;         // Whether the enemy is currently searching
    private float searchEndTime;        // Time when search state ends
    private float originalDetectionRange;   // Saves original detection range
    private float originalViewAngle;    // Saves original view angels

    [Header("References")]
    [SerializeField] private LayerMask whatIsPlayer;    //Player layer
    [SerializeField] private LayerMask obstacleMask;    //Obstacle layer blocking line of sight
    public Transform currentTarget;   // Current target player
    private NavMeshAgent agent;     // Reference to NavMeshAgent component

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
        {
            return;
        }

        if (currentTarget != null)
        {
            agent.SetDestination(currentTarget.position);
        }
        else if (isSearching)
        {
            agent.SetDestination(lastKnownPosition);

            if (Time.time >= searchEndTime ||
                Vector3.Distance(transform.position, lastKnownPosition) <= agent.stoppingDistance)
            {
                StopSearching();
            }
        }
    }

    /// <summary>
    /// Finds the closest visible player using vision cone and line-of-sight checks.
    /// </summary>
    void UpdateTarget()
    {

        if (isPaused)
        {
            return;
        }
        Collider[] playersInRange = Physics.OverlapSphere(
            transform.position,
            detectionRange,
            whatIsPlayer
        );

        Transform closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider playerCollider in playersInRange)
        {
            Vector3 directionToPlayer =
                (playerCollider.transform.position - transform.position).normalized;

            float distanceToPlayer =
                Vector3.Distance(transform.position, playerCollider.transform.position);

            // Vision cone check
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer > viewAngle * 0.5f)
                continue;

            // Line of sight check
            if (!Physics.Raycast(
                transform.position,
                directionToPlayer,
                distanceToPlayer,
                obstacleMask))
            {
                if (distanceToPlayer < closestDistance)
                {
                    closestDistance = distanceToPlayer;
                    closestPlayer = playerCollider.transform;

                    Vector3 directionToPlayerFlat = directionToPlayer;
                    directionToPlayerFlat.y = 0f;
                    directionToPlayerFlat.Normalize();

                    lastKnownPosition = playerCollider.transform.position + directionToPlayerFlat * searchOvershootDistance;
                    isSearching = false;
                    detectionRange = originalDetectionRange;
                    viewAngle = originalViewAngle;
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
        lastKnownPosition = currentTarget.position;
        currentTarget = null;

        isSearching = true;
        searchEndTime = Time.time + searchDuration;
        detectionRange = originalDetectionRange * alertedDetectionMultiplier;
        viewAngle = alertedViewAngle;

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
    /// Draws detection radius and vision cone for debugging.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Vector3 leftBoundary =
            Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBoundary =
            Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * detectionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * detectionRange);
    }
}
