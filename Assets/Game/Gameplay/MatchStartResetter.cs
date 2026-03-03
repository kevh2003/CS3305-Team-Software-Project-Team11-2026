using System.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class MatchStartResetter : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // After scene load, place everyone once. Late joiners will also get moved to the nearest spawn point once they spawn.
        StartCoroutine(ResetPlayersNextFrame());

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Wait for player object exist
        StartCoroutine(ResetSinglePlayerNextFrame(clientId));
    }

    private IEnumerator ResetPlayersNextFrame()
    {
        yield return new WaitForSeconds(0.1f);
        ResetAllPlayers();
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
            // stable index based on OwnerClientId ordering among current players
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
}