using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DuckInteractable : NetworkBehaviour, IInteractable
{
    private bool collectedServerSide;

    public bool CanInteract() => true;

    public bool Interact(Interactor interactor)
    {
        // Only the local player should request the pickup
        if (interactor == null || !interactor.IsOwner)
            return false;

        CollectServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollectServerRpc(ServerRpcParams rpcParams = default)
    {
        if (collectedServerSide) return;
        collectedServerSide = true;

        if (ObjectiveState.Instance != null)
            ObjectiveState.Instance.RegisterDuckServerRpc();

        HideDuckClientRpc();

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(false);
        else
            gameObject.SetActive(false);
    }

    [ClientRpc]
    private void HideDuckClientRpc()
    {
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        gameObject.SetActive(false);
    }
}