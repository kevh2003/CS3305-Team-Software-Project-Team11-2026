using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles patrol movement between a set of patrol points.
/// Automatically pauses patrol when EnemyAI is chasing or searching,
/// and resumes patrol when EnemyAI returns to idle.
/// </summary>
/// 
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyAI))]
public class EnemyPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    public Transform[] patrolPoints;     // Empty game objects as patrol points
    public float waitTimeAtPoint = 3f;   // Time to wait at each patrol point
    public bool loopPatrol = true;       // Loop or stop at end

    private NavMeshAgent agent;
    private EnemyAI enemyAI;

    private int currentPointIndex;
    private float waitTimer;
    private bool waiting;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAI = GetComponent<EnemyAI>();
    }

    void Start()
    {
        if (patrolPoints.Length > 0)
        {
            agent.SetDestination(patrolPoints[0].position);
        }
    }

    void Update()
    {
        // If EnemyAI is busy (chasing or searching), patrol is disabled
        if (enemyAIHasControl())
        {
            waiting = false;
            return;
        }

        PatrolBehaviour();
    }

    bool enemyAIHasControl()
    {
        // EnemyAI controls movement if it has a target or is searching
        return enemyAI.enabled &&
               (enemyAIHasTarget() || enemyAIIsSearching());
    }

    bool enemyAIHasTarget()
    {
        // Uses reflection-safe approach by checking destination updates
        return agent.hasPath && agent.remainingDistance > agent.stoppingDistance;
    }

    bool enemyAIIsSearching()
    {
        // EnemyAI uses SetDestination during searching, so we just check path activity
        return agent.hasPath && agent.velocity.magnitude > 0.1f;
    }

    void PatrolBehaviour()
    {
        if (patrolPoints.Length == 0)
            return;

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!waiting)
            {
                waiting = true;
                waitTimer = waitTimeAtPoint;
                agent.ResetPath();
            }
        }

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                MoveToNextPoint();
            }
        }
    }

    void MoveToNextPoint()
    {
        currentPointIndex++;

        if (currentPointIndex >= patrolPoints.Length)
        {
            if (loopPatrol)
                currentPointIndex = 0;
            else
                return;
        }

        agent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Gizmos.color = Color.cyan;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;

            Gizmos.DrawSphere(patrolPoints[i].position, 0.3f);

            if (i + 1 < patrolPoints.Length)
            {
                Gizmos.DrawLine(
                    patrolPoints[i].position,
                    patrolPoints[i + 1].position
                );
            }
            else if (loopPatrol)
            {
                Gizmos.DrawLine(
                    patrolPoints[i].position,
                    patrolPoints[0].position
                );
            }
        }
    }
}
