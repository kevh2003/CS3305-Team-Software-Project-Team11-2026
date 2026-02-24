using System.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class MatchStartResetter : NetworkBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Wait 1 frame so all player objects exist after the scene load
        StartCoroutine(ResetPlayersNextFrame());
    }

    private IEnumerator ResetPlayersNextFrame()
    {
        yield return new WaitForSeconds(0.1f);

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
            yield break;

        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[i % spawnPoints.Length];
                p.ServerResetForNewMatch(sp.position, sp.rotation);
            }
            else
            {
                Debug.LogWarning("[MatchStartResetter] No spawn points assigned.");
                p.ServerResetForNewMatch(p.transform.position, p.transform.rotation);
            }
        }
    }
}