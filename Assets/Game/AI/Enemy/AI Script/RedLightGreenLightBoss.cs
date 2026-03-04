using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Red Light Green Light boss controller.
/// The boss does not move or rotate — it has eyes (point lights) and room
/// lights that change colour via BossRoomLightController.
///
/// SETUP:
/// 1. Attach to the boss NPC prefab (requires NetworkObject).
/// 2. Assign BossRoomLightController in the inspector.
/// 3. Place a BossRoomActivationTrigger in the boss room doorway and assign
///    this boss as its target. Nothing starts until players enter the room.
/// 4. Optionally place a RedLightFinishLine at the end of the room.
/// </summary>
public class RedLightGreenLightBoss : NetworkBehaviour
{
    // ── Enums ────────────────────────────────────────────────────────────────

    public enum BossPhase
    {
        Idle,       // waiting — game has not started
        Countdown,  // brief delay before first green light
        GreenLight, // players may move
        Turning,    // warning window before red light
        RedLight,   // players must not move
        GameOver
    }

    // ── Network state ────────────────────────────────────────────────────────

    public NetworkVariable<BossPhase> Phase = new NetworkVariable<BossPhase>(
        BossPhase.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private BossRoomLightController lightController;

    [Header("Phase Durations")]
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float minGreenDuration  = 3f;
    [SerializeField] private float maxGreenDuration  = 6f;
    [SerializeField] private float turningDuration   = 1.2f;
    [SerializeField] private float minRedDuration    = 2f;
    [SerializeField] private float maxRedDuration    = 5f;

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold   = 0.18f;
    [Tooltip("Seconds after Red Light begins before checks start. Covers network latency.")]
    [SerializeField] private float redLightGracePeriod = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioClip greenLightClip;
    [SerializeField] private AudioClip redLightClip;
    [SerializeField] private AudioClip turningClip;
    [SerializeField] private AudioClip countdownClip;
    [SerializeField] private AudioClip eliminatedClip;

    // ── Private — server only ────────────────────────────────────────────────

    private float _phaseTimer;
    private float _graceTimer;
    private bool  _graceOver;
    private readonly Dictionary<ulong, Vector3> _snapshots = new();

    // ── Private — all clients ────────────────────────────────────────────────

    private AudioSource _audio;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();

        _audio.spatialBlend = 1f;
        _audio.playOnAwake  = false;
    }

    public override void OnNetworkSpawn()
    {
        Phase.OnValueChanged += OnPhaseChanged;
        ApplyLightsClientRpc(Phase.Value);
    }

    public override void OnNetworkDespawn()
    {
        Phase.OnValueChanged -= OnPhaseChanged;
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (Phase.Value)
        {
            case BossPhase.Countdown:
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f) ServerSetPhase(BossPhase.GreenLight);
                break;

            case BossPhase.GreenLight:
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f) ServerSetPhase(BossPhase.Turning);
                break;

            case BossPhase.Turning:
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f) ServerSetPhase(BossPhase.RedLight);
                break;

            case BossPhase.RedLight:
                if (!_graceOver)
                {
                    _graceTimer -= Time.deltaTime;
                    if (_graceTimer <= 0f) _graceOver = true;
                }
                else
                {
                    ServerCheckMovement();
                }

                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f) ServerSetPhase(BossPhase.GreenLight);
                break;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Called by BossRoomActivationTrigger when players enter the room.</summary>
    public void Activate()
    {
        if (!IsServer) return;
        if (Phase.Value != BossPhase.Idle) return;
        Debug.Log("[RLGL Boss] Room activated — starting countdown.");
        ServerSetPhase(BossPhase.Countdown);
    }

    /// <summary>Call (server only) when a player crosses the finish line.</summary>
    public void NotifyPlayerWon()
    {
        if (!IsServer) return;
        Debug.Log("[RLGL Boss] A player won!");
        ServerSetPhase(BossPhase.GameOver);
    }

    // ── Server: phase transitions ────────────────────────────────────────────

    private void ServerSetPhase(BossPhase next)
    {
        switch (next)
        {
            case BossPhase.Countdown:
                _phaseTimer = countdownDuration;
                break;

            case BossPhase.GreenLight:
                _phaseTimer = Random.Range(minGreenDuration, maxGreenDuration);
                _snapshots.Clear();
                break;

            case BossPhase.Turning:
                _phaseTimer = turningDuration;
                break;

            case BossPhase.RedLight:
                _phaseTimer = Random.Range(minRedDuration, maxRedDuration);
                _graceTimer = redLightGracePeriod;
                _graceOver  = false;
                ServerTakeSnapshots();
                break;
        }

        Phase.Value = next;
    }

    // ── Server: movement detection ───────────────────────────────────────────

    private void ServerTakeSnapshots()
    {
        _snapshots.Clear();

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead.Value) continue;

            _snapshots[client.ClientId] = obj.transform.position;
        }
    }

    private void ServerCheckMovement()
    {
        var copy = new Dictionary<ulong, Vector3>(_snapshots);

        foreach (var kvp in copy)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(kvp.Key, out var clientData)) continue;

            var obj = clientData.PlayerObject;
            if (obj == null) continue;

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead.Value) continue;

            if (Vector3.Distance(obj.transform.position, kvp.Value) > movementThreshold)
                ServerEliminatePlayer(kvp.Key, obj);
        }
    }

    private void ServerEliminatePlayer(ulong clientId, NetworkObject playerObj)
    {
        _snapshots.Remove(clientId);

        var health = playerObj.GetComponent<PlayerHealth>();
        if (health != null)
            health.TakeDamage(health.MaxHealth);

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        OnEliminatedClientRpc(rpcParams);
        PlaySoundClientRpc(SoundId.Eliminated);

        Debug.Log($"[RLGL Boss] Client {clientId} eliminated for moving on Red Light.");
    }

    // ── Client: react to phase changes ───────────────────────────────────────

    private void OnPhaseChanged(BossPhase prev, BossPhase next)
    {
        // Audio plays locally from the NetworkVariable callback on all clients
        switch (next)
        {
            case BossPhase.Countdown:  PlaySound(countdownClip);  break;
            case BossPhase.GreenLight: PlaySound(greenLightClip); break;
            case BossPhase.Turning:    PlaySound(turningClip);    break;
            case BossPhase.RedLight:   PlaySound(redLightClip);   break;
        }

        // Light changes sent via ClientRpc so every client updates its local lights
        if (IsServer) ApplyLightsClientRpc(next);
    }

    [ClientRpc]
    private void ApplyLightsClientRpc(BossPhase phase)
    {
        switch (phase)
        {
            case BossPhase.Idle:
            case BossPhase.GameOver:
                lightController?.SetIdle();
                break;
            case BossPhase.Countdown:
                lightController?.SetIdle();
                break;
            case BossPhase.GreenLight:
                lightController?.SetGreen();
                break;
            case BossPhase.Turning:
                lightController?.SetTurning();
                break;
            case BossPhase.RedLight:
                lightController?.SetRed();
                break;
        }
    }

    // ── RPCs ─────────────────────────────────────────────────────────────────

    private enum SoundId { GreenLight, RedLight, Turning, Countdown, Eliminated }

    [ClientRpc]
    private void PlaySoundClientRpc(SoundId id)
    {
        AudioClip clip = id switch
        {
            SoundId.GreenLight => greenLightClip,
            SoundId.RedLight   => redLightClip,
            SoundId.Turning    => turningClip,
            SoundId.Countdown  => countdownClip,
            SoundId.Eliminated => eliminatedClip,
            _                  => null
        };
        PlaySound(clip);
    }

    [ClientRpc]
    private void OnEliminatedClientRpc(ClientRpcParams _ = default)
    {
        Debug.Log("[RLGL] You moved on Red Light — eliminated!");
        // Hook your UI here: PlayerHealthUI.ShowEliminated();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audio == null) return;
        _audio.Stop();
        _audio.PlayOneShot(clip);
    }
}