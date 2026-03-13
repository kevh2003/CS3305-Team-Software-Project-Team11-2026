using UnityEngine;

// Ensures networking is shut down when the application exits.
public sealed class ApplicationQuitHandler : MonoBehaviour
{
    private void OnApplicationQuit()
    {
        Debug.Log("[App] Application quitting, shutting down networking.");

        if (Services.NetSession != null)
        {
            Services.NetSession.Shutdown();
        }
    }
}