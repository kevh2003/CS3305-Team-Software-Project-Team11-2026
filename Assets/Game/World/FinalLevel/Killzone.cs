using UnityEngine;
using Unity.Netcode;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        if (health != null)
        {
            // Route through normal damage flow so server-side game-over checks run.
            health.TakeDamage(health.MaxHealth);
        }
    }
}