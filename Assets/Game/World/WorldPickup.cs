using UnityEngine;

public class WorldPickup : MonoBehaviour, IInteractable
{
    public Sprite itemIcon;
    public Material itemMaterial;

    void Awake()
    {
        // Auto-grab material from renderer if not assigned
        if (itemMaterial == null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                itemMaterial = rend.material;
                Debug.Log($"✅ WorldPickup: Auto-assigned material from {gameObject.name}");
            }
        }
    }

    public bool CanInteract()
    {
        return true;
    }

    public bool Interact(Interactor interactor)  // Changed to return bool
    {
        Debug.Log($"🎯 WorldPickup.Interact() called on {gameObject.name}");
        
        // Find the player's inventory
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        
        if (inventory == null)
        {
            Debug.LogError("❌ No PlayerInventory found on interactor!");
            return false;  // Return false on failure
        }

        // Add item to inventory
        bool success = inventory.AddItem(itemIcon, itemMaterial);
        
        if (success)
        {
            Debug.Log($"✅ Item added to inventory, destroying {gameObject.name}");
            Destroy(gameObject);
            return true;  // Return true on success
        }
        else
        {
            Debug.LogWarning("⚠️ Inventory full, couldn't add item");
            return false;  // Return false if inventory full
        }
    }
}