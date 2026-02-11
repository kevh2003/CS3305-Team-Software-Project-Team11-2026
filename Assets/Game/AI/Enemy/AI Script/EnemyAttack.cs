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
        if (health != null)
        {
            health.TakeDamage(damage);
        }

        // Pause the entire AI for 5 seconds
        yield return StartCoroutine(enemyAI.PauseAI(attackCooldown));

        isAttacking = false;
    }
}
