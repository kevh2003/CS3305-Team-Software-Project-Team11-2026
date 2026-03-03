using Unity.Netcode;
using UnityEngine;

public class ObjectiveState : NetworkBehaviour
{
    public static ObjectiveState Instance { get; private set; }

    [SerializeField] private int ducksTotal = 12;
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
    public void RegisterDuckServerRpc()
    {
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
}