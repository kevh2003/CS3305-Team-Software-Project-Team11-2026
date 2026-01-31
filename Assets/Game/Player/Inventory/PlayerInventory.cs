using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Manages player inventory for networked players.
/// Only the local player can see and interact with their own UI.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Settings")]
    public int hotbarSlots = 2;
    public int inventorySlots = 3;

    // Internal data - networked if needed in future
    private Sprite[] itemIcons;
    private Material[] itemMaterials;
    private int selectedSlot = 0;
    private bool inventoryOpen = false;

    // UI References (set by InventoryUI)
    [HideInInspector] public Canvas canvas;
    [HideInInspector] public GameObject hotbarPanel;
    [HideInInspector] public GameObject inventoryPanel;
    [HideInInspector] public Image[] hotbarSlotImages;
    [HideInInspector] public Image[] inventorySlotImages;

    // Player references
    [HideInInspector] public PlayerMovement playerMovement;
    [HideInInspector] public Transform handPosition;
    [HideInInspector] public Transform dropPosition;

    private GameObject handItem;
    private PlayerInputActions inputActions;
    private float lastScrollValue = 0f;

    void Awake()
    {
        itemIcons = new Sprite[hotbarSlots + inventorySlots];
        itemMaterials = new Material[hotbarSlots + inventorySlots];
        inputActions = new PlayerInputActions();
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"🎮 PlayerInventory.OnNetworkSpawn() - IsOwner: {IsOwner}");

        if (!IsOwner)
        {
            Debug.Log("❌ Not owner, disabling PlayerInventory");
            enabled = false;
            return;
        }

        Debug.Log("✅ Is owner, enabling input");
        inputActions.Enable();
        SetupInputCallbacks();
        Debug.Log("✅ Input callbacks setup complete");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (inputActions != null)
            inputActions.Disable();
    }

    void SetupInputCallbacks()
    {
        if (inputActions == null)
        {
            Debug.LogError("❌ inputActions is null in SetupInputCallbacks!");
            return;
        }

        inputActions.Player.HotbarSlot1.performed += ctx => SelectSlot(0);
        inputActions.Player.HotbarSlot2.performed += ctx => SelectSlot(1);
        inputActions.Player.ToggleInventory.performed += ctx => ToggleInventory();
        inputActions.Player.DropItem.performed += ctx => DropItem(selectedSlot);

        Debug.Log("✅ Input callbacks registered");
    }

    void Update()
    {
        if (inputActions == null) return; // Safety check

        HandleScrollWheel();
    }

    void HandleScrollWheel()
    {
        if (hotbarSlotImages == null || hotbarSlotImages.Length == 0) return; // Safety check

        float scroll = inputActions.Player.ScrollWheel.ReadValue<float>();
        if (scroll > 0.1f)
        {
            SelectSlot((selectedSlot + 1) % hotbarSlots);
        }
        else if (scroll < -0.1f)
        {
            SelectSlot((selectedSlot - 1 + hotbarSlots) % hotbarSlots);
        }
    }

    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= hotbarSlots) return;
        selectedSlot = slot;
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
        if (handItem != null) Destroy(handItem);
        if (handPosition == null || itemIcons[selectedSlot] == null) return;

        handItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        handItem.transform.SetParent(handPosition);
        handItem.transform.localPosition = Vector3.zero;
        handItem.transform.localRotation = Quaternion.identity;
        handItem.transform.localScale = Vector3.one * 0.3f;

        if (itemMaterials[selectedSlot] != null)
            handItem.GetComponent<Renderer>().material = itemMaterials[selectedSlot];

        Destroy(handItem.GetComponent<Collider>());
    }

    public void ToggleInventory()
    {
        inventoryOpen = !inventoryOpen;

        if (inventoryPanel != null)
            inventoryPanel.SetActive(inventoryOpen);

        if (inventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerMovement != null) playerMovement.enabled = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (playerMovement != null) playerMovement.enabled = true;
        }
    }

    public bool AddItem(Sprite icon, Material mat)
    {
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] == null)
            {
                itemIcons[i] = icon;
                itemMaterials[i] = mat;
                UpdateSlotVisual(i);
                if (i == selectedSlot) UpdateHandDisplay();
                return true;
            }
        }
        return false;
    }

    public void RemoveItem(int index)
    {
        if (index < 0 || index >= itemIcons.Length) return;
        itemIcons[index] = null;
        itemMaterials[index] = null;
        UpdateSlotVisual(index);
        if (index == selectedSlot) UpdateHandDisplay();
    }

    void UpdateSlotVisual(int index)
    {
        Image slotImage = GetSlotImage(index);
        if (slotImage != null)
        {
            slotImage.sprite = itemIcons[index];
            slotImage.enabled = (itemIcons[index] != null);
        }
    }

    Image GetSlotImage(int index)
    {
        if (index < hotbarSlots && hotbarSlotImages != null && index < hotbarSlotImages.Length)
            return hotbarSlotImages[index];

        int invIndex = index - hotbarSlots;
        if (invIndex >= 0 && inventorySlotImages != null && invIndex < inventorySlotImages.Length)
            return inventorySlotImages[invIndex];

        return null;
    }

    void DropItem(int index)
    {
        if (itemIcons[index] == null || dropPosition == null) return;

        GameObject dropped = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dropped.transform.position = dropPosition.position + dropPosition.forward * 1.5f;
        dropped.transform.localScale = Vector3.one * 0.5f;

        if (itemMaterials[index] != null)
            dropped.GetComponent<Renderer>().material = itemMaterials[index];

        WorldPickup pickup = dropped.AddComponent<WorldPickup>();
        pickup.itemIcon = itemIcons[index];
        pickup.itemMaterial = itemMaterials[index];

        dropped.AddComponent<Rigidbody>().mass = 0.5f;

        RemoveItem(index);
    }

    public Sprite GetItem(int index)
    {
        if (index >= 0 && index < itemIcons.Length)
            return itemIcons[index];
        return null;
    }

    public void SwapItems(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= itemIcons.Length) return;
        if (toIndex < 0 || toIndex >= itemIcons.Length) return;

        Sprite tempIcon = itemIcons[fromIndex];
        Material tempMat = itemMaterials[fromIndex];

        itemIcons[fromIndex] = itemIcons[toIndex];
        itemMaterials[fromIndex] = itemMaterials[toIndex];

        itemIcons[toIndex] = tempIcon;
        itemMaterials[toIndex] = tempMat;

        UpdateSlotVisual(fromIndex);
        UpdateSlotVisual(toIndex);

        if (fromIndex == selectedSlot || toIndex == selectedSlot)
            UpdateHandDisplay();
    }
}