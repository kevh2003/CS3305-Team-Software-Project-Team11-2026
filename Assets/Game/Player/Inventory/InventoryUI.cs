using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Connects the UI elements to the PlayerInventory component.
/// Runs on the local player only.
/// </summary>
public class InventoryUI : NetworkBehaviour
{
    private PlayerInventory inventory;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"⚙️ InventoryUI.OnNetworkSpawn() - IsOwner: {IsOwner}");

        if (!IsOwner)
        {
            Debug.Log("❌ Not owner, disabling InventoryUI");
            enabled = false;
            return;
        }

        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("❌ InventoryUI: No PlayerInventory component found!");
            return;
        }

        Debug.Log("✅ Found PlayerInventory, waiting for GameCanvas...");

        // Try to setup UI, with retries if canvas not found yet
        StartCoroutine(WaitForCanvasAndSetup());
    }

    System.Collections.IEnumerator WaitForCanvasAndSetup()
    {
        int attempts = 0;
        while (attempts < 20) // Try for 2 seconds
        {
            Canvas canvas = FindCanvasInScene();
            if (canvas != null)
            {
                Debug.Log($"✅ Found canvas on attempt {attempts + 1}, setting up UI...");
                SetupUI();
                yield break;
            }

            attempts++;
            Debug.Log($"⏳ Waiting for GameCanvas... attempt {attempts}/20");
            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError("❌ GameCanvas never appeared after 2 seconds!");
    }

    void SetupUI()
    {
        // Find canvas in scene
        Canvas canvas = FindCanvasInScene();
        if (canvas == null)
        {
            Debug.LogError("❌ InventoryUI: No Canvas found in scene!");
            return;
        }

        Debug.Log($"✅ Found Canvas: {canvas.name}");
        inventory.canvas = canvas;

        // Find panels
        Transform hotbar = canvas.transform.Find("HotbarPanel");
        Transform inventoryPanel = canvas.transform.Find("InventoryPanel");

        if (hotbar == null)
        {
            Debug.LogError("❌ InventoryUI: HotbarPanel not found!");
            Debug.Log($"Canvas children: {string.Join(", ", System.Linq.Enumerable.Select(canvas.GetComponentsInChildren<Transform>(), t => t.name))}");
            return;
        }

        if (inventoryPanel == null)
        {
            Debug.LogError("❌ InventoryUI: InventoryPanel not found!");
            return;
        }

        Debug.Log("✅ Found HotbarPanel and InventoryPanel");

        inventory.hotbarPanel = hotbar.gameObject;
        inventory.inventoryPanel = inventoryPanel.gameObject;

        // Setup hotbar slots
        inventory.hotbarSlotImages = new Image[inventory.hotbarSlots];
        for (int i = 0; i < inventory.hotbarSlots; i++)
        {
            Transform slot = hotbar.Find($"HotbarSlot_{i}");
            if (slot != null)
            {
                Debug.Log($"✅ Found HotbarSlot_{i}");
                inventory.hotbarSlotImages[i] = slot.GetComponent<Image>();

                // Add drag/drop component
                InventorySlot slotComponent = slot.gameObject.AddComponent<InventorySlot>();
                slotComponent.slotIndex = i;
                slotComponent.inventory = inventory;
            }
            else
            {
                Debug.LogError($"❌ HotbarSlot_{i} not found!");
            }
        }

        // Setup inventory slots
        inventory.inventorySlotImages = new Image[inventory.inventorySlots];
        for (int i = 0; i < inventory.inventorySlots; i++)
        {
            Transform slot = inventoryPanel.Find($"InventorySlot_{i}");
            if (slot != null)
            {
                Debug.Log($"✅ Found InventorySlot_{i}");
                inventory.inventorySlotImages[i] = slot.GetComponent<Image>();

                // Add drag/drop component
                InventorySlot slotComponent = slot.gameObject.AddComponent<InventorySlot>();
                slotComponent.slotIndex = i + inventory.hotbarSlots;
                slotComponent.inventory = inventory;
            }
            else
            {
                Debug.LogError($"❌ InventorySlot_{i} not found!");
            }
        }

        // Get player movement reference
        inventory.playerMovement = GetComponent<PlayerMovement>();
        Debug.Log($"PlayerMovement found: {inventory.playerMovement != null}");

        // Find hand and drop positions
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            Debug.Log("✅ Found camera");
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
        else
        {
            Debug.LogError("❌ No camera found!");
        }

        Transform drop = transform.Find("DropPosition");
        if (drop == null)
        {
            Debug.Log("Creating DropPosition...");
            drop = new GameObject("DropPosition").transform;
            drop.SetParent(transform);
            drop.localPosition = new Vector3(0, 1, 1);
        }

        // Find camera controller (around line 145, after camera check)
        if (cam != null)
        {
            Debug.Log("✅ Found camera");
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

            // Find camera controller script
            // Try common script names
            MonoBehaviour camController = cam.GetComponent<MonoBehaviour>();
            if (camController != null)
            {
                // Check if it's a look/camera controller (you can add more names here)
                string scriptName = camController.GetType().Name;
                if (scriptName.Contains("Look") || scriptName.Contains("Camera") || scriptName.Contains("Mouse"))
                {
                    inventory.cameraController = camController;
                    Debug.Log($"✅ Found camera controller: {scriptName}");
                }
            }
        }
        inventory.dropPosition = drop;
        Debug.Log("✅ DropPosition set");

        // Hide inventory panel initially
        inventoryPanel.gameObject.SetActive(false);
        Debug.Log("✅ Inventory panel hidden");

        // Select first slot
        inventory.SelectSlot(0);
        Debug.Log("✅ Selected slot 0");

        Debug.Log("✅✅✅ InventoryUI: Setup complete!");

    }
    Canvas FindCanvasInScene()
    {
        // First, try to find the persistent GameCanvas
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        Debug.Log($"🔍 Found {allCanvases.Length} canvases in scene");

        foreach (Canvas c in allCanvases)
        {
            Debug.Log($"   - Canvas: {c.name}");

            // Look for GameCanvas or canvas with PersistentCanvas component
            if (c.name == "GameCanvas" || c.GetComponent<PersistentCanvas>() != null)
            {
                Debug.Log($"✅ Found GameCanvas: {c.name}");
                return c;
            }
        }

        // Fallback: look for HotbarPanel child
        foreach (Canvas c in allCanvases)
        {
            if (c.transform.Find("HotbarPanel") != null)
            {
                Debug.Log($"✅ Found canvas with HotbarPanel: {c.name}");
                return c;
            }
        }

        Debug.LogError("❌ Could not find GameCanvas!");
        return null;
    }
}