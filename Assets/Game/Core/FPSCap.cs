using UnityEngine;

public class FpsCap : MonoBehaviour
{
    [SerializeField] private int targetFps = 60;

    private void Awake()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }
}