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
            Debug.LogError("WorldPickup: missing NetworkObject on key world prefab root.");
            return false;
        }

        inv.PickupKeyServerRpc(no);
        return true;
    }
}
