using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 3;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int MaxHealth => maxHealth;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDead.Value = false;
        }
    }

    // Keep this signature so your EnemyAttack can call it.
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;

        if (IsServer) ApplyDamage(amount);
        else TakeDamageServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        if (IsDead.Value) return;

        CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value - amount, 0, maxHealth);
        if (CurrentHealth.Value <= 0) IsDead.Value = true;
    }
}
