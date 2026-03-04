using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PressurePlate : NetworkBehaviour
{
    [Header("Identity")]
    [SerializeField] private int plateID = 0;
    [SerializeField] private string playerTag = "Player";

    [Header("Visuals")]
    [SerializeField] private MeshRenderer plateRenderer;
    [SerializeField] private Light plateLight;

    [Tooltip("Plate colour when powered OFF (navy/black).")]
    [SerializeField] private Color poweredOffColor = new Color(0.05f, 0.06f, 0.09f);

    [Tooltip("Plate colour when powered ON but not active (still navy/black).")]
    [SerializeField] private Color poweredOnColor = new Color(0.05f, 0.06f, 0.09f);

    [Tooltip("Plate colour when active (green).")]
    [SerializeField] private Color activeColor = new Color(0.1f, 0.9f, 0.2f);

    [Header("Light colours")]
    [SerializeField] private Color lightPoweredColor = new Color(0.1f, 0.5f, 1.0f); // blue glow
    [SerializeField] private Color lightActiveColor  = new Color(0.1f, 0.9f, 0.2f);  // green glow

    [Header("Light intensity")]
    [SerializeField] private float lightOffIntensity = 0f;
    [SerializeField] private float lightOnIntensity  = 5f;

    // Networked state
    private NetworkVariable<bool> isPowered = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isActive = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isLatched = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // IMPORTANT: Netcode expects this exact delegate type
    private NetworkVariable<bool>.OnValueChangedDelegate _onAnyNetBoolChanged;

    // Server-only tracking of who is standing on it
    private readonly HashSet<Collider> playersOnPlate = new();

    // Controller hook
    private SecurityRoomController controller;

    public int PlateID => plateID;

    // Properties the controller expects
    public bool IsPowered => isPowered.Value;
    public bool IsActive => isActive.Value;

    private void Awake()
    {
        if (plateRenderer == null)
            plateRenderer = GetComponentInChildren<MeshRenderer>(true);

        // Ensure trigger collider
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Prevent “flash” in editor before network vars apply
        UpdateVisuals();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _onAnyNetBoolChanged ??= OnAnyNetBoolChanged;

        isPowered.OnValueChanged += _onAnyNetBoolChanged;
        isActive.OnValueChanged  += _onAnyNetBoolChanged;
        isLatched.OnValueChanged += _onAnyNetBoolChanged;

        UpdateVisuals();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (_onAnyNetBoolChanged != null)
        {
            isPowered.OnValueChanged -= _onAnyNetBoolChanged;
            isActive.OnValueChanged  -= _onAnyNetBoolChanged;
            isLatched.OnValueChanged -= _onAnyNetBoolChanged;
        }
    }

    private void OnAnyNetBoolChanged(bool previousValue, bool newValue)
    {
        UpdateVisuals();
    }

    // Called by SecurityRoomController
    public void RegisterController(SecurityRoomController c)
    {
        controller = c;
    }

    // Called by SecurityRoomController
    public void ServerSetPowered(bool on)
    {
        if (!IsServer) return;

        if (isPowered.Value == on) return;
        isPowered.Value = on;

        if (!on)
        {
            // Fully shut down
            playersOnPlate.Clear();
            isLatched.Value = false;
            if (isActive.Value)
                isActive.Value = false;
        }

        controller?.ServerOnPlateChanged();
    }

    // Called by SecurityRoomController
    public void ServerSetLatched(bool latched)
    {
        if (!IsServer) return;

        if (isLatched.Value == latched) return;
        isLatched.Value = latched;

        // If we just unlatched and nobody is standing on it, drop back to inactive
        if (!latched && playersOnPlate.Count == 0 && isActive.Value)
        {
            isActive.Value = false;
            controller?.ServerOnPlateChanged();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isPowered.Value) return;
        if (!IsPlayerCollider(other)) return;

        playersOnPlate.Add(other);

        // First player stepping on activates it
        if (!isActive.Value)
        {
            isActive.Value = true;
            controller?.ServerOnPlateChanged();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!isPowered.Value) return;
        if (!IsPlayerCollider(other)) return;

        playersOnPlate.Remove(other);

        // If latched, stay active even if people leave
        if (isLatched.Value) return;

        // If nobody left on plate, deactivate
        if (playersOnPlate.Count == 0 && isActive.Value)
        {
            isActive.Value = false;
            controller?.ServerOnPlateChanged();
        }
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;

        if (col.CompareTag(playerTag)) return true;
        if (col.transform.parent != null && col.transform.parent.CompareTag(playerTag)) return true;

        Transform root = col.transform.root;
        return root != null && root.CompareTag(playerTag);
    }

    private void UpdateVisuals()
    {
        // Plate colour: navy unless active green
        if (plateRenderer != null)
        {
            Color c;

            if (!isPowered.Value) c = poweredOffColor;
            else if (isActive.Value) c = activeColor;
            else c = poweredOnColor;

            // Note: this instantiates a per-renderer material at runtime; fine for a few plates.
            if (plateRenderer.material != null)
                plateRenderer.material.color = c;
        }

        // Light behaviour:
        // - OFF when not powered
        // - Blue when powered
        // - Green when active
        if (plateLight != null)
        {
            if (!isPowered.Value)
            {
                plateLight.enabled = false;
                plateLight.intensity = lightOffIntensity;
            }
            else
            {
                plateLight.enabled = true;
                plateLight.intensity = lightOnIntensity;
                plateLight.color = isActive.Value ? lightActiveColor : lightPoweredColor;
            }
        }
    }
}