using UnityEngine;
using Unity.Netcode;

public class WorldPickup : NetworkBehaviour, IInteractable
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

    public bool Interact(Interactor interactor)
    {
        Debug.Log($"🎯 WorldPickup.Interact() called on {gameObject.name}");
        
        // FIXED: Get the inventory from the Player, not from the Interactor
        if (interactor.Player == null)
        {
            Debug.LogError("❌ Interactor has no Player reference!");
            return false;
        }

        // Find the player's inventory on the NetworkPlayer
        PlayerInventory inventory = interactor.Player.GetComponent<PlayerInventory>();
        
        if (inventory == null)
        {
            Debug.LogError("❌ No PlayerInventory found on the player!");
            return false;
        }

        // Add item to inventory
        bool success = inventory.AddItem(itemIcon, itemMaterial);
        
        if (success)
        {
            Debug.Log($"✅ Item added to inventory, destroying {gameObject.name}");
            
            // Handle networked destruction properly
            if (IsServer)
            {
                // If this is the server, despawn directly
                GetComponent<NetworkObject>().Despawn();
            }
            else
            {
                // If this is a client, request the server to despawn
                RequestDespawnServerRpc();
            }
            
            return true;
        }
        else
        {
            Debug.LogWarning("⚠️ Inventory full, couldn't add item");
            return false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDespawnServerRpc()
    {
        GetComponent<NetworkObject>().Despawn();
    }
}