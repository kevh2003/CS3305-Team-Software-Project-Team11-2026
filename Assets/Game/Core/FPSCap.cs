using UnityEngine;

// Applies a startup frame-rate cap and disables v-sync for consistent limits.
public class FpsCap : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps > 0 ? targetFps : -1;
    }
}