using UnityEngine;

/// <summary>
/// Pickup item in the world that can be picked up with E key.
/// </summary>
public class WorldPickup : MonoBehaviour, IInteractable
{
    public Sprite itemIcon;
    public Material itemMaterial;

    void Start()
    {
        if (itemMaterial == null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
                itemMaterial = rend.material;
        }
    }

    public bool CanInteract()
    {
        return itemIcon != null;
    }

    public bool Interact(Interactor interactor)
    {
        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("No PlayerInventory found on player!");
            return false;
        }

        if (inventory.AddItem(itemIcon, itemMaterial))
        {
            Destroy(gameObject);
            return true;
        }

        return false;
    }
}