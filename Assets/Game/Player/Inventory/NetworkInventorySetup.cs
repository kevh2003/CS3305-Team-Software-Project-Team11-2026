using UnityEngine;
using Unity.Netcode;

/// Sets up inventory UI references for networked players
/// Only the local player should interact with UI
public class NetworkInventorySetup : NetworkBehaviour
{
    private InventorySystem inventorySystem;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only setup UI for the local player
        if (!IsOwner) return;

        inventorySystem = GetComponent<InventorySystem>();
        if (inventorySystem == null)
        {
            Debug.LogError("InventorySystem component not found!");
            return;
        }

        SetupInventoryUI();
    }

    void SetupInventoryUI()
    {
        // Find the UI elements in the scene
        GameObject canvas = GameObject.Find("InventoryCanvas");
        if (canvas == null)
        {
            Debug.LogError("InventoryCanvas not found in scene!");
            return;
        }

        // Find panels
        Transform hotbarPanel = canvas.transform.Find("HotbarPanel");
        Transform inventoryPanel = canvas.transform.Find("InventoryPanel");

        if (hotbarPanel == null || inventoryPanel == null)
        {
            Debug.LogError("Hotbar or Inventory panel not found!");
            return;
        }

        // Assign UI references
        inventorySystem.canvas = canvas.GetComponent<Canvas>();
        inventorySystem.hotbarPanel = hotbarPanel.gameObject;
        inventorySystem.inventoryPanel = inventoryPanel.gameObject;

        // Setup hotbar slots
        inventorySystem.hotbarSlots.Clear();
        for (int i = 0; i < 2; i++)
        {
            Transform slot = hotbarPanel.Find("HotbarSlot_" + i);
            if (slot != null)
            {
                UnityEngine.UI.Image img = slot.GetComponent<UnityEngine.UI.Image>();
                inventorySystem.hotbarSlots.Add(img);

                // Ensure outline component exists
                UnityEngine.UI.Outline outline = slot.GetComponent<UnityEngine.UI.Outline>();
                if (outline == null)
                {
                    outline = slot.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = new Color32(139, 0, 0, 255);
                    outline.effectDistance = new Vector2(3, 3);
                }
                outline.enabled = false; // Start disabled
            }
        }

        // Setup inventory slots
        inventorySystem.inventorySlots.Clear();
        for (int i = 0; i < 3; i++)
        {
            Transform slot = inventoryPanel.Find("InventorySlot_" + i);
            if (slot != null)
            {
                inventorySystem.inventorySlots.Add(slot.GetComponent<UnityEngine.UI.Image>());
            }
        }

        // Find PlayerMovement component
        inventorySystem.playerMovement = GetComponent<PlayerMovement>();

        // Setup hand and drop positions
        Transform cameraTransform = GetComponentInChildren<Camera>()?.transform;
        if (cameraTransform != null)
        {
            Transform handPos = cameraTransform.Find("HandPosition");
            if (handPos != null)
            {
                inventorySystem.handPosition = handPos;
            }
        }

        Transform dropPos = transform.Find("DropPosition");
        if (dropPos != null)
        {
            inventorySystem.dropPosition = dropPos;
        }

        Debug.Log("Inventory UI setup complete for local player!");
    }
}