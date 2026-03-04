using UnityEngine;
using Unity.Netcode;

public class WorldPickup : NetworkBehaviour, IInteractable
{
    public bool CanInteract()
    {
        // If player has been been despawned/disabled, don't allow interaction
        return isActiveAndEnabled;
    }

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

        // Ask server to pick it up. Server will despawn it.
        inv.PickupItemServerRpc(new NetworkObjectReference(no), worldItem.definition.itemId, inv.GetSelectedSlot());
        return true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // When the object is spawned, ensure it is visible/interactive
        SetVisible(true);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = visible;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        // disable scripts so it can’t be interacted with
        enabled = visible;

    }
}