using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Attach to any classroom object that can be knocked over by pressing E.
/// The object will instantly teleport to a "fallen" position and rotation,
/// synced across all clients via Netcode for GameObjects.
///
/// Setup per object:
///  - Set FallenPosition to where the object should land (e.g. directly below it on the floor).
///  - Set FallenRotation to how it should look lying down (e.g. 90 on X for a wall clock).
///  - Optionally override PromptText for your UI system.
/// </summary>
/// 
public class KnockableObject : NetworkBehaviour, IInteractable
{
    [Header("Fallen Transform")]
    [Tooltip("World position the object teleports to when knocked. " +
             "Use the Scene view to find a good floor position.")]
    public Vector3 FallenPosition;

    [Tooltip("World rotation (Euler angles) the object takes when knocked. " +
             "e.g. (90, 0, 0) lays something flat that was standing upright.")]
    public Vector3 FallenRotation;

    [Header("Interaction")]
    [Tooltip("Text shown in your interaction prompt UI, if you have one.")]
    public string PromptText = "Knock over";

    [Tooltip("If true, players can no longer interact once the object is already knocked.")]
    public bool SingleUse = true;

    // ── Networked State ────────────────────────────────────────────
    // NetworkVariable ensures late-joining clients also see the correct state.
    private NetworkVariable<bool> _isKnocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Cached original transform so we could restore it later if needed
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;

    // ── Unity Lifecycle ────────────────────────────────────────────

    void Awake()
    {
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;

        // Default FallenPosition to directly below the object if not set in Inspector
        if (FallenPosition == Vector3.zero)
            FallenPosition = new Vector3(
                transform.position.x,
                0f,                       // assumes your floor is at Y = 0
                transform.position.z
            );
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe so all clients react when the server flips the flag
        _isKnocked.OnValueChanged += OnKnockedStateChanged;

        // Apply immediately in case this client joined after it was already knocked
        if (_isKnocked.Value)
            ApplyFallenTransform();
    }

    public override void OnNetworkDespawn()
    {
        _isKnocked.OnValueChanged -= OnKnockedStateChanged;
        base.OnNetworkDespawn();
    }

    // ── IInteractable ──────────────────────────────────────────────

    public bool CanInteract()
    {
        // Block interaction if already knocked and this is a single-use object
        if (SingleUse && _isKnocked.Value) return false;
        return true;
    }

    public bool Interact(Interactor interactor)
    {
        if (!CanInteract()) return false;

        // Any client can call this — the server will validate and broadcast
        RequestKnockServerRpc();
        return true;
    }

    // ── Netcode RPCs ───────────────────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void RequestKnockServerRpc()
    {
        // Server-side validation: reject if already knocked
        if (SingleUse && _isKnocked.Value) return;

        // Setting the NetworkVariable automatically replicates to all clients
        _isKnocked.Value = true;

        Debug.Log($"✅ Server: {gameObject.name} knocked over.");
    }

    // ── State Change Handler (runs on ALL clients) ─────────────────

    private void OnKnockedStateChanged(bool previousValue, bool newValue)
    {
        if (newValue)
            ApplyFallenTransform();
    }

    // ── Transform Logic ────────────────────────────────────────────

    private void ApplyFallenTransform()
    {
        transform.localPosition = FallenPosition;
        transform.localRotation = Quaternion.Euler(FallenRotation);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

}