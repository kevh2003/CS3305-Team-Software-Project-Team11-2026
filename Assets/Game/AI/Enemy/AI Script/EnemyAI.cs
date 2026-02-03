using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy AI that detects players using line of sight, chases the closest visible player,
/// and stops when no player is in sight.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Attack Settings")]
    public float damage = 10f;          // Damage per attack
    public float attackRange = 2f;      // Distance required to attack
    public float attackCooldown = 1f;   // Time between attacks
    private float nextAttackTime;       // Next allowed attack timestamp

    [Header("Detection Settings")]
    public float detectionRange = 15f;  // Maximum vision distance
    public float updateRate = 0.25f;    // How often the enemy updates targets
    public float viewAngle = 175f;      // The size of the angle of sightline
    [SerializeField] private LayerMask whatIsPlayer;   // Player layer mask
    [SerializeField] private LayerMask obstacleMask;   // Obstacles that block vision

    private NavMeshAgent agent;         // Handles movement and pathfinding
    private Transform currentTarget;    // Currently targeted player

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRange * 0.9f;
        agent.updateRotation = true;     // Allow agent to rotate toward target
        agent.angularSpeed = 1080f;       // Rotation speed in degrees/sec
    }

    void Start()
    {
        // Periodically check for visible players
        InvokeRepeating(nameof(UpdateTarget), 0f, updateRate);
    }

    void Update()
    {
        // Move toward the current target if one exists
        if (currentTarget != null)
        {
            agent.SetDestination(currentTarget.position);
        }
    }

    /// <summary>
    /// Finds the closest player that is visible (line-of-sight) and sets it as the current target.
    /// </summary>
    void UpdateTarget()
{
    // Find all players in detection range
    Collider[] playersInRange = Physics.OverlapSphere(transform.position, detectionRange, whatIsPlayer);

    Transform closestPlayer = null;
    float closestDistance = Mathf.Infinity;

    foreach (Collider playerCollider in playersInRange)
    {
        Vector3 directionToPlayer = (playerCollider.transform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerCollider.transform.position);

        // 1. Check if the player is inside the vision cone
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > viewAngle * 0.5f)
            continue;  // Skip players outside the vision cone

        // 2. Check for line-of-sight (obstacles)
        if (!Physics.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleMask))
        {
            // Player is visible
            if (distanceToPlayer < closestDistance)
            {
                closestDistance = distanceToPlayer;
                closestPlayer = playerCollider.transform;
            }
        }
    }

    if (closestPlayer != null)
    {
        currentTarget = closestPlayer;
    }
    else
    {
        StopChasing();
    }
}


    /// <summary>
    /// Stops chasing the current target and clears the NavMesh path.
    /// </summary>
    void StopChasing()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            agent.ResetPath();
        }
    }

    /// <summary>
    /// Draws detection and attack ranges in the editor for debugging.
    /// </summary>
    void OnDrawGizmosSelected()
{
    // Detection radius
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, detectionRange);

    // Vision cone boundaries
    Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
    Vector3 rightBoundary = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;

    Gizmos.color = Color.red;
    Gizmos.DrawLine(transform.position, transform.position + leftBoundary * detectionRange);
    Gizmos.DrawLine(transform.position, transform.position + rightBoundary * detectionRange);
}
}
