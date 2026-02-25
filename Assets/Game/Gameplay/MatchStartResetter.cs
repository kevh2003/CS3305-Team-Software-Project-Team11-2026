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

            Vector3 pos = p.transform.position;
            Quaternion rot = p.transform.rotation;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[i % spawnPoints.Length];
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
    }
}