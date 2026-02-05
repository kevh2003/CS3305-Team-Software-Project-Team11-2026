using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * NgoNetSession
 * 
 * NGO-backed implementation of INetSession
 * This should be the main (ideally only) place that touches NetworkManager/Transport
 */

public sealed class NgoNetSession : MonoBehaviour, INetSession
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;
    [SerializeField] private ushort basePort = 7777; // default port 
    [SerializeField] private int maxAttempts = 15; // if port 7777 is occupied, attempt up to 15 subsequent ports

    public bool IsHost => networkManager != null && networkManager.IsHost; // is both a server and client
    public bool IsClient => networkManager != null && networkManager.IsClient; // is a client of a server
    public bool IsServer => networkManager != null && networkManager.IsServer; // is a server
    public bool IsConnected => networkManager != null && (networkManager.IsServer || networkManager.IsConnectedClient); // is connected to a running/active server

    public string LastHostIp { get; private set; } = "";
    public ushort LastHostPort { get; private set; } = 0;

    public int ConnectedPlayers => networkManager != null ? (int)networkManager.ConnectedClientsList.Count : 0;

    public event Action OnConnected;
    public event Action<string> OnDisconnected;

    private void Awake()
    {
        // Fallback : will allow prefabs to auto-wire these if missing
        if (networkManager == null) networkManager = GetComponent<NetworkManager>();
        if (transport == null) transport = GetComponent<UnityTransport>();

        Services.NetSession = this;

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

        Debug.Log("[NetSession] Ready");
    }

    private void OnDestroy()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager.LocalClientId == clientId)
            OnConnected?.Invoke();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // Fires for host too when it starts
        if (networkManager.LocalClientId == clientId)
        {
            OnDisconnected?.Invoke("Failed to connect to host (check IP/port, host running, same network).");
        }
    }

    // NOTE: HostLan method is currently only useful for debugging, particularly regarding port number issues.
    // It does not actively get used by the current build.
    // Instead, the HostLanAutoPort method is used, as it automatically finds a port for the host to use.
    // I will likely remove the HostLan method in future - kev
    private NetStartResult HostLan(ushort port)
    {
        if (transport == null || networkManager == null) return NetStartResult.NotInitialized;

        transport.SetConnectionData("0.0.0.0", port);
        bool ok = networkManager.StartHost();

        if (ok)
        {
            LastHostPort = port;
            LastHostIp = NetUtil.GetLocalIPv4();
        }

        return ok ? NetStartResult.Success : NetStartResult.TransportError;
    }

    public NetStartResult HostLanAutoPort(out ushort chosenPort)
    {
        chosenPort = 0;

        if (transport == null || networkManager == null)
            return NetStartResult.NotInitialized;

        // If already running, cleanly stop first
        if (networkManager.IsListening || networkManager.IsClient || networkManager.IsServer)
            networkManager.Shutdown();

        for (int i = 0; i <= maxAttempts; i++)
        {
            ushort portToTry = (ushort)(basePort + i);

            Debug.Log($"[NetSession] Trying to host on port {portToTry}...");

            var res = HostLan(portToTry);
            if (res == NetStartResult.Success)
            {
                chosenPort = portToTry;
                Debug.Log($"[NetSession] Hosting started on port {chosenPort}");
                return NetStartResult.Success;
            }

            // Reset state before next attempt
            networkManager.Shutdown();
        }

        Debug.LogError($"[NetSession] Failed to host on any port in range {basePort}-{basePort + maxAttempts}.");
        return NetStartResult.TransportError;
    }

    public NetStartResult JoinLan(string address, ushort port)
    {
        if (transport == null || networkManager == null) return NetStartResult.NotInitialized;
        if (string.IsNullOrWhiteSpace(address)) return NetStartResult.InvalidInput;

        transport.SetConnectionData(address, port);
        return networkManager.StartClient() ? NetStartResult.Success : NetStartResult.TransportError;
    }

    public async Task<(NetStartResult result, string joinCode)> HostOnlineAsync(int maxPlayers)
    {
        // PLACEHOLDER Relay implementation will be added later ***** - kev
        await Task.CompletedTask;
        return (NetStartResult.ServicesUnavailable, string.Empty);
    }

    public async Task<NetStartResult> JoinOnlineAsync(string joinCode)
    {
        // PLACEHOLDER Relay implementation will be added later ***** - kev
        await Task.CompletedTask;
        return NetStartResult.ServicesUnavailable;
    }

    public void LoadSceneForAll(string sceneName)
    {
        // Only host is allowed to drive networked scene transitions
        if (!IsHost) return;

        if (networkManager.SceneManager == null)
        {
            Debug.LogError("[NetSession] SceneManager not enabled on NetworkManager.");
            return;
        }

        networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void OnApplicationQuit()
    {
        if (networkManager != null)
            networkManager.Shutdown();
    }

    public void Shutdown()
    {
        if (networkManager == null) return;
        networkManager.Shutdown();
    }
}