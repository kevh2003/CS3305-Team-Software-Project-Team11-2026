using Unity.Netcode;
using UnityEngine;

public class TimerBootstrap : MonoBehaviour
{
    [SerializeField] private TimerNetwork timerNetworkPrefab;

    private bool spawned;

    private void Update()
    {
        if (spawned) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Host/server is running and listening -> spawn the timer once
        if (nm.IsServer && nm.IsListening)
        {
            var timer = Instantiate(timerNetworkPrefab);
            timer.GetComponent<NetworkObject>().Spawn(true);

            spawned = true;
            Debug.Log("[TimerBootstrap] Spawned TimerNetwork.");
        }
    }
}