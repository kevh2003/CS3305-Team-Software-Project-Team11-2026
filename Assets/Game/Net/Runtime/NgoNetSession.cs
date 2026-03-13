using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
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
#if UNITY_WEBGL
    private const string RelayConnectionType = "wss";
#else
    private const string RelayConnectionType = "dtls";
#endif

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
    public string LastOnlineJoinCode { get; private set; } = "";

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

    // Internal host helper used by HostLanAutoPort.
    private NetStartResult HostLan(ushort port)
    {
        if (transport == null || networkManager == null) return NetStartResult.NotInitialized;

        transport.SetConnectionData("0.0.0.0", port);
        bool ok = networkManager.StartHost();

        if (ok)
        {
            LastHostPort = port;
            LastHostIp = NetUtil.GetLocalIPv4();
            LastOnlineJoinCode = string.Empty;
        }

        return ok ? NetStartResult.Success : NetStartResult.TransportError;
    }

    public NetStartResult HostLanAutoPort(out ushort chosenPort)
    {
        chosenPort = 0;

        if (transport == null || networkManager == null)
            return NetStartResult.NotInitialized;
        if (networkManager.ShutdownInProgress)
            return NetStartResult.TransportError;

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
        if (networkManager.ShutdownInProgress) return NetStartResult.TransportError;

        LastOnlineJoinCode = string.Empty;
        transport.SetConnectionData(address, port);
        return networkManager.StartClient() ? NetStartResult.Success : NetStartResult.TransportError;
    }

    public async Task<(NetStartResult result, string joinCode)> HostOnlineAsync(int maxPlayers)
    {
        if (networkManager == null || transport == null)
            return (NetStartResult.NotInitialized, string.Empty);

        if (maxPlayers < 2)
            return (NetStartResult.InvalidInput, string.Empty);

        await EnsureNetworkStoppedAsync();

        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception ex) when (IsServicesException(ex))
        {
            Debug.LogWarning($"[NetSession] HostOnline services unavailable: {ex.Message}");
            return (NetStartResult.ServicesUnavailable, string.Empty);
        }

        try
        {
            int maxConnections = Mathf.Max(1, maxPlayers - 1); // host is not counted in Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            ApplyRelayTransportConfig(allocation.ToRelayServerData(RelayConnectionType));
            if (!networkManager.StartHost())
                return (NetStartResult.TransportError, string.Empty);

            LastHostIp = string.Empty;
            LastHostPort = 0;
            LastOnlineJoinCode = joinCode;
            return (NetStartResult.Success, joinCode);
        }
        catch (RelayServiceException ex) when (IsInvalidRelayInput(ex))
        {
            Debug.LogWarning($"[NetSession] HostOnline invalid request: {ex.Message}");
            return (NetStartResult.InvalidInput, string.Empty);
        }
        catch (RelayServiceException ex)
        {
            Debug.LogWarning($"[NetSession] HostOnline relay failure ({ex.Reason}): {ex.Message}");
            return (NetStartResult.ServicesUnavailable, string.Empty);
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[NetSession] HostOnline invalid input: {ex.Message}");
            return (NetStartResult.InvalidInput, string.Empty);
        }
        catch (Exception ex) when (IsServicesException(ex))
        {
            Debug.LogWarning($"[NetSession] HostOnline relay failure: {ex.Message}");
            return (NetStartResult.ServicesUnavailable, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetSession] HostOnline unknown error: {ex}");
            return (NetStartResult.UnknownError, string.Empty);
        }
    }

    public async Task<NetStartResult> JoinOnlineAsync(string joinCode)
    {
        if (networkManager == null || transport == null)
            return NetStartResult.NotInitialized;

        if (string.IsNullOrWhiteSpace(joinCode))
            return NetStartResult.InvalidInput;

        LastOnlineJoinCode = string.Empty;
        await EnsureNetworkStoppedAsync();

        try
        {
            await EnsureServicesReadyAsync();
        }
        catch (Exception ex) when (IsServicesException(ex))
        {
            Debug.LogWarning($"[NetSession] JoinOnline services unavailable: {ex.Message}");
            return NetStartResult.ServicesUnavailable;
        }

        try
        {
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpperInvariant());
            ApplyRelayTransportConfig(allocation.ToRelayServerData(RelayConnectionType));
            return networkManager.StartClient() ? NetStartResult.Success : NetStartResult.TransportError;
        }
        catch (RelayServiceException ex) when (IsInvalidRelayInput(ex))
        {
            Debug.LogWarning($"[NetSession] JoinOnline invalid code ({ex.Reason}): {ex.Message}");
            return NetStartResult.InvalidInput;
        }
        catch (RelayServiceException ex)
        {
            Debug.LogWarning($"[NetSession] JoinOnline relay failure ({ex.Reason}): {ex.Message}");
            return NetStartResult.ServicesUnavailable;
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[NetSession] JoinOnline invalid code: {ex.Message}");
            return NetStartResult.InvalidInput;
        }
        catch (Exception ex) when (IsServicesException(ex))
        {
            Debug.LogWarning($"[NetSession] JoinOnline relay failure: {ex.Message}");
            return NetStartResult.ServicesUnavailable;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetSession] JoinOnline unknown error: {ex}");
            return NetStartResult.UnknownError;
        }
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
        LastHostIp = string.Empty;
        LastHostPort = 0;
        LastOnlineJoinCode = string.Empty;
        networkManager.Shutdown();
    }

    private async Task EnsureNetworkStoppedAsync()
    {
        if (networkManager == null) return;
        if (!networkManager.IsListening && !networkManager.IsClient && !networkManager.IsServer && !networkManager.ShutdownInProgress)
            return;

        networkManager.Shutdown();

        int remainingFrames = 120;
        while (networkManager.ShutdownInProgress && remainingFrames-- > 0)
            await Task.Yield();
    }

    private void ApplyRelayTransportConfig(RelayServerData relayServerData)
    {
        if (transport == null || networkManager == null) return;
        transport.UseWebSockets = RelayConnectionType.StartsWith("ws");
        transport.SetRelayServerData(relayServerData);
        networkManager.NetworkConfig.NetworkTransport = transport;
    }

    private static async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private static bool IsServicesException(Exception ex)
    {
        return ex is RequestFailedException
               || ex is ServicesInitializationException
               || ex is AuthenticationException
               || ex is RelayServiceException;
    }

    private static bool IsInvalidRelayInput(RelayServiceException ex)
    {
        return ex.Reason == RelayExceptionReason.InvalidRequest
               || ex.Reason == RelayExceptionReason.InvalidArgument
               || ex.Reason == RelayExceptionReason.JoinCodeNotFound
               || ex.Reason == RelayExceptionReason.AllocationNotFound;
    }
}