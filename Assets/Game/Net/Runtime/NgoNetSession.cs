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

    public bool IsHost => networkManager != null && networkManager.IsHost;
    public bool IsClient => networkManager != null && networkManager.IsClient;
    public bool IsServer => networkManager != null && networkManager.IsServer;
    public bool IsConnected => networkManager != null && (networkManager.IsClient || networkManager.IsServer);

    public int ConnectedPlayers => networkManager != null ? (int)networkManager.ConnectedClientsList.Count : 0;

    public event Action OnConnected;
    public event Action<string> OnDisconnected;

    private void Awake()
    {
        // Fallback : allow prefabs to auto-wire these if missing
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
        // Fires for host too when it starts
        if (networkManager.LocalClientId == clientId)
            OnConnected?.Invoke();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager.LocalClientId == clientId)
            OnDisconnected?.Invoke("Disconnected");
    }

    public NetStartResult HostLan(ushort port)
    {
        if (transport == null || networkManager == null) return NetStartResult.NotInitialized;

        // 0.0.0.0 binds to all local interfaces (LAN host)
        transport.SetConnectionData("0.0.0.0", port);
        return networkManager.StartHost() ? NetStartResult.Success : NetStartResult.TransportError;
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

    public void Shutdown()
    {
        if (networkManager == null) return;
        networkManager.Shutdown();
    }
}