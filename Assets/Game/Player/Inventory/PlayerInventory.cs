using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

// Networked player inventory with authoritative pickup/drop and item presentation.
public class PlayerInventory : NetworkBehaviour
{
    [Header("Hotbar Settings")]
    public int hotbarSlots = 2;

    [Header("Item Database")]
    public List<ItemDefinition> itemDatabase = new List<ItemDefinition>();

    [Header("Torch Networking")]
    [SerializeField] private int torchItemId = 2;
    [SerializeField] private int keyItemId = 1;
    [SerializeField] private float serverPickupRange = 6f;
    [SerializeField] private bool remoteTorchAnchorToCamera = true;
    [SerializeField] private Vector3 remoteTorchLocalOffset = new Vector3(0.08f, -0.24f, 0.03f);
    [SerializeField] private Vector3 remoteTorchLocalEuler = new Vector3(4f, 0f, 0f);
    [SerializeField] private float remoteTorchIntensity = 12f;
    [SerializeField] private float remoteTorchRange = 22f;
    [SerializeField] private float remoteTorchSpotAngle = 52f;
    [SerializeField] private Color remoteTorchColor = new Color(0.99f, 0.99f, 0.99f, 1f);
    [SerializeField] private bool remoteTorchUseLookPitch = true;
    [SerializeField] private float remoteTorchPitchOffset = 0f;
    [SerializeField] private float remoteTorchPitchLerpSpeed = 20f;

    [HideInInspector] public GameObject hotbarPanel;
    [HideInInspector] public Image[] hotbarSlotImages;
    [HideInInspector] public Transform handPosition;
    [HideInInspector] public Transform dropPosition;

    private PlayerInputActions inputActions;

    private const int EMPTY = -1;

    // Server authoritative inventory state (IDs)
    private int[] itemIds;

    // Local-only hand visuals
    private GameObject[] handItems;
    private int selectedSlot = 0;

    // Fast lookup
    private Dictionary<int, ItemDefinition> byId;

    // Local-only UI prompt state
    private bool _inventoryPromptVisible = false;

    // Networked torch state for remote visual replication
    private readonly NetworkVariable<bool> _torchEquipped = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> _torchOn = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject _remoteTorchLightObject;
    private Light _remoteTorchLight;
    private bool _lastSentTorchEquipped;
    private bool _lastSentTorchOn;
    private NetworkPlayer _networkPlayer;
    private PlayerSoundFX _soundFX;
    private float _remoteTorchSmoothedPitch;
    private Transform _remoteTorchAnchor;

    void Awake()
    {
        itemIds = new int[hotbarSlots];
        for (int i = 0; i < hotbarSlots; i++) itemIds[i] = EMPTY;

        handItems = new GameObject[hotbarSlots];
        inputActions = new PlayerInputActions();
        _networkPlayer = GetComponent<NetworkPlayer>();
        _soundFX = GetComponent<PlayerSoundFX>();

        BuildDatabaseLookup();
    }

    void BuildDatabaseLookup()
    {
        byId = new Dictionary<int, ItemDefinition>();

        foreach (var def in itemDatabase)
        {
            if (def == null) continue;

            if (byId.ContainsKey(def.itemId))
            {
                Debug.LogError($"Duplicate itemId found in itemDatabase: {def.itemId} ({def.name}). Item IDs must be unique.");
                continue;
            }

            byId.Add(def.itemId, def);
        }
    }

    ItemDefinition GetDef(int itemId)
    {
        if (itemId == EMPTY) return null;
        if (byId != null && byId.TryGetValue(itemId, out var def)) return def;

        Debug.LogError($"PlayerInventory: itemId {itemId} not found in itemDatabase.");
        return null;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server stays enabled for authoritative state; only owner reads local input/UI.

        if (IsServer)
            ServerSetTorchState(false, false);

        _torchEquipped.OnValueChanged += OnTorchNetworkStateChanged;
        _torchOn.OnValueChanged += OnTorchNetworkStateChanged;

        EnsureRemoteTorchLight();
        UpdateRemoteTorchVisual();

        // Owner: enable input + bind callbacks
        if (IsOwner)
        {
            // Safety: avoid double-subscribing if something reinitializes
            RemoveInputCallbacks();

            inputActions.Enable();
            SetupInputCallbacks();

            // Ensure prompt matches current hand state
            UpdateInventoryDropPrompt();

            StartCoroutine(OwnerLateInit());
            StartCoroutine(OwnerEnsureAnchors());
            SyncTorchStateIfOwner(force: true);
        }
    }

    public override void OnNetworkDespawn()
    {
        _torchEquipped.OnValueChanged -= OnTorchNetworkStateChanged;
        _torchOn.OnValueChanged -= OnTorchNetworkStateChanged;

        // If 'this' player disconnects, drop their items.
        if (IsServer)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ShutdownInProgress)
            {
                // Only drop if they actually have items
                bool hasAny = false;
                for (int i = 0; i < hotbarSlots; i++)
                {
                    if (itemIds[i] != EMPTY) { hasAny = true; break; }
                }

                if (hasAny)
                    DropAllItemsServer_NoClientUI();
            }

            ServerSetTorchState(false, false);
        }

        if (_remoteTorchLightObject != null)
        {
            Destroy(_remoteTorchLightObject);
            _remoteTorchLightObject = null;
            _remoteTorchLight = null;
        }
        _remoteTorchAnchor = null;

        base.OnNetworkDespawn();
    }

    private System.Collections.IEnumerator OwnerLateInit()
    {
        // Wait a short time for InventoryUI to create/assign HandPosition/DropPosition
        float timeout = 2f;
        while (timeout > 0f && handPosition == null)
        {
            timeout -= UnityEngine.Time.deltaTime;
            yield return null;
        }

        UpdateHandDisplay();
    }

    void SetupInputCallbacks()
    {
        inputActions.Player.HotbarSlot0.performed += OnHotbar0;
        inputActions.Player.HotbarSlot1.performed += OnHotbar1;
        inputActions.Player.DropItem.performed += OnDrop;
    }

    void RemoveInputCallbacks()
    {
        if (inputActions == null) return;

        inputActions.Player.HotbarSlot0.performed -= OnHotbar0;
        inputActions.Player.HotbarSlot1.performed -= OnHotbar1;
        inputActions.Player.DropItem.performed -= OnDrop;
    }

    private void OnHotbar0(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SelectSlot(0);
    private void OnHotbar1(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SelectSlot(1);
    private void OnDrop(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (CameraInteraction.IsAnyLocalCctvActive || CameraInteraction.WasExitedThisFrame ||
            StartGame.IsAnyLocalWifiMinigameActive || StartGame.WasExitedThisFrame)
            return;

        DropSelectedItem();
    }


    public void SetAnchors(Transform hand, Transform drop)
    {
        if (hand != null) handPosition = hand;
        if (drop != null) dropPosition = drop;
    }

    private IEnumerator OwnerEnsureAnchors()
    {
        // Wait a few frames for InventoryUI to create HandPosition/DropPosition
        for (int i = 0; i < 30; i++)
        {
            if (handPosition != null && dropPosition != null)
                yield break;

            // Try to find them if InventoryUI already created them
            if (handPosition == null)
            {
                var hp = transform.Find("HandPosition");
                if (hp == null)
                    hp = FindChildTransformByName("HandPosition");
                handPosition = hp;
            }

            if (dropPosition == null)
            {
                var dp = transform.Find("DropPosition");
                if (dp == null)
                    dp = FindChildTransformByName("DropPosition");
                dropPosition = dp;
            }

            yield return null;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "03_Game")
            {
                if (handPosition == null)
                    Debug.LogError("PlayerInventory (Owner): HandPosition still missing after waiting. Held items may be invisible.");
                if (dropPosition == null)
                    Debug.LogError("PlayerInventory (Owner): DropPosition still missing after waiting. Drops may appear at wrong position.");
            }
    }
    
    void Update()
    {
        if (!IsOwner) return;

        // fallback Q
        if (!CameraInteraction.IsAnyLocalCctvActive &&
            !CameraInteraction.WasExitedThisFrame &&
            !StartGame.IsAnyLocalWifiMinigameActive &&
            !StartGame.WasExitedThisFrame &&
            Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame)
            DropSelectedItem();

        SyncTorchStateIfOwner();
    }

    private void LateUpdate()
    {
        if (IsOwner) return;
        UpdateRemoteTorchPose();
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;
        selectedSlot = index;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
        UpdateInventoryDropPrompt();
        SyncTorchStateIfOwner(force: true);
    }

    void UpdateHotbarOutlines()
    {
        if (hotbarSlotImages == null) return;

        for (int i = 0; i < hotbarSlotImages.Length; i++)
        {
            if (hotbarSlotImages[i] == null) continue;

            Outline outline = hotbarSlotImages[i].GetComponent<Outline>();
            if (outline == null)
            {
                outline = hotbarSlotImages[i].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(139, 0, 0, 255);
                outline.effectDistance = new Vector2(3, 3);
            }
            outline.enabled = (i == selectedSlot);
        }
    }

    private void UpdateInventoryDropPrompt()
    {
        if (!IsOwner) return;

        bool holdingItemInSelectedSlot =
            (selectedSlot >= 0 && selectedSlot < hotbarSlots && itemIds[selectedSlot] != EMPTY);
        bool selectedTorch =
            holdingItemInSelectedSlot && itemIds[selectedSlot] == torchItemId;

        _inventoryPromptVisible = holdingItemInSelectedSlot;

        string promptMessage = selectedTorch
            ? "Press Q to drop\nPress T to toggle torch"
            : "Press Q to drop";

        // Safe: Instance can be null during shutdown
        DropPromptUI.Instance?.SetInventoryVisible(holdingItemInSelectedSlot, promptMessage);
    }

    void UpdateHandDisplay()
    {
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (handItems[i] != null)
                handItems[i].SetActive(false);
        }

        if (handPosition != null && handItems[selectedSlot] != null)
        {
            var handItem = handItems[selectedSlot];
            handItem.SetActive(true);
            handItem.transform.SetParent(handPosition);
            handItem.transform.localPosition = Vector3.zero;
            handItem.transform.localRotation = Quaternion.identity;
            handItem.transform.localScale = Vector3.one;
        }
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < hotbarSlots; i++)
            if (itemIds[i] == EMPTY)
                return i;
        return -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void PickupItemServerRpc(NetworkObjectReference itemRef, int itemId, int preferredSlot, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != OwnerClientId) return;

        if (!itemRef.TryGet(out NetworkObject itemNo)) return;
        if (!itemNo.IsSpawned) return;
        if (!IsSenderInPickupRange(senderClientId, itemNo)) return;

        var worldItem = itemNo.GetComponent<WorldItem>();
        if (worldItem == null || worldItem.definition == null) return;

        int serverItemId = worldItem.definition.itemId;

        // Reject mismatched payloads; item identity is authoritative on the world object.
        if (itemId != serverItemId) return;

        // Validate item exists in database on server
        var def = GetDef(serverItemId);
        if (def == null) return;

        int slot = -1;

        // Try selected slot first
        if (preferredSlot >= 0 && preferredSlot < hotbarSlots && itemIds[preferredSlot] == EMPTY)
            slot = preferredSlot;
        else
            slot = FindFirstEmptySlot();

        if (slot == -1) return; // inventory full

        // record on server
        itemIds[slot] = serverItemId;

        // Remove the picked world object fully to avoid stale hidden instances lingering in-scene.
        itemNo.Despawn(true);

        // If this item is the key, mark it collected for everyone.
        if (ObjectiveState.Instance != null && serverItemId == keyItemId)
        {
            ObjectiveState.Instance.KeyCollected.Value = true;
        }

        // tell only this client to show UI/hand item
        ulong clientId = senderClientId;
        GiveItemClientRpc(slot, serverItemId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    private bool IsSenderInPickupRange(ulong senderClientId, NetworkObject itemNo)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderClientId, out var client)) return false;
        if (client.PlayerObject == null) return false;
        if (itemNo == null) return false;

        Vector3 playerPos = client.PlayerObject.transform.position;
        float maxSqr = serverPickupRange * serverPickupRange;
        float bestSqr = float.PositiveInfinity;

        var colliders = itemNo.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null) continue;

            Vector3 closest = col.ClosestPoint(playerPos);
            float sqr = (closest - playerPos).sqrMagnitude;
            if (sqr < bestSqr) bestSqr = sqr;
        }

        if (bestSqr < float.PositiveInfinity)
            return bestSqr <= maxSqr;

        return (itemNo.transform.position - playerPos).sqrMagnitude <= maxSqr;
    }

    [ClientRpc]
    void GiveItemClientRpc(int slot, int itemId, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        if (slot < 0 || slot >= hotbarSlots) return;

        itemIds[slot] = itemId;

        var def = GetDef(itemId);
        if (def == null) return;

        // UI icon
        if (hotbarSlotImages != null && hotbarSlotImages.Length > slot && hotbarSlotImages[slot] != null)
        {
            hotbarSlotImages[slot].sprite = def.icon;
            hotbarSlotImages[slot].color = Color.white;
        }

        // hand visual
        if (handItems[slot] != null)
        {
            Destroy(handItems[slot]);
            handItems[slot] = null;
        }

        if (def.handPrefab != null)
        {
            handItems[slot] = Instantiate(def.handPrefab);
            handItems[slot].SetActive(false);
        }

        _soundFX?.PlayPickupItemSound(itemId, keyItemId, torchItemId);
        UpdateHandDisplay();
        UpdateInventoryDropPrompt();
        SyncTorchStateIfOwner(force: true);
    }

    public void DropSelectedItem()
    {
        if (!IsOwner) return;
        if (CameraInteraction.IsAnyLocalCctvActive || CameraInteraction.WasExitedThisFrame ||
            StartGame.IsAnyLocalWifiMinigameActive || StartGame.WasExitedThisFrame)
            return;
        if (selectedSlot < 0 || selectedSlot >= hotbarSlots) return;
        if (itemIds[selectedSlot] == EMPTY) return;

        DropItemFromSlotServerRpc(selectedSlot);
    }

    [ServerRpc(RequireOwnership = false)]
    void DropItemFromSlotServerRpc(int slot, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != OwnerClientId) return;

        if (slot < 0 || slot >= hotbarSlots) return;

        int itemId = itemIds[slot];
        if (itemId == EMPTY) return;

        var def = GetDef(itemId);
        if (def == null) return;

        if (def.worldPrefab == null)
        {
            Debug.LogError($"PlayerInventory: worldPrefab not assigned for itemId {itemId} ({def.name}).");
            return;
        }

        Vector3 dropPos = GetServerDropPosition();

        GameObject worldItem = Instantiate(def.worldPrefab, dropPos, Quaternion.identity);

        var no = worldItem.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("Dropped world prefab missing NetworkObject on ROOT.");
            Destroy(worldItem);
            return;
        }

        EnsureWorldPhysics(worldItem);
        // World drops are round-scoped; destroy on scene unload (don't persist into lobby/new match).
        no.Spawn(true);

        // clear server slot
        itemIds[slot] = EMPTY;
        if (itemId == torchItemId)
            ServerSetTorchState(false, false);

        // clear only dropping client's UI/hand
        ulong clientId = senderClientId;
        ClearSlotClientRpc(slot, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    void ClearSlotClientRpc(int slot, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        if (slot < 0 || slot >= hotbarSlots) return;

        itemIds[slot] = EMPTY;

        if (handItems[slot] != null)
        {
            Destroy(handItems[slot]);
            handItems[slot] = null;
        }

        if (hotbarSlotImages != null && hotbarSlotImages.Length > slot && hotbarSlotImages[slot] != null)
        {
            hotbarSlotImages[slot].sprite = null;
            hotbarSlotImages[slot].color = new Color(1, 1, 1, 0.3f);
        }

        UpdateHandDisplay();
        UpdateInventoryDropPrompt();
        SyncTorchStateIfOwner(force: true);
    }

    public int GetSelectedSlot() => selectedSlot;

    static void EnsureWorldPhysics(GameObject worldItem)
    {
        foreach (var col in worldItem.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        var rb = worldItem.GetComponent<Rigidbody>();
        if (rb == null) rb = worldItem.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private Vector3 GetServerDropPosition()
    {
        // Server copies for remote players do not own camera/drop anchors reliably.
        // Build a deterministic drop point from authoritative player transform instead.
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        else
            flatForward.Normalize();

        return transform.position + flatForward * 1.5f + Vector3.up * 0.5f;
    }

    // Called by PlayerHealth on the server when this player dies.
    public void DropAllItemsOnDeathServer()
    {
        if (!IsServer) return;

        // Drop every occupied hotbar slot
        for (int slot = 0; slot < hotbarSlots; slot++)
        {
            if (slot < 0 || slot >= itemIds.Length) continue;
            if (itemIds[slot] == EMPTY) continue;

            DropItemFromSlotServer_Internal(slot);
        }

        // Clear the dead player's local UI + hand visuals
        if (OwnerClientId != NetworkManager.ServerClientId)
        {
            ClearAllSlotsClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }
        else
        {
            ClearAllSlotsClientRpc();
        }

        ServerSetTorchState(false, false);
    }

    // Internal server-side drop
    private void DropItemFromSlotServer_Internal(int slot)
    {
        if (slot < 0 || slot >= hotbarSlots) return;

        int itemId = itemIds[slot];
        if (itemId == EMPTY) return;

        var def = GetDef(itemId);
        if (def == null) return;

        if (def.worldPrefab == null)
        {
            Debug.LogError($"PlayerInventory: worldPrefab not assigned for itemId {itemId} ({def.name}).");
            return;
        }

        Vector3 dropPos = GetServerDropPosition();

        GameObject worldItem = Instantiate(def.worldPrefab, dropPos, Quaternion.identity);

        var no = worldItem.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("Dropped world prefab missing NetworkObject on ROOT.");
            Destroy(worldItem);
            return;
        }

        EnsureWorldPhysics(worldItem);
        // World drops are round-scoped; destroy on scene unload (don't persist into lobby/new match).
        no.Spawn(true);

        // Clear server slot
        itemIds[slot] = EMPTY;
        if (itemId == torchItemId)
            ServerSetTorchState(false, false);
    }

    // Server: drops items when a player leaves/disconnects from session
    private void DropAllItemsServer_NoClientUI()
    {
        if (!IsServer) return;

        for (int slot = 0; slot < hotbarSlots; slot++)
        {
            if (slot < 0 || slot >= itemIds.Length) continue;
            if (itemIds[slot] == EMPTY) continue;

            DropItemFromSlotServer_Internal(slot);
        }

        ServerSetTorchState(false, false);
    }

    // Server: wipes this player's inventory for a fresh match
    public void ResetInventoryForNewMatchServer()
    {
        if (!IsServer) return;

        // Clear server-authoritative IDs
        for (int i = 0; i < hotbarSlots; i++)
            itemIds[i] = EMPTY;

        // Tell ONLY the owning client to clear UI + destroy hand prefabs
        ClearAllSlotsClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });

        ServerSetTorchState(false, false);
    }

    [ClientRpc]
    private void ClearAllSlotsClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;

        for (int slot = 0; slot < hotbarSlots; slot++)
        {
            if (slot < 0 || slot >= itemIds.Length) continue;

            itemIds[slot] = EMPTY;

            if (handItems[slot] != null)
            {
                Destroy(handItems[slot]);
                handItems[slot] = null;
            }

            if (hotbarSlotImages != null && hotbarSlotImages.Length > slot && hotbarSlotImages[slot] != null)
            {
                hotbarSlotImages[slot].sprite = null;
                hotbarSlotImages[slot].color = new Color(1, 1, 1, 0.3f);
            }
        }
        // Reset selection to slot 0 after a match reset
        selectedSlot = 0;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
        UpdateInventoryDropPrompt(); // keeps the "Press Q" prompt correct
        SyncTorchStateIfOwner(force: true);
    }

    public override void OnDestroy()
    {
        RemoveInputCallbacks();

        if (inputActions != null)
            inputActions.Disable();

        base.OnDestroy();
    }

    public bool HasItemId(int itemId) // used to check keys against locked doors
    {
        for (int i = 0; i < hotbarSlots; i++)
            if (itemIds[i] == itemId)
                return true;
        return false;
    }

    private void OnTorchNetworkStateChanged(bool oldValue, bool newValue)
    {
        UpdateRemoteTorchVisual();
    }

    private void EnsureRemoteTorchLight()
    {
        if (!IsClient) return;
        if (IsOwner) return;
        if (_remoteTorchLightObject != null) return;

        _remoteTorchLightObject = new GameObject("RemoteTorchLight");
        _remoteTorchLightObject.transform.SetParent(transform, false);
        _remoteTorchAnchor = ResolveRemoteTorchAnchor();

        _remoteTorchLight = _remoteTorchLightObject.AddComponent<Light>();
        _remoteTorchLight.type = LightType.Spot;
        _remoteTorchLight.color = remoteTorchColor;
        _remoteTorchLight.intensity = remoteTorchIntensity;
        _remoteTorchLight.range = remoteTorchRange;
        _remoteTorchLight.spotAngle = remoteTorchSpotAngle;
        _remoteTorchLight.shadows = LightShadows.None;
        _remoteTorchLight.enabled = false;

        _remoteTorchSmoothedPitch = remoteTorchLocalEuler.x;
        UpdateRemoteTorchPose();
    }

    private void UpdateRemoteTorchVisual()
    {
        if (_remoteTorchLight == null) return;

        _remoteTorchLight.color = remoteTorchColor;
        _remoteTorchLight.intensity = remoteTorchIntensity;
        _remoteTorchLight.range = remoteTorchRange;
        _remoteTorchLight.spotAngle = remoteTorchSpotAngle;

        bool inGame = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "03_Game";
        bool shouldBeOn = !IsOwner && inGame && _torchEquipped.Value && _torchOn.Value;
        _remoteTorchLight.enabled = shouldBeOn;
        UpdateRemoteTorchPose();
    }

    private void UpdateRemoteTorchPose()
    {
        if (_remoteTorchLightObject == null) return;
        var anchor = ResolveRemoteTorchAnchor();

        float targetPitch = remoteTorchLocalEuler.x;

        if (remoteTorchUseLookPitch && _networkPlayer != null)
            targetPitch += _networkPlayer.LookPitch.Value + remoteTorchPitchOffset;

        _remoteTorchSmoothedPitch = Mathf.LerpAngle(
            _remoteTorchSmoothedPitch,
            targetPitch,
            Mathf.Max(1f, remoteTorchPitchLerpSpeed) * Time.deltaTime);

        Vector3 anchorPosition = anchor != null ? anchor.position : transform.position;
        Quaternion anchorRotation = anchor != null ? anchor.rotation : transform.rotation;

        Quaternion localRotation = Quaternion.Euler(
            _remoteTorchSmoothedPitch,
            remoteTorchLocalEuler.y,
            remoteTorchLocalEuler.z);

        _remoteTorchLightObject.transform.SetPositionAndRotation(
            anchorPosition + (anchorRotation * remoteTorchLocalOffset),
            anchorRotation * localRotation);
    }

    private Transform ResolveRemoteTorchAnchor()
    {
        if (_remoteTorchAnchor != null)
            return _remoteTorchAnchor;

        if (remoteTorchAnchorToCamera)
        {
            if (_networkPlayer != null && _networkPlayer.PlayerCameraTransform != null)
            {
                _remoteTorchAnchor = _networkPlayer.PlayerCameraTransform;
                return _remoteTorchAnchor;
            }

            var cameraByName = FindChildTransformByName("MainCamera");
            if (cameraByName != null)
            {
                _remoteTorchAnchor = cameraByName;
                return _remoteTorchAnchor;
            }
        }

        _remoteTorchAnchor = transform;
        return _remoteTorchAnchor;
    }

    private Transform FindChildTransformByName(string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return null;

        var children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (t != null && t.name == childName)
                return t;
        }

        return null;
    }

    private void SyncTorchStateIfOwner(bool force = false)
    {
        if (!IsOwner || !IsSpawned) return;

        bool equipped = TryGetSelectedTorchState(out bool isOn);

        if (!force && equipped == _lastSentTorchEquipped && isOn == _lastSentTorchOn)
            return;

        _lastSentTorchEquipped = equipped;
        _lastSentTorchOn = isOn;

        SetTorchStateServerRpc(equipped, isOn);
    }

    private bool TryGetSelectedTorchState(out bool isOn)
    {
        isOn = false;

        if (selectedSlot < 0 || selectedSlot >= hotbarSlots) return false;
        if (itemIds[selectedSlot] != torchItemId) return false;
        if (handItems == null || selectedSlot >= handItems.Length) return false;

        var hand = handItems[selectedSlot];
        if (hand == null) return false;

        var torch = hand.GetComponentInChildren<TorchHeld>(true);
        if (torch == null) return false;

        isOn = torch.IsOn;
        return true;
    }

    [ServerRpc]
    private void SetTorchStateServerRpc(bool equipped, bool isOn, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        ServerSetTorchState(equipped, isOn);
    }

    private void ServerSetTorchState(bool equipped, bool isOn)
    {
        if (!IsServer) return;

        _torchEquipped.Value = equipped;
        _torchOn.Value = equipped && isOn;
    }
}