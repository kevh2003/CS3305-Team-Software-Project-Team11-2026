using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Manages player inventory for networked players.
/// Uses PlayerInput component (matches your Interactor and movement scripts).
/// Only the local player can see and interact with their own UI.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    [HideInInspector] public MonoBehaviour[] scriptsToDisable;
    [HideInInspector] public MonoBehaviour cameraController;
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
    private float scrollWheelValue = 0f;

    void Awake()
    {
        itemIcons = new Sprite[hotbarSlots + inventorySlots];
        itemMaterials = new Material[hotbarSlots + inventorySlots];
        Debug.Log("✅ PlayerInventory: Initialized in Awake");
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

        Debug.Log("✅ Is owner, inventory ready");
    }

    // These methods are called by Unity's PlayerInput component
    // Make sure you have these actions in your Input Actions asset:
    // - HotbarSlot0 (key: 1)
    // - HotbarSlot1 (key: 2)
    // - ToggleInventory (key: Tab or I)
    // - DropItem (key: Q)
    // - ScrollWheel (Mouse scroll)

    public void OnHotbarSlot0(InputValue value)
    {
        if (!IsOwner) return;
        if (value.isPressed) SelectSlot(0);
    }

    public void OnHotbarSlot1(InputValue value)
    {
        if (!IsOwner) return;
        if (value.isPressed) SelectSlot(1);
    }

    public void OnToggleInventory(InputValue value)
    {
        if (!IsOwner) return;
        if (value.isPressed) ToggleInventory();
    }

    public void OnDropItem(InputValue value)
    {
        if (!IsOwner) return;
        if (value.isPressed) DropItem(selectedSlot);
    }

    public void OnScrollWheel(InputValue value)
    {
        if (!IsOwner) return;
        scrollWheelValue = value.Get<float>();
    }

    void Update()
    {
        if (!IsOwner) return;
        HandleScrollWheel();
    }

    void HandleScrollWheel()
    {
        if (hotbarSlotImages == null || hotbarSlotImages.Length == 0)
        {
            return; // Safety check - not ready yet
        }

        if (scrollWheelValue > 0.1f)
        {
            SelectSlot((selectedSlot + 1) % hotbarSlots);
            scrollWheelValue = 0f;
        }
        else if (scrollWheelValue < -0.1f)
        {
            SelectSlot((selectedSlot - 1 + hotbarSlots) % hotbarSlots);
            scrollWheelValue = 0f;
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;

        selectedSlot = index;

        Debug.Log($"🎯 SelectSlot({index}) called");
        Debug.Log($"   handPosition null? {handPosition == null}");
        Debug.Log($"   itemMaterials[{index}] null? {(index < itemMaterials.Length ? itemMaterials[index] == null : true)}");

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
        // Clear existing hand item
        if (handPosition != null)
        {
            foreach (Transform child in handPosition)
            {
                Destroy(child.gameObject);
            }
        }

        // Show item in selected slot
        if (selectedSlot < hotbarSlots && itemMaterials[selectedSlot] != null)
        {
            if (handPosition != null)
            {
                GameObject handItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handItem.transform.SetParent(handPosition);
                handItem.transform.localPosition = Vector3.zero;
                handItem.transform.localRotation = Quaternion.identity;
                handItem.transform.localScale = Vector3.one * 0.3f; // Small cube in hand

                // Remove collider so it doesn't interfere
                Collider col = handItem.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Apply material
                Renderer rend = handItem.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = itemMaterials[selectedSlot];
                }

                Debug.Log($"✅ Displaying item in hand at slot {selectedSlot}");
            }
        }
    }

    void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        bool isOpen = !inventoryPanel.activeSelf;
        inventoryPanel.SetActive(isOpen);

        if (isOpen)
        {
            // Inventory opened
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerMovement != null)
                playerMovement.enabled = false;

            // Disable all camera scripts
            if (scriptsToDisable != null)
            {
                foreach (var script in scriptsToDisable)
                {
                    if (script != null) script.enabled = false;
                }
            }

            // Hide crosshair
            Crosshair crosshair = GetComponent<Crosshair>();
            if (crosshair != null)
                crosshair.Hide();
        }
        else
        {
            // Inventory closed
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerMovement != null)
                playerMovement.enabled = true;

            // Re-enable all camera scripts
            if (scriptsToDisable != null)
            {
                foreach (var script in scriptsToDisable)
                {
                    if (script != null) script.enabled = true;
                }
            }

            // Show crosshair
            Crosshair crosshair = GetComponent<Crosshair>();
            if (crosshair != null)
                crosshair.Show();
        }
    }

    public bool AddItem(Sprite icon, Material mat)
    {
        // Find empty slot
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] == null)
            {
                itemIcons[i] = icon;
                itemMaterials[i] = mat;

                // Update visual
                if (i < hotbarSlots && hotbarSlotImages[i] != null)
                {
                    hotbarSlotImages[i].sprite = icon;
                    hotbarSlotImages[i].color = Color.white;
                }
                else if (i >= hotbarSlots && inventorySlotImages[i - hotbarSlots] != null)
                {
                    inventorySlotImages[i - hotbarSlots].sprite = icon;
                    inventorySlotImages[i - hotbarSlots].color = Color.white;
                }

                Debug.Log($"✅ Added item to slot {i}");

                // If added to selected slot, update hand display
                if (i == selectedSlot)
                {
                    UpdateHandDisplay();
                }

                return true;
            }
        }

        Debug.LogWarning("Inventory full!");
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

    public void SwapItems(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= itemIcons.Length) return;
        if (slotB < 0 || slotB >= itemIcons.Length) return;

        Debug.Log($"🔄 Swapping slot {slotA} with slot {slotB}");

        // Swap icons
        Sprite tempIcon = itemIcons[slotA];
        itemIcons[slotA] = itemIcons[slotB];
        itemIcons[slotB] = tempIcon;

        // Swap materials
        Material tempMat = itemMaterials[slotA];
        itemMaterials[slotA] = itemMaterials[slotB];
        itemMaterials[slotB] = tempMat;

        // Update visuals for slot A
        if (slotA < hotbarSlots && hotbarSlotImages[slotA] != null)
        {
            hotbarSlotImages[slotA].sprite = itemIcons[slotA];
            hotbarSlotImages[slotA].color = itemIcons[slotA] != null ? Color.white : new Color(1, 1, 1, 0);
        }
        else if (slotA >= hotbarSlots && inventorySlotImages[slotA - hotbarSlots] != null)
        {
            inventorySlotImages[slotA - hotbarSlots].sprite = itemIcons[slotA];
            inventorySlotImages[slotA - hotbarSlots].color = itemIcons[slotA] != null ? Color.white : new Color(1, 1, 1, 0);
        }

        // Update visuals for slot B
        if (slotB < hotbarSlots && hotbarSlotImages[slotB] != null)
        {
            hotbarSlotImages[slotB].sprite = itemIcons[slotB];
            hotbarSlotImages[slotB].color = itemIcons[slotB] != null ? Color.white : new Color(1, 1, 1, 0);
        }
        else if (slotB >= hotbarSlots && inventorySlotImages[slotB - hotbarSlots] != null)
        {
            inventorySlotImages[slotB - hotbarSlots].sprite = itemIcons[slotB];
            inventorySlotImages[slotB - hotbarSlots].color = itemIcons[slotB] != null ? Color.white : new Color(1, 1, 1, 0);
        }

        // Update hand display if selected slot was involved
        if (slotA == selectedSlot || slotB == selectedSlot)
        {
            UpdateHandDisplay();
        }
    }
}