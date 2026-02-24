using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

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

        // Keep enabled on server.
        // Disable only on remote clients.
        if (!IsOwner && !IsServer)
        {
            enabled = false;
            return;
        }

        if (IsOwner)
        {
            inputActions.Enable();
            SetupInputCallbacks();
        }
    }

    void SetupInputCallbacks()
    {
        inputActions.Player.HotbarSlot0.performed += _ => SelectSlot(0);
        inputActions.Player.HotbarSlot1.performed += _ => SelectSlot(1);
        inputActions.Player.DropItem.performed += _ => DropSelectedItem();
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
        itemNo.Despawn();

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

    void OnDestroy()
    {
        if (inputActions != null)
            inputActions.Disable();
    }
}