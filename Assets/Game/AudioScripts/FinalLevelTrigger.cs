using UnityEngine;

public class FinalLevelTrigger : MonoBehaviour
{
    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (!other.CompareTag("Player")) return;

        // Only switch music for the local owning player.
        var player = other.GetComponentInParent<NetworkPlayer>();
        if (player == null || !player.IsOwner) return;

        triggered = true;
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayFinalLevelMusic();
    }
}