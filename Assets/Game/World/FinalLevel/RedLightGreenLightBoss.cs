using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Red Light Green Light boss controller.
/// The boss does not move or rotate -- it has eyes (point lights) and room
/// lights that change colour via BossRoomLightController.
/// </summary>
public class RedLightGreenLightBoss : NetworkBehaviour
{
    public enum BossPhase
    {
        Idle,       // waiting -- game has not started
        Countdown,  // brief delay before first green light
        GreenLight, // players may move
        Turning,    // warning window before red light
        RedLight,   // players must not move
        GameOver
    }

    public NetworkVariable<BossPhase> Phase = new NetworkVariable<BossPhase>(
        BossPhase.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("References")]
    [SerializeField] private BossRoomLightController lightController;
    [Tooltip("Only players inside this collider can be eliminated during Red Light. " +
             "If unassigned, all players are considered in-bounds.")]
    [SerializeField] private Collider redLightContainmentCollider;

    [Header("Phase Durations")]
    [SerializeField] private float countdownDuration = 0.25f;
    [SerializeField] private float minGreenDuration  = 3f;
    [SerializeField] private float maxGreenDuration  = 6f;
    [SerializeField] private float turningDuration   = 0.67f;
    [SerializeField] private float minRedDuration    = 2f;
    [SerializeField] private float maxRedDuration    = 5f;

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold   = 0.18f;
    [Tooltip("Seconds after Red Light begins before checks start. Covers network latency.")]
    [SerializeField] private float redLightGracePeriod = 0.25f;
    [Tooltip("How long movement must remain above threshold before elimination.")]
    [SerializeField] private float redLightViolationSeconds = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip greenLightClip;
    [SerializeField] private AudioClip redLightClip;
    [SerializeField] private AudioClip turningClip;
    [SerializeField] private AudioClip countdownClip;
    [SerializeField] private AudioClip eliminatedClip;

    private float _phaseTimer;
    private float _graceTimer;
    private bool  _graceOver;
    private readonly Dictionary<ulong, Vector3> _snapshots = new();
    private readonly Dictionary<ulong, float> _violationTimers = new();
    private readonly List<ulong> _pendingEliminations = new();
    private readonly List<ulong> _staleTrackedIds = new();

    private AudioSource _audio;

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

        if (IsServer)
        {
            _snapshots.Clear();
            _violationTimers.Clear();
            Phase.Value = BossPhase.Idle;
            ApplyLightsClientRpc(Phase.Value);
        }
        else
        {
            ApplyLightsLocal(Phase.Value);
        }
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

    /// <summary>Called by BossRoomActivationTrigger when players enter the room.</summary>
    public void Activate()
    {
        if (!IsServer) return;
        if (Phase.Value != BossPhase.Idle) return;
        Debug.Log("[RLGL Boss] Room activated -- starting immediately.");
        ServerSetPhase(BossPhase.GreenLight);
    }

    /// <summary>Call (server only) when a player crosses the finish line.</summary>
    public void NotifyPlayerWon()
    {
        if (!IsServer) return;
        Debug.Log("[RLGL Boss] A player won!");
        ServerSetPhase(BossPhase.GameOver);
    }

    private void ServerSetPhase(BossPhase next)
    {
        if (next != BossPhase.RedLight)
        {
            _snapshots.Clear();
            _violationTimers.Clear();
        }

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

    private void ServerTakeSnapshots()
    {
        _snapshots.Clear();
        _violationTimers.Clear();

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead.Value) continue;
            if (!IsInsideRedLightContainment(obj.transform.position)) continue;

            var p = obj.transform.position;
            p.y = 0f; // ignore tiny vertical jitter from network/controller
            _snapshots[client.ClientId] = p;
            _violationTimers[client.ClientId] = 0f;
        }
    }

    private void ServerCheckMovement()
    {
        _pendingEliminations.Clear();
        _staleTrackedIds.Clear();

        // Track/validate all connected players currently inside containment.
        foreach (var clientData in NetworkManager.ConnectedClientsList)
        {
            ulong clientId = clientData.ClientId;

            var obj = clientData.PlayerObject;
            if (obj == null)
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead.Value)
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            if (!IsInsideRedLightContainment(obj.transform.position))
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            var current = obj.transform.position;
            current.y = 0f;

            if (!_snapshots.ContainsKey(clientId))
            {
                _snapshots[clientId] = current;
                _violationTimers[clientId] = 0f;
                continue;
            }

            float moved = Vector3.Distance(current, _snapshots[clientId]);
            if (moved > movementThreshold)
            {
                if (!_violationTimers.ContainsKey(clientId))
                    _violationTimers[clientId] = 0f;

                _violationTimers[clientId] += Time.deltaTime;
                if (_violationTimers[clientId] >= redLightViolationSeconds)
                    _pendingEliminations.Add(clientId);
            }
            else
            {
                _violationTimers[clientId] = 0f;
            }
        }

        // Remove tracked IDs that no longer exist in ConnectedClients.
        foreach (var kvp in _snapshots)
        {
            if (!NetworkManager.ConnectedClients.ContainsKey(kvp.Key))
                _staleTrackedIds.Add(kvp.Key);
        }

        for (int i = 0; i < _staleTrackedIds.Count; i++)
        {
            ulong staleId = _staleTrackedIds[i];
            _snapshots.Remove(staleId);
            _violationTimers.Remove(staleId);
        }

        for (int i = 0; i < _pendingEliminations.Count; i++)
        {
            ulong clientId = _pendingEliminations[i];

            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var clientData))
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            var obj = clientData.PlayerObject;
            if (obj == null)
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            var health = obj.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead.Value)
            {
                _snapshots.Remove(clientId);
                _violationTimers.Remove(clientId);
                continue;
            }

            ServerEliminatePlayer(clientId, obj);
        }
    }

    private void ServerEliminatePlayer(ulong clientId, NetworkObject playerObj)
    {
        _snapshots.Remove(clientId);
        _violationTimers.Remove(clientId);

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
        ApplyLightsLocal(phase);
    }

    private void ApplyLightsLocal(BossPhase phase)
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
        Debug.Log("[RLGL] You moved on Red Light -- eliminated!");
    }

    private bool IsInsideRedLightContainment(Vector3 worldPosition)
    {
        if (redLightContainmentCollider == null)
            return true;

        Vector3 closest = redLightContainmentCollider.ClosestPoint(worldPosition);
        return (closest - worldPosition).sqrMagnitude <= 0.0001f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audio == null) return;
        _audio.Stop();
        _audio.PlayOneShot(clip);
    }
}