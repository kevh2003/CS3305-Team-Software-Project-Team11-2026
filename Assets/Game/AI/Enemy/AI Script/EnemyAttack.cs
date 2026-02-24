using UnityEngine;
using System.Collections;

public class EnemyAttack : MonoBehaviour
{
    public float damage = 1f;
    public float attackCooldown = 5f;

    private EnemyAI enemyAI;
    private bool isAttacking = false;

    void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
    }

    void Update()
    {
        if (isAttacking) return;
        if (enemyAI.currentTarget == null) return;

        // Skip dead targets
        PlayerHealth targetHealth = enemyAI.currentTarget.GetComponent<PlayerHealth>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            enemyAI.currentTarget = null; // stop targeting dead player
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            enemyAI.currentTarget.position
        );

        if (distance <= enemyAI.attackRange)
        {
            StartCoroutine(AttackRoutine(enemyAI.currentTarget));
        }
    }

    IEnumerator AttackRoutine(Transform target)
    {
        isAttacking = true;

        // Deal damage
        PlayerHealth health = target.GetComponent<PlayerHealth>();
        if (health != null && !health.IsDead)
        {
            health.TakeDamage(damage);
        }

        // Pause the AI
        yield return StartCoroutine(enemyAI.PauseAI(attackCooldown));

        isAttacking = false;
    }
}
