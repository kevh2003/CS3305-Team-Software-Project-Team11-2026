using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
// Applies enemy damage on the server with cooldown and dead-target guards.
public class EnemyAttack : NetworkBehaviour
{
    [Header("Attack")]
    public int damage = 1;
    public float attackCooldown = 5f;

    private EnemyAI enemyAI;
    private bool isAttacking = false;

    // Per-target safety guard to prevent repeated hits from multiple colliders/events.
    private readonly Dictionary<ulong, float> lastHitTime = new();

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (isAttacking) return;
        if (enemyAI.currentTarget == null) return;

        // Skip dead targets (server-authoritative)
        PlayerHealth targetHealth = enemyAI.currentTarget.GetComponentInParent<PlayerHealth>();
        if (targetHealth != null && targetHealth.IsDead.Value)
        {
            enemyAI.ServerClearTargetAndCalmDown();
            return;
        }

        float distance = Vector3.Distance(transform.position, enemyAI.currentTarget.position);

        if (distance <= enemyAI.attackRange)
        {
            StartCoroutine(AttackRoutine(enemyAI.currentTarget));
        }
    }

    private IEnumerator AttackRoutine(Transform target)
    {
        isAttacking = true;

        var health = target != null ? target.GetComponentInParent<PlayerHealth>() : null;
        if (health != null && !health.IsDead.Value)
        {
            // Attack cooldown per player
            ulong id = health.OwnerClientId;
            float now = Time.time;

            if (!lastHitTime.TryGetValue(id, out float last) || now - last >= attackCooldown * 0.8f)
            {
                lastHitTime[id] = now;

                health.TakeDamage(damage);
            }
        }

        yield return StartCoroutine(enemyAI.PauseAI(attackCooldown));
        isAttacking = false;
    }
}