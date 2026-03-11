using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class ObjectiveState : NetworkBehaviour
{
    public static ObjectiveState Instance { get; private set; }

    [SerializeField] private int ducksTotal = 8;
    public int DucksTotal => ducksTotal;

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

    // Optional future-proofing: extra “pre-key” tasks we may add later
    // If you add new tasks, set PreKeyExtraRequired to N and increment PreKeyExtraCompleted as each completes
    // see MatchStartResetter.cs for this method. And add the following to your new task script - kev

    //if (!IsServer) return;

    //if (ObjectiveState.Instance != null)
    //{
    //    ObjectiveState.Instance.PreKeyExtraCompleted.Value++;
    //}

    public NetworkVariable<int> PreKeyExtraRequired = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> PreKeyExtraCompleted = new(
        0,
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

    [Header("CCTV Lure")]
    [SerializeField] private LayerMask lureEnemyMask = ~0;
    [SerializeField] private float maxLureRadius = 30f;

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
        bool assignmentComplete = (RequiredSubmitCount.Value > 0 && CurrentSubmitCount.Value >= RequiredSubmitCount.Value);

        bool extrasOk = (PreKeyExtraRequired.Value <= 0) || (PreKeyExtraCompleted.Value >= PreKeyExtraRequired.Value);

        return ducksComplete && assignmentComplete && extrasOk;
    }

    public bool ArePreKeyObjectivesCompleteClient()
    {
        bool ducksComplete = DucksFound.Value >= DucksTotal;
        bool assignmentComplete = (RequiredSubmitCount.Value > 0 && CurrentSubmitCount.Value >= RequiredSubmitCount.Value);
        bool extrasOk = (PreKeyExtraRequired.Value <= 0) || (PreKeyExtraCompleted.Value >= PreKeyExtraRequired.Value);
        return ducksComplete && assignmentComplete && extrasOk;
    }

    public void ServerResetKeyGateForNewRound()
    {
        if (!IsServer) return;
        KeySpawned.Value = false;
        PreKeyExtraCompleted.Value = 0;
    }

    public void ServerResetPostKeyObjectivesForNewRound()
    {
        if (!IsServer) return;

        KeyCollected.Value = false;
        SecurityDoorUnlocked.Value = false;
        ElevatorOpened.Value = false;
        GradesChanged.Value = false;
    }

    public void ServerLureEnemiesAtPoint(Vector3 point, float radius)
    {
        if (!IsServer) return;

        float safeRadius = Mathf.Clamp(radius, 0.5f, maxLureRadius);
        Collider[] hits = Physics.OverlapSphere(point, safeRadius, lureEnemyMask, QueryTriggerInteraction.Ignore);
        var seen = new HashSet<EnemyAI>();

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            var enemy = c.GetComponentInParent<EnemyAI>();
            if (enemy == null) continue;
            if (!seen.Add(enemy)) continue;

            enemy.Lure(point);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLureEnemiesServerRpc(Vector3 point, float radius, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!MatchRosterLocked.Value) return; // only allow lure requests during active rounds

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var sender)) return;
        if (sender.PlayerObject == null) return;

        var ph = sender.PlayerObject.GetComponent<PlayerHealth>();
        if (ph != null && ph.IsDead.Value) return;

        ServerLureEnemiesAtPoint(point, radius);
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
}