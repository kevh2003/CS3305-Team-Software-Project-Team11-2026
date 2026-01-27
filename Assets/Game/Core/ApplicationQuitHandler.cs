using UnityEngine;

/*
 * Application Quit Handler
 * 
 * This script simply ensures that if a user closes their game via unconventional means e.g alt f4,
 * it will shutdown any Netcode sessions and background tasks, otherwise it will continue to run even
 * if the user quits the application :O
 */

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