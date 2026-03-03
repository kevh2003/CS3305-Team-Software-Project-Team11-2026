using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MatchStartResetter : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        // After scene load, place everyone once.
        StartCoroutine(ResetPlayersNextFrame());

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (!IsServer) return;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // If the roster is already locked, exclude late joiners
        if (ObjectiveState.Instance != null && ObjectiveState.Instance.MatchRosterLocked.Value)
        {
            StartCoroutine(KillLateJoinerNextFrame(clientId));
            return;
        }

        // Normal: wait for player object to exist, then reset spawn/health/inventory
        StartCoroutine(ResetSinglePlayerNextFrame(clientId));
    }

    private IEnumerator KillLateJoinerNextFrame(ulong clientId)
    {
        // wait for player object to exist
        for (int i = 0; i < 30; i++)
        {
            if (TryGetPlayer(clientId, out var player) && player != null)
            {
                var health = player.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.CurrentHealth.Value = 0;
                    health.IsDead.Value = true;
                }

                // clear inventory
                var inv = player.GetComponent<PlayerInventory>();
                if (inv != null)
                    inv.ResetInventoryForNewMatchServer();

                yield break;
            }
            yield return null;
        }

        Debug.LogWarning($"[MatchStartResetter] Late joiner {clientId} could not be found to kill/exclude.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        // Drop items before player object disappears
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            var inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null)
            {
                inv.DropAllItemsOnDeathServer();
            }
        }
    }

    private IEnumerator ResetPlayersNextFrame()
    {
        yield return new WaitForSeconds(0.1f);

        ResetAllPlayers();
        ResetAllDoors();
        ResetAllEnemies();

        SetupObjectivesForScene();
    }

    private void SetupObjectivesForScene()
    {
        if (!IsServer) return;

        var obj = ObjectiveState.Instance;
        if (obj == null)
        {
            Debug.LogWarning("[MatchStartResetter] ObjectiveState.Instance is null (object may not be in this scene).");
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "03_Game")
        {
            // new round
            obj.DucksFound.Value = 0; // - reset ducks count
            obj.ServerBeginRoundRoster(); // - lock roster + reset assignment submission tracking
        }
        else
        {
            // lobby scene
            obj.DucksFound.Value = 0; // - reset ducks
            obj.ServerResetAssignmentForLobby(); // - unlock/reset assignment so late joiners can participate next round
        }
    }

    private IEnumerator ResetSinglePlayerNextFrame(ulong clientId)
    {
        for (int i = 0; i < 20; i++)
        {
            if (TryGetPlayer(clientId, out var player))
            {
                ResetPlayer(player);
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning($"[MatchStartResetter] Could not find player for clientId {clientId} to reset spawn.");
    }

    private void ResetAllPlayers()
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
            return;

        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        for (int i = 0; i < players.Length; i++)
            ResetPlayer(players[i], i);
    }

    private void ResetPlayer(NetworkPlayer p, int forcedIndex = -1)
    {
        if (p == null) return;

        int index = forcedIndex;
        if (index < 0)
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == p) { index = i; break; }
            }
            if (index < 0) index = 0;
        }

        Vector3 pos = p.transform.position;
        Quaternion rot = p.transform.rotation;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var sp = spawnPoints[index % spawnPoints.Length];
            pos = sp.position;
            rot = sp.rotation;
        }
        else
        {
            Debug.LogWarning("[MatchStartResetter] No spawn points assigned.");
        }

        // Reset transform
        p.ServerResetForNewMatch(pos, rot);

        // Reset health on the server
        var health = p.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.CurrentHealth.Value = health.MaxHealth;
            health.IsDead.Value = false;
        }

        // Reset inventory on the server + clear UI/hand items on the owning client
        var inv = p.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            inv.ResetInventoryForNewMatchServer();
        }
    }

    private void ResetAllDoors()
    {
        // Any reset door in the scene with HingeDoorInteractable
        var doors = FindObjectsByType<HingeDoorInteractable>(FindObjectsSortMode.None);
        if (doors == null || doors.Length == 0) return;

        foreach (var d in doors)
        {
            if (d != null)
                d.ServerResetToDefaults();
        }
        // Reset all elevator doors (SERVER)
        var elevatorDoors = FindObjectsByType<ElevatorDoorController>(FindObjectsSortMode.None);
        foreach (var d in elevatorDoors)
            d.ServerResetForNewMatch();
    }

    private bool TryGetPlayer(ulong clientId, out NetworkPlayer player)
    {
        player = null;

        if (NetworkManager.Singleton == null) return false;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        player = client.PlayerObject.GetComponent<NetworkPlayer>();
        return player != null;
    }

    private void ResetAllEnemies()
    {
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e != null)
                e.ServerResetForNewMatch();
        }
    }
}