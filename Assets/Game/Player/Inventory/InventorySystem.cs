using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InventorySystem : MonoBehaviour
{
    [Header("UI References")]
    public GameObject hotbarPanel;
    public GameObject inventoryPanel;
    public Canvas canvas;
    public List<Image> hotbarSlots = new List<Image>(2);
    public List<Image> inventorySlots = new List<Image>(3);

    [Header("Inventory Data")]
    private List<Sprite> items = new List<Sprite>();
    private List<Material> itemMaterials = new List<Material>();
    
    [Header("Player Reference")]
    public PlayerMovement playerMovement;
    
    [Header("Hand Display")]
    public Transform handPosition;
    private GameObject currentHandItem;
    
    [Header("Hotbar Selection")]
    public Color outlineColor = new Color32(139, 0, 0, 255);
    private int selectedHotbarSlot = 0;
    
    [Header("Drop Settings")]
    public Transform dropPosition;
    public GameObject droppedItemPrefab;
    
    private bool isInventoryOpen = false;
    private GameObject currentDraggedItem;
    private int draggedFromSlot = -1;
    private Sprite draggedSprite;
    private Material draggedMaterial;

    void Start()
    {
        InitializeInventory();
        SetupSlots();
        SelectHotbarSlot(0);
    }

    void Update()
    {
        HandleInventoryToggle();
        HandleHotbarSelection();
        HandleItemDrop();
        UpdateDraggedItem();
    }

    #region Initialization

    void InitializeInventory()
    {
        if (hotbarPanel != null)
            hotbarPanel.SetActive(true);
        
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
        
        items.Clear();
        itemMaterials.Clear();
        
        int totalSlots = hotbarSlots.Count + inventorySlots.Count;
        for (int i = 0; i < totalSlots; i++)
        {
            items.Add(null);
            itemMaterials.Add(null);
        }
        
        Color slotColor = new Color32(45, 45, 45, 255);
        foreach (Image slot in hotbarSlots)
        {
            if (slot != null)
                slot.color = slotColor;
        }
    }

    void SetupSlots()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] != null)
            {
                AddSlotComponent(hotbarSlots[i].gameObject, i);
            }
        }

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (inventorySlots[i] != null)
            {
                AddSlotComponent(inventorySlots[i].gameObject, i + hotbarSlots.Count);
            }
        }
    }

    void AddSlotComponent(GameObject slotObject, int slotIndex)
    {
        ItemSlot itemSlot = slotObject.GetComponent<ItemSlot>();
        if (itemSlot == null)
        {
            itemSlot = slotObject.AddComponent<ItemSlot>();
        }
        itemSlot.slotIndex = slotIndex;
        itemSlot.inventorySystem = this;
    }

    #endregion

    #region Input Handling

    void HandleInventoryToggle()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }

    void HandleHotbarSelection()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectHotbarSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectHotbarSlot(1);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            SelectHotbarSlot(0);
        }
        else if (scroll < 0f)
        {
            SelectHotbarSlot(1);
        }
    }

    void HandleItemDrop()
    {
        if (Input.GetKeyDown(KeyCode.Q) && !isInventoryOpen)
        {
            DropItemFromSlot(selectedHotbarSlot);
        }
    }

    void UpdateDraggedItem()
    {
        if (currentDraggedItem != null)
        {
            currentDraggedItem.transform.position = Input.mousePosition;
            
            if (Input.GetMouseButtonDown(0) && !IsMouseOverInventory())
            {
                DropDraggedItem();
            }
        }
    }

    #endregion

    #region Inventory Management

    public bool AddItem(Sprite itemSprite, Material itemMaterial = null)
    {
        // Try hotbar first
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (items[i] == null)
            {
                SetItemInSlot(i, itemSprite, itemMaterial);
                
                if (i == selectedHotbarSlot)
                {
                    UpdateHandDisplay();
                }
                
                return true;
            }
        }

        // Try inventory slots
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            int slotIndex = i + hotbarSlots.Count;
            if (items[slotIndex] == null)
            {
                SetItemInSlot(slotIndex, itemSprite, itemMaterial);
                return true;
            }
        }

        return false; // Inventory full
    }

    void SetItemInSlot(int slotIndex, Sprite itemSprite, Material itemMaterial)
    {
        items[slotIndex] = itemSprite;
        itemMaterials[slotIndex] = itemMaterial;
        
        Image slotImage = GetSlotImage(slotIndex);
        if (slotImage != null)
        {
            slotImage.sprite = itemSprite;
            slotImage.enabled = true;
        }
    }

    void RemoveItem(int slotIndex)
    {
        items[slotIndex] = null;
        itemMaterials[slotIndex] = null;
        
        Image slotImage = GetSlotImage(slotIndex);
        if (slotImage != null)
        {
            slotImage.sprite = null;
        }
    }

    public Sprite GetItemInSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < items.Count)
            return items[slotIndex];
        return null;
    }

    Material GetMaterialInSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < itemMaterials.Count)
            return itemMaterials[slotIndex];
        return null;
    }

    Image GetSlotImage(int slotIndex)
    {
        if (slotIndex < hotbarSlots.Count)
            return hotbarSlots[slotIndex];
        
        int inventoryIndex = slotIndex - hotbarSlots.Count;
        if (inventoryIndex >= 0 && inventoryIndex < inventorySlots.Count)
            return inventorySlots[inventoryIndex];
        
        return null;
    }

    #endregion

    #region UI Management

    void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        
        if (inventoryPanel != null)
            inventoryPanel.SetActive(isInventoryOpen);
        
        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            if (playerMovement != null)
                playerMovement.enabled = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            if (playerMovement != null)
                playerMovement.enabled = true;
        }
    }

    void SelectHotbarSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= hotbarSlots.Count)
            return;

        selectedHotbarSlot = slotIndex;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
    }

    void UpdateHotbarOutlines()
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (hotbarSlots[i] != null)
            {
                UnityEngine.UI.Outline outline = hotbarSlots[i].GetComponent<UnityEngine.UI.Outline>();
                if (outline != null)
                {
                    outline.enabled = (i == selectedHotbarSlot);
                }
            }
        }
    }

    bool IsMouseOverInventory()
    {
        if (hotbarPanel != null && IsMouseOverPanel(hotbarPanel))
            return true;

        if (inventoryPanel != null && inventoryPanel.activeSelf && IsMouseOverPanel(inventoryPanel))
            return true;

        return false;
    }

    bool IsMouseOverPanel(GameObject panel)
    {
        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform, Input.mousePosition, canvas.worldCamera);
    }

    #endregion

    #region Drag and Drop

    public void OnSlotClicked(int slotIndex)
    {
        if (draggedFromSlot == -1)
        {
            // Pick up item
            Sprite itemInSlot = GetItemInSlot(slotIndex);
            if (itemInSlot != null)
            {
                PickupItemForDrag(slotIndex, itemInSlot);
            }
        }
        else
        {
            // Place item
            if (currentDraggedItem != null)
            {
                PlaceItemFromDrag(slotIndex);
            }
        }
    }

    void PickupItemForDrag(int slotIndex, Sprite itemSprite)
    {
        draggedFromSlot = slotIndex;
        draggedSprite = itemSprite;
        draggedMaterial = GetMaterialInSlot(slotIndex);
        
        RemoveItem(slotIndex);
        CreateDraggedItemVisual(itemSprite);
    }

    void CreateDraggedItemVisual(Sprite itemSprite)
    {
        if (canvas == null)
        {
            Debug.LogError("Canvas not assigned!");
            return;
        }

        currentDraggedItem = new GameObject("DraggedItem");
        currentDraggedItem.transform.SetParent(canvas.transform);
        currentDraggedItem.transform.SetAsLastSibling();
        
        RectTransform rt = currentDraggedItem.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);
        
        UnityEngine.UI.Image img = currentDraggedItem.AddComponent<UnityEngine.UI.Image>();
        img.sprite = itemSprite;
        img.raycastTarget = false;
        img.color = Color.white;
        
        CanvasGroup cg = currentDraggedItem.AddComponent<CanvasGroup>();
        cg.alpha = 0.8f;
        cg.blocksRaycasts = false;
        
        currentDraggedItem.transform.position = Input.mousePosition;
    }

    void PlaceItemFromDrag(int targetSlot)
    {
        Sprite targetSprite = GetItemInSlot(targetSlot);
        Material targetMaterial = GetMaterialInSlot(targetSlot);

        // Place dragged item in target
        SetItemInSlot(targetSlot, draggedSprite, draggedMaterial);

        // If target had item, swap to original slot
        if (targetSprite != null)
        {
            SetItemInSlot(draggedFromSlot, targetSprite, targetMaterial);
        }

        DestroyDraggedItemVisual();

        // Update hand if needed
        if (targetSlot == selectedHotbarSlot || draggedFromSlot == selectedHotbarSlot)
        {
            UpdateHandDisplay();
        }

        ResetDragState();
    }

    void DestroyDraggedItemVisual()
    {
        if (currentDraggedItem != null)
        {
            Destroy(currentDraggedItem);
            currentDraggedItem = null;
        }
    }

    void ResetDragState()
    {
        draggedFromSlot = -1;
        draggedSprite = null;
        draggedMaterial = null;
    }

    #endregion

    #region Item Dropping

    void DropItemFromSlot(int slotIndex)
    {
        Sprite itemSprite = GetItemInSlot(slotIndex);
        if (itemSprite == null)
            return;

        Material itemMaterial = GetMaterialInSlot(slotIndex);
        SpawnDroppedItem(itemSprite, itemMaterial);
        RemoveItem(slotIndex);
        
        if (slotIndex == selectedHotbarSlot)
        {
            UpdateHandDisplay();
        }
    }

    void DropDraggedItem()
    {
        if (draggedSprite == null || currentDraggedItem == null)
            return;

        SpawnDroppedItem(draggedSprite, draggedMaterial);
        DestroyDraggedItemVisual();
        ResetDragState();
    }

    void SpawnDroppedItem(Sprite itemSprite, Material itemMaterial)
    {
        if (dropPosition == null)
        {
            Debug.LogError("Drop position not set!");
            return;
        }

        GameObject droppedItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        droppedItem.transform.position = dropPosition.position + dropPosition.forward * 1.5f;
        droppedItem.transform.localScale = Vector3.one * 0.5f;

        // Apply material
        if (itemMaterial != null)
        {
            Renderer rend = droppedItem.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = itemMaterial;
            }
        }

        // Add pickup component
        PickupItem pickupItem = droppedItem.AddComponent<PickupItem>();
        pickupItem.inventoryIcon = itemSprite;
        pickupItem.itemMaterial = itemMaterial;

        // Add physics
        Rigidbody rb = droppedItem.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
    }

    #endregion

    #region Hand Display

    void UpdateHandDisplay()
    {
        if (currentHandItem != null)
        {
            Destroy(currentHandItem);
            currentHandItem = null;
        }

        if (handPosition == null)
            return;

        Sprite itemSprite = GetItemInSlot(selectedHotbarSlot);
        Material itemMaterial = GetMaterialInSlot(selectedHotbarSlot);

        if (itemSprite != null)
        {
            CreateHandItem(itemMaterial);
        }
    }

    void CreateHandItem(Material itemMaterial)
    {
        currentHandItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentHandItem.transform.SetParent(handPosition);
        currentHandItem.transform.localPosition = Vector3.zero;
        currentHandItem.transform.localRotation = Quaternion.identity;
        currentHandItem.transform.localScale = Vector3.one * 0.3f;

        if (itemMaterial != null)
        {
            Renderer rend = currentHandItem.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = itemMaterial;
            }
        }

        Collider col = currentHandItem.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }
    }

    #endregion
}