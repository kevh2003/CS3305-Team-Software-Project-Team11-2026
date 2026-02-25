using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
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

    [Header("Death Behaviour")]
    [SerializeField] private string aliveLayer = "WhatsIsPlayer";
    [SerializeField] private string deadLayer = "DeadPlayer";

    [Tooltip("Scripts to disable when dead (Interactor etc).")]
    [SerializeField] private Behaviour[] disableOnDeath;

    [Tooltip("Optional root object containing meshes.")]
    [SerializeField] private GameObject visualsRoot;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDead.Value = false;
        }

        // React to death changes on ALL clients
        IsDead.OnValueChanged += OnDeadChanged;

        ApplyDeathState(IsDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= OnDeadChanged;
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        ApplyDeathState(current);
    }

    private void ApplyDeathState(bool dead)
    {
        // Layer swap
        int alive = LayerMask.NameToLayer(aliveLayer);
        int deadL = LayerMask.NameToLayer(deadLayer);

        if (alive != -1 && deadL != -1)
            gameObject.layer = dead ? deadL : alive;

        // Disable interaction scripts
        if (disableOnDeath != null)
        {
            foreach (var script in disableOnDeath)
            {
                if (script != null)
                    script.enabled = !dead;
            }
        }

        // Hide visuals
        if (visualsRoot != null)
        {
            visualsRoot.SetActive(!dead);
        }
        else
        {
            // fallback
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.enabled = !dead;
        }
    }

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

        if (CurrentHealth.Value <= 0)
            IsDead.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetHealthServerRpc()
    {
        CurrentHealth.Value = maxHealth;
        IsDead.Value = false;
    }
}