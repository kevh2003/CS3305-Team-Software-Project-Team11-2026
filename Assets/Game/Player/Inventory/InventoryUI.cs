using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// MULTIPLAYER-SAFE VERSION with Multi-Scene Support
/// Creates a separate UI instance for each local player.
/// Handles camera activation delays across scene changes.
/// Remote players don't see or interact with your UI.
/// </summary>

public class InventoryUI : NetworkBehaviour
{
    private PlayerInventory inventory;
    private Canvas localCanvas; // Each player gets their own canvas reference

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"InventoryUI.OnNetworkSpawn() - IsOwner: {IsOwner}");

        if (!IsOwner)
        {
            Debug.Log("Not owner, disabling InventoryUI");
            enabled = false;
            return;
        }

        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("InventoryUI: No PlayerInventory component found!");
            return;
        }

        Debug.Log("Found PlayerInventory, creating local UI...");

        // Create UI for this player only
        CreateLocalUI();
    }

    void CreateLocalUI()
    {
        // Create a new Canvas specifically for THIS player
        GameObject canvasObj = new GameObject($"PlayerInventory_{OwnerClientId}");
        localCanvas = canvasObj.AddComponent<Canvas>();
        localCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        localCanvas.sortingOrder = 100; // Above other UI
        
        // Add required components
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Don't destroy when loading new scenes
        DontDestroyOnLoad(canvasObj);

        Debug.Log($"Created local canvas for player {OwnerClientId}");

        // Create the UI structure
        CreateHotbarPanel();
        CreateInventoryPanel();

        // Get player movement reference
        inventory.playerMovement = GetComponent<PlayerMovement>();

        // Setup hand and drop positions (with retry for camera)
        StartCoroutine(SetupHandAndDropPositionsWithRetry());

        // Hide inventory panel initially
        inventory.inventoryPanel.SetActive(false);

        // Select first slot
        inventory.SelectSlot(0);

        Debug.Log("InventoryUI: Local UI setup complete!");
    }

    void CreateHotbarPanel()
    {
        // Create hotbar panel
        GameObject hotbarPanel = new GameObject("HotbarPanel");
        hotbarPanel.transform.SetParent(localCanvas.transform, false);
        
        RectTransform hotbarRect = hotbarPanel.AddComponent<RectTransform>();
        hotbarRect.anchorMin = new Vector2(0.5f, 0f); // Bottom center
        hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        hotbarRect.pivot = new Vector2(0.5f, 0f);
        hotbarRect.anchoredPosition = new Vector2(0, 20);
        hotbarRect.sizeDelta = new Vector2(200, 60);

        Image hotbarImage = hotbarPanel.AddComponent<Image>();
        hotbarImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent background

        inventory.hotbarPanel = hotbarPanel;

        // Create hotbar slots
        inventory.hotbarSlotImages = new Image[inventory.hotbarSlots];
        for (int i = 0; i < inventory.hotbarSlots; i++)
        {
            GameObject slot = new GameObject($"HotbarSlot_{i}");
            slot.transform.SetParent(hotbarPanel.transform, false);

            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(50, 50);
            slotRect.anchoredPosition = new Vector2(-30 + (i * 60), 5);

            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(1, 1, 1, 0.3f); // Semi-transparent slot

            inventory.hotbarSlotImages[i] = slotImage;

            // Add drag/drop component
            InventorySlot slotComponent = slot.AddComponent<InventorySlot>();
            slotComponent.slotIndex = i;
            slotComponent.inventory = inventory;

            Debug.Log($"Created HotbarSlot_{i}");
        }
    }

    void CreateInventoryPanel()
    {
        // Create inventory panel
        GameObject invPanel = new GameObject("InventoryPanel");
        invPanel.transform.SetParent(localCanvas.transform, false);

        RectTransform invRect = invPanel.AddComponent<RectTransform>();
        invRect.anchorMin = new Vector2(0.5f, 0.5f); // Center
        invRect.anchorMax = new Vector2(0.5f, 0.5f);
        invRect.pivot = new Vector2(0.5f, 0.5f);
        invRect.anchoredPosition = Vector2.zero;
        invRect.sizeDelta = new Vector2(300, 250);

        Image invImage = invPanel.AddComponent<Image>();
        invImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark background

        inventory.inventoryPanel = invPanel;

        // Create inventory slots
        inventory.inventorySlotImages = new Image[inventory.inventorySlots];
        for (int i = 0; i < inventory.inventorySlots; i++)
        {
            GameObject slot = new GameObject($"InventorySlot_{i}");
            slot.transform.SetParent(invPanel.transform, false);

            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(50, 50);
            
            // Arrange in grid (2 columns)
            int row = i / 2;
            int col = i % 2;
            slotRect.anchoredPosition = new Vector2(-50 + (col * 110), 50 - (row * 70));

            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(1, 1, 1, 0.3f); // Semi-transparent slot

            inventory.inventorySlotImages[i] = slotImage;

            // Add drag/drop component
            InventorySlot slotComponent = slot.AddComponent<InventorySlot>();
            slotComponent.slotIndex = i + inventory.hotbarSlots;
            slotComponent.inventory = inventory;

            Debug.Log($"Created InventorySlot_{i}");
        }
    }

    IEnumerator SetupHandAndDropPositionsWithRetry()
    {
        // Camera might not be active yet (disabled in lobby scene)
        // Keep trying for up to 5 seconds
        int attempts = 0;
        int maxAttempts = 50; // 5 seconds
        
        while (attempts < maxAttempts)
        {
            Camera cam = TryFindCamera();
            
            if (cam != null)
            {
                Debug.Log($"Found camera on attempt {attempts + 1}");
                SetupHandPosition(cam);
                break;
            }
            
            attempts++;
            yield return new WaitForSeconds(0.1f);
        }
        
        if (attempts >= maxAttempts)
        {
            Debug.LogWarning("Camera not found after 5 seconds. Hand display will not work until camera is active.");
        }
        
        // Setup drop position (doesn't need camera)
        SetupDropPosition();
    }

    Camera TryFindCamera()
    {
        // Try multiple methods to find the camera
        
        // Method 1: GetComponentInChildren (include inactive)
        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam != null && cam.gameObject.activeInHierarchy)
        {
            return cam;
        }
        
        // Method 2: Check LocalPlayerReference
        if (LocalPlayerReference.Instance != null)
        {
            cam = LocalPlayerReference.Instance.PlayerCamera;
            if (cam != null && cam.gameObject.activeInHierarchy)
            {
                Debug.Log("Found camera via LocalPlayerReference");
                return cam;
            }
        }
        
        // Method 3: Find by NetworkPlayer reference
        NetworkPlayer netPlayer = GetComponent<NetworkPlayer>();
        if (netPlayer != null)
        {
            cam = netPlayer.GetComponentInChildren<Camera>(true);
            if (cam != null && cam.gameObject.activeInHierarchy)
            {
                return cam;
            }
        }
        
        return null;
    }

    void SetupHandPosition(Camera cam)
    {
        Transform hand = cam.transform.Find("HandPosition");
        if (hand == null)
        {
            Debug.Log("Creating HandPosition...");
            hand = new GameObject("HandPosition").transform;
            hand.SetParent(cam.transform);
            hand.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
        }
        inventory.handPosition = hand;
        Debug.Log("✅ HandPosition set");
    }

    void SetupDropPosition()
    {
        Transform drop = transform.Find("DropPosition");
        if (drop == null)
        {
            Debug.Log("Creating DropPosition...");
            drop = new GameObject("DropPosition").transform;
            drop.SetParent(transform);
            drop.localPosition = new Vector3(0, 1, 1);
        }
        inventory.dropPosition = drop;
        Debug.Log("DropPosition set");
    }

    void OnDestroy()
    {
        // Clean up the UI when player disconnects
        if (localCanvas != null)
        {
            Destroy(localCanvas.gameObject);
        }
    }
}