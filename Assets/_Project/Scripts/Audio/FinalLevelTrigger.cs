using UnityEngine;

public class FinalLevelTrigger : MonoBehaviour
{
    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayFinalLevelMusic();
            }
        }
    }
}