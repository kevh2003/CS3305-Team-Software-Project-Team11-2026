using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Optional RLGL end trigger.
/// By default this no longer ends the game; containment-based elimination
/// should drive the mode instead.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RedLightFinishLine : MonoBehaviour
{
    [SerializeField] private RedLightGreenLightBoss boss;
    [SerializeField] private bool endGameOnCross = false;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!endGameOnCross) return;

        var player = other.GetComponentInParent<NetworkPlayer>();
        if (player == null) return;

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return;

        Debug.Log($"[FinishLine] Player {player.OwnerClientId} crossed the finish line!");
        boss?.NotifyPlayerWon();
    }
}