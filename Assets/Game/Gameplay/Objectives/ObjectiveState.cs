using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectiveState : NetworkBehaviour
{
    public static ObjectiveState Instance { get; private set; }

    [SerializeField] private int ducksTotal = 12;
    [SerializeField] private int wifiTotal = 4;

    public int DucksTotal => ducksTotal;
    public int WifiTotal => Mathf.Max(0, wifiTotal);

    public NetworkVariable<int> DucksFound = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> RequiredSubmitCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> CurrentSubmitCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> WifiFixedCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> MatchRosterLocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Pre-key gate state
    public NetworkVariable<bool> KeySpawned = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Post-key objective state
    public NetworkVariable<bool> KeyCollected = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> SecurityDoorUnlocked = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> ElevatorOpened = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> GradesChanged = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // MUST be initialized at declaration for Netcode
    private NetworkList<ulong> requiredSubmitters = new();
    private NetworkList<ulong> submitted = new();

    private void Awake()
    {
        // Scene singleton (do NOT DontDestroyOnLoad for scene NetworkObjects)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            WarnIfWifiNodesMissing();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // client helpers
    public bool HasSubmittedClient(ulong clientId)
    {
        return submitted != null && submitted.Contains(clientId);
    }

    // server : ducks
    public void ServerResetDucksForNewRound()
    {
        if (!IsServer) return;
        DucksFound.Value = 0;
    }

    public void ServerRegisterDuck()
    {
        if (!IsServer) return;
        if (DucksFound.Value >= ducksTotal) return;
        DucksFound.Value++;
    }

    public void ServerResetWifiForNewRound()
    {
        if (!IsServer) return;
        WifiFixedCount.Value = 0;
    }

    public void ServerRegisterWifiFix()
    {
        if (!IsServer) return;

        int total = WifiTotal;
        if (total <= 0) return;
        if (WifiFixedCount.Value >= total) return;

        WifiFixedCount.Value++;
    }

    public bool IsWifiObjectiveCompleteClient()
    {
        int total = WifiTotal;
        return total <= 0 || WifiFixedCount.Value >= total;
    }

    public bool IsWifiObjectiveCompleteServer()
    {
        if (!IsServer) return false;

        int total = WifiTotal;
        return total <= 0 || WifiFixedCount.Value >= total;
    }

    // Keeping rpc version for now - kev
    [ServerRpc(RequireOwnership = false)]
    public void RegisterDuckServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!MatchRosterLocked.Value) return;
        if (NetworkManager.Singleton == null) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var sender)) return;
        if (sender.PlayerObject == null) return;

        var ph = sender.PlayerObject.GetComponent<PlayerHealth>();
        if (ph != null && ph.IsDead.Value) return;

        ServerRegisterDuck();
    }

    // server : assignment roster
    public void ServerBeginRoundRoster()
    {
        if (!IsServer) return;

        requiredSubmitters.Clear();
        submitted.Clear();

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
            requiredSubmitters.Add(p.OwnerClientId);

        RequiredSubmitCount.Value = requiredSubmitters.Count;
        CurrentSubmitCount.Value = 0;
        MatchRosterLocked.Value = true;
    }

    public bool ServerHasSubmitted(ulong clientId)
    {
        if (!IsServer) return false;
        return submitted.Contains(clientId);
    }

    public void ServerRegisterAssignmentSubmit(ulong clientId)
    {
        if (!IsServer) return;

        if (MatchRosterLocked.Value && !requiredSubmitters.Contains(clientId))
            return;

        if (submitted.Contains(clientId))
            return;

        submitted.Add(clientId);
        CurrentSubmitCount.Value = submitted.Count;
    }

    public void ServerResetAssignmentForLobby()
    {
        if (!IsServer) return;

        requiredSubmitters.Clear();
        submitted.Clear();

        RequiredSubmitCount.Value = 0;
        CurrentSubmitCount.Value = 0;
        MatchRosterLocked.Value = false;
    }

    // Call when a player dies mid-round so they don't block completion
    public void ServerHandlePlayerDeath(ulong clientId)
    {
        if (!IsServer) return;
        if (!MatchRosterLocked.Value) return;

        if (!requiredSubmitters.Contains(clientId)) return;
        if (submitted.Contains(clientId)) return;

        requiredSubmitters.Remove(clientId);
        RequiredSubmitCount.Value = requiredSubmitters.Count;
    }

    public bool ServerArePreKeyObjectivesComplete()
    {
        if (!IsServer) return false;

        bool ducksComplete = DucksFound.Value >= DucksTotal;
        bool wifiComplete = IsWifiObjectiveCompleteServer();
        bool assignmentComplete = (RequiredSubmitCount.Value > 0 && CurrentSubmitCount.Value >= RequiredSubmitCount.Value);
        return ducksComplete && wifiComplete && assignmentComplete;
    }

    public bool ArePreKeyObjectivesCompleteClient()
    {
        bool ducksComplete = DucksFound.Value >= DucksTotal;
        bool wifiComplete = IsWifiObjectiveCompleteClient();
        bool assignmentComplete = (RequiredSubmitCount.Value > 0 && CurrentSubmitCount.Value >= RequiredSubmitCount.Value);
        return ducksComplete && wifiComplete && assignmentComplete;
    }

    public void ServerResetKeyGateForNewRound()
    {
        if (!IsServer) return;
        KeySpawned.Value = false;
    }

    public void ServerResetPostKeyObjectivesForNewRound()
    {
        if (!IsServer) return;

        KeyCollected.Value = false;
        SecurityDoorUnlocked.Value = false;
        ElevatorOpened.Value = false;
        GradesChanged.Value = false;
    }

    // --- Post-key setters (server authoritative) ---

    public void ServerMarkKeyCollected()
    {
        if (!IsServer) return;
        KeyCollected.Value = true;
    }

    public void ServerMarkSecurityDoorUnlocked()
    {
        if (!IsServer) return;
        SecurityDoorUnlocked.Value = true;
    }

    public void ServerMarkElevatorOpened()
    {
        if (!IsServer) return;
        ElevatorOpened.Value = true;
    }

    public void ServerMarkGradesChanged()
    {
        if (!IsServer) return;
        GradesChanged.Value = true;
    }

    private void WarnIfWifiNodesMissing()
    {
        if (SceneManager.GetActiveScene().name != "03_Game") return;

        int total = WifiTotal;
        if (total <= 0) return;

        int found = FindObjectsByType<StartGame>(FindObjectsSortMode.None).Length;
        if (found < total)
        {
            Debug.LogWarning($"[ObjectiveState] WiFi objective expects {total} nodes, but only found {found} StartGame objects in scene.");
        }
    }
}