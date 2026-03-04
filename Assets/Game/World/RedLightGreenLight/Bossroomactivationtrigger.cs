using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Place this trigger collider in the doorway / entrance to the boss room.
/// It tells the boss to Activate() once the required number of players have entered.
///
/// This is a plain MonoBehaviour (not a NetworkBehaviour). Trigger detection runs
/// on the server because the server owns all NetworkPlayer positions via the
/// CharacterController. If you ever switch to client-authoritative movement,
/// replace OnTriggerEnter with a ServerRpc.
///
/// SETUP:
/// 1. Create an empty GameObject in the boss room entrance.
/// 2. Add a trigger Collider (BoxCollider with IsTrigger = true).
/// 3. Attach this script and assign the boss reference.
/// 4. Set activationMode to FirstPlayer or AllPlayers to taste.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BossRoomActivationTrigger : MonoBehaviour
{
    public enum ActivationMode
    {
        FirstPlayer,  // boss activates the moment any player enters
        AllPlayers    // boss waits until every living player has entered
    }

    [Header("References")]
    [SerializeField] private RedLightGreenLightBoss boss;

    [Header("Settings")]
    [SerializeField] private ActivationMode activationMode = ActivationMode.FirstPlayer;
    [Tooltip("Only used when mode is AllPlayers. How long to wait after the first player enters " +
             "before giving up and activating anyway (0 = never time out).")]
    [SerializeField] private float allPlayersTimeoutSeconds = 10f;

    // ── Private ───────────────────────────────────────────────────────────────

    private bool  _activated       = false;
    private int   _playersInside   = 0;
    private float _timeoutTimer    = 0f;
    private bool  _timeoutRunning  = false;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        // Only the server drives activation
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_activated) return;

        if (_timeoutRunning && allPlayersTimeoutSeconds > 0f)
        {
            _timeoutTimer -= Time.deltaTime;
            if (_timeoutTimer <= 0f)
            {
                Debug.Log("[BossRoomTrigger] Timeout reached — activating boss without all players.");
                TriggerActivation();
            }
        }
    }

    // OnTriggerEnter runs on the server (server owns CharacterController movement)
    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_activated) return;

        var player = other.GetComponent<NetworkPlayer>();
        if (player == null) return;

        // Ignore dead players — they can't participate anyway
        var health = other.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead.Value) return;

        _playersInside++;
        Debug.Log($"[BossRoomTrigger] Player {player.OwnerClientId} entered boss room. " +
                  $"Inside: {_playersInside}");

        if (activationMode == ActivationMode.FirstPlayer)
        {
            TriggerActivation();
            return;
        }

        // AllPlayers mode
        int alivePlayers = CountAlivePlayers();

        if (_playersInside >= alivePlayers)
        {
            TriggerActivation();
        }
        else if (!_timeoutRunning && allPlayersTimeoutSeconds > 0f)
        {
            // Start the timeout once the first player enters
            _timeoutRunning = true;
            _timeoutTimer   = allPlayersTimeoutSeconds;
            Debug.Log($"[BossRoomTrigger] Waiting for remaining players. " +
                      $"Timeout in {allPlayersTimeoutSeconds}s.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_activated) return;

        var player = other.GetComponent<NetworkPlayer>();
        if (player == null) return;

        _playersInside = Mathf.Max(0, _playersInside - 1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerActivation()
    {
        _activated      = true;
        _timeoutRunning = false;

        Debug.Log("[BossRoomTrigger] Activating boss!");
        boss?.Activate();
    }

    private int CountAlivePlayers()
    {
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        int alive   = 0;

        foreach (var p in players)
        {
            if (!p.IsSpawned) continue;
            var health = p.GetComponent<PlayerHealth>();
            if (health == null || !health.IsDead.Value)
                alive++;
        }

        return Mathf.Max(1, alive);
    }
}