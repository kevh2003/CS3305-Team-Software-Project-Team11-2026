using UnityEngine;
using Unity.Netcode;

public class WorldPickup : MonoBehaviour, IInteractable
{
    public Sprite itemIcon;
    public Material itemMaterial;
    public GameObject itemPrefab; // Reference to itself for respawning
    
    void Awake()
    {
        if (itemMaterial == null)
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                itemMaterial = rend.material;
            }
        }
    }

    public bool CanInteract()
    {
        return true; // Always interactable
    }

    public bool Interact(Interactor interactor)
    {
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        
        if (inventory == null)
        {
            return false;
        }

        // Store reference to this prefab before adding to inventory
        bool success = inventory.AddItem(itemIcon, itemMaterial, gameObject);
        
        if (success)
        {
            // Hide the world object (but don't destroy it - inventory manages it now)
            gameObject.SetActive(false);
        }
        
        return success;
    }
}