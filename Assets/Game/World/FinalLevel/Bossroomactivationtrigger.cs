using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Starts RLGL immediately when the first living player enters this trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossRoomActivationTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RedLightGreenLightBoss boss;

    private bool _activated;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_activated) return;

        var player = other.GetComponentInParent<NetworkPlayer>();
        if (player == null) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return;

        _activated = true;
        Debug.Log($"[BossRoomTrigger] Player {player.OwnerClientId} entered. Activating RLGL.");
        boss?.Activate();
    }
}