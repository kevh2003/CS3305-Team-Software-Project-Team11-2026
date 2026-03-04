using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Place at the far end of the boss room as the win condition.
/// When a living player crosses it, tells the boss the game is over.
///
/// SETUP:
/// 1. Create an empty GameObject at the finish line.
/// 2. Add a trigger Collider (BoxCollider, IsTrigger = true).
/// 3. Attach this script and assign the boss reference.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RedLightFinishLine : MonoBehaviour
{
    [SerializeField] private RedLightGreenLightBoss boss;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var player = other.GetComponent<NetworkPlayer>();
        if (player == null) return;

        var health = other.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return;

        Debug.Log($"[FinishLine] Player {player.OwnerClientId} crossed the finish line!");
        boss?.NotifyPlayerWon();
    }
}