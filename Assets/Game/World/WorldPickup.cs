using UnityEngine;
using Unity.Netcode;

public class WorldPickup : MonoBehaviour, IInteractable
{
    public bool CanInteract() => true;

    public bool Interact(Interactor interactor)
    {
        if (interactor == null) return false;

        var inv = interactor.GetComponent<PlayerInventory>();
        if (inv == null) return false;

        var no = GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("WorldPickup: missing NetworkObject on world prefab ROOT.");
            return false;
        }

        var worldItem = GetComponent<WorldItem>();
        if (worldItem == null || worldItem.definition == null)
        {
            Debug.LogError("WorldPickup: missing WorldItem or definition reference.");
            return false;
        }

        inv.PickupItemServerRpc(new NetworkObjectReference(no), worldItem.definition.itemId, inv.GetSelectedSlot());
        return true;
    }
}