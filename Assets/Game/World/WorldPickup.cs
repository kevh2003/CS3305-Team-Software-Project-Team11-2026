using UnityEngine;
using Unity.Netcode;

public class WorldPickup : MonoBehaviour, IInteractable
{
    public Sprite itemIcon;
    public Material itemMaterial;
    
    private bool isPickedUp = false;

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
        return !isPickedUp;
    }

    public bool Interact(Interactor interactor)
    {
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        
        if (inventory == null)
        {
            return false;
        }

        bool success = inventory.AddItem(itemIcon, itemMaterial, this.gameObject);
        
        if (success)
        {
            isPickedUp = true;
            // DON'T hide it - let inventory move it to hand
        }
        
        return success;
    }
}