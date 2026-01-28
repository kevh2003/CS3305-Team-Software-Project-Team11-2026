using UnityEngine;

public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item Data")]
    public Sprite inventoryIcon;
    public Material itemMaterial;
    
    void Start()
    {
        // Auto-grab material from renderer if not set
        if (itemMaterial == null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                itemMaterial = rend.material;
            }
        }
    }
    
    public bool CanInteract()
    {
        return inventoryIcon != null;
    }
    
    public bool Interact(Interactor interactor)
    {
        // Find inventory system on the interactor's GameObject
        InventorySystem inventory = interactor.GetComponent<InventorySystem>();
        
        if (inventory == null)
        {
            Debug.LogError("No InventorySystem found on player!");
            return false;
        }
        
        // Try to add item to inventory
        if (inventory.AddItem(inventoryIcon, itemMaterial))
        {
            Debug.Log("Picked up: " + gameObject.name);
            Destroy(gameObject);
            return true;
        }
        else
        {
            Debug.Log("Inventory full!");
            return false;
        }
    }
}