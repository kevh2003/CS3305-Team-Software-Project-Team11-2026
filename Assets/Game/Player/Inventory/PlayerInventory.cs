using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Hotbar Settings")]
    public int hotbarSlots = 2;

    [Header("Key Item Setup (ASSIGN ON PLAYER PREFAB)")]
    public GameObject keyWorldPrefab;   // world key prefab: must have NetworkObject + Collider + WorldPickup
    public GameObject keyHandPrefab;    // hand-only prefab
    public Sprite keyIcon;              // UI icon

    [HideInInspector] public GameObject hotbarPanel;
    [HideInInspector] public Image[] hotbarSlotImages;
    [HideInInspector] public Transform handPosition;
    [HideInInspector] public Transform dropPosition;

    private PlayerInputActions inputActions;

    private const int EMPTY = -1;
    private const int KEY_ID = 1;

    // Server authoritative inventory state 
    private int[] itemIds;

    // Local-only hand visuals
    private GameObject[] handItems;
    private int selectedSlot = 0;

    void Awake()
    {
        itemIds = new int[hotbarSlots];
        for (int i = 0; i < hotbarSlots; i++) itemIds[i] = EMPTY;

        handItems = new GameObject[hotbarSlots];
        inputActions = new PlayerInputActions();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Keep enabled on server.
        // Disable only on remote clients (not owner AND not server).
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

    bool ServerHasKey()
    {
        for (int i = 0; i < hotbarSlots; i++)
            if (itemIds[i] == KEY_ID)
                return true;
        return false;
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < hotbarSlots; i++)
            if (itemIds[i] == EMPTY)
                return i;
        return -1;
    }

    [ServerRpc(RequireOwnership = false)]
    public void PickupKeyServerRpc(NetworkObjectReference keyRef, ServerRpcParams rpcParams = default)
    {
        if (!keyRef.TryGet(out NetworkObject keyNo)) return;
        if (!keyNo.IsSpawned) return;

        // prevent duplication
        if (ServerHasKey()) return;

        int slot = FindFirstEmptySlot();
        if (slot == -1) return;

        // record on server
        itemIds[slot] = KEY_ID;

        // despawn the world key for everyone
        keyNo.Despawn();

        // tell ONLY this client to show UI/hand item
        ulong clientId = rpcParams.Receive.SenderClientId;
        GiveKeyClientRpc(slot, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    void GiveKeyClientRpc(int slot, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        if (slot < 0 || slot >= hotbarSlots) return;

        itemIds[slot] = KEY_ID;

        // UI icon
        if (hotbarSlotImages != null && hotbarSlotImages.Length > slot && hotbarSlotImages[slot] != null)
        {
            hotbarSlotImages[slot].sprite = keyIcon;
            hotbarSlotImages[slot].color = Color.white;
        }

        // hand visual
        if (handItems[slot] != null)
        {
            Destroy(handItems[slot]);
            handItems[slot] = null;
        }

        if (keyHandPrefab != null)
        {
            handItems[slot] = Instantiate(keyHandPrefab);
            handItems[slot].SetActive(false);
        }

        UpdateHandDisplay();
    }

    public void DropSelectedItem()
    {
        if (!IsOwner) return;
        if (selectedSlot < 0 || selectedSlot >= hotbarSlots) return;
        if (itemIds[selectedSlot] != KEY_ID) return;

        DropKeyFromSlotServerRpc(selectedSlot);
    }

    [ServerRpc(RequireOwnership = false)]
    void DropKeyFromSlotServerRpc(int slot, ServerRpcParams rpcParams = default)
    {
        if (slot < 0 || slot >= hotbarSlots) return;
        if (itemIds[slot] != KEY_ID) return;

        if (keyWorldPrefab == null)
        {
            Debug.LogError("PlayerInventory: keyWorldPrefab is NOT assigned on PLAYER prefab.");
            return;
        }

        Vector3 dropPos = (dropPosition != null)
            ? dropPosition.position
            : (transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);

        GameObject worldItem = Instantiate(keyWorldPrefab, dropPos, Quaternion.identity);

        var no = worldItem.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("Key world prefab missing NetworkObject on ROOT.");
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

    static void EnsureWorldPhysics(GameObject worldItem)
    {
        // colliders on
        foreach (var col in worldItem.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        // make sure it falls
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
