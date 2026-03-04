using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Hotbar Settings")]
    public int hotbarSlots = 2;

    [Header("Item Database (assign all ItemDefinitions here)")]
    public List<ItemDefinition> itemDatabase = new List<ItemDefinition>();

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

    void Awake()
    {
        itemIds = new int[hotbarSlots];
        for (int i = 0; i < hotbarSlots; i++) itemIds[i] = EMPTY;

        handItems = new GameObject[hotbarSlots];
        inputActions = new PlayerInputActions();

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

        // IMPORTANT:
        // - Server must keep this component enabled for ALL players (authoritative state / RPCs / spawning)
        // - Only the owning client should read input + drive UI -kev

        if (!IsOwner && !IsServer)
        {
            enabled = false;
            return;
        }

        // Owner: enable input + bind callbacks
        if (IsOwner)
        {
            // Safety: avoid double-subscribing if something reinitializes
            RemoveInputCallbacks();

            inputActions.Enable();
            SetupInputCallbacks();

            StartCoroutine(OwnerLateInit());
            StartCoroutine(OwnerEnsureAnchors());
        }
    }

    public override void OnNetworkDespawn()
    {
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
        }

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
    private void OnDrop(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => DropSelectedItem();


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
                    hp = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HandPosition");
                handPosition = hp;
            }

            if (dropPosition == null)
            {
                var dp = transform.Find("DropPosition");
                if (dp == null)
                    dp = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DropPosition");
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
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            DropSelectedItem();
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;
        selectedSlot = index;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
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
        if (!itemRef.TryGet(out NetworkObject itemNo)) return;
        if (!itemNo.IsSpawned) return;

        // Validate item exists in database on server
        var def = GetDef(itemId);
        if (def == null) return;

        int slot = -1;

        // Try selected slot first
        if (preferredSlot >= 0 && preferredSlot < hotbarSlots && itemIds[preferredSlot] == EMPTY)
            slot = preferredSlot;
        else
            slot = FindFirstEmptySlot();

        if (slot == -1) return; // inventory full

        // record on server
        itemIds[slot] = itemId;

        // despawn the world object for everyone
        itemNo.Despawn(false);

        // If this item is the key, mark it collected for everyone
        // NOTE: ensure this matches key itemId (door uses requiredKeyItemId = 1 by default) -kev
        if (ObjectiveState.Instance != null && itemId == 1)
        {
            ObjectiveState.Instance.KeyCollected.Value = true;
        }

        // tell only this client to show UI/hand item
        ulong clientId = rpcParams.Receive.SenderClientId;
        GiveItemClientRpc(slot, itemId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
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

        UpdateHandDisplay();
    }

    public void DropSelectedItem()
    {
        if (!IsOwner) return;
        if (selectedSlot < 0 || selectedSlot >= hotbarSlots) return;
        if (itemIds[selectedSlot] == EMPTY) return;

        DropItemFromSlotServerRpc(selectedSlot);
    }

    [ServerRpc(RequireOwnership = false)]
    void DropItemFromSlotServerRpc(int slot, ServerRpcParams rpcParams = default)
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

        Vector3 dropPos = (dropPosition != null)
            ? dropPosition.position
            : (transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);

        GameObject worldItem = Instantiate(def.worldPrefab, dropPos, Quaternion.identity);

        var no = worldItem.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("Dropped world prefab missing NetworkObject on ROOT.");
            Destroy(worldItem);
            return;
        }

        EnsureWorldPhysics(worldItem);
        no.Spawn();

        // clear server slot
        itemIds[slot] = EMPTY;

        // clear only dropping client's UI/hand
        ulong clientId = rpcParams.Receive.SenderClientId;
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

    // Drop all items on death logic (SERVER ONLY) - Called by PlayerHealth when a player dies - kev
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

        Vector3 dropPos = (dropPosition != null)
            ? dropPosition.position
            : (transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);

        GameObject worldItem = Instantiate(def.worldPrefab, dropPos, Quaternion.identity);

        var no = worldItem.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("Dropped world prefab missing NetworkObject on ROOT.");
            Destroy(worldItem);
            return;
        }

        EnsureWorldPhysics(worldItem);
        no.Spawn();

        // Clear server slot
        itemIds[slot] = EMPTY;
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
    }

    void OnDestroy()
    {
        RemoveInputCallbacks();

        if (inputActions != null)
            inputActions.Disable();
    }

    public bool HasItemId(int itemId) // used to check keys against locked doors
    {
        for (int i = 0; i < hotbarSlots; i++)
            if (itemIds[i] == itemId)
                return true;
        return false;
    }
}