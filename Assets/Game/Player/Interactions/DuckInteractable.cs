using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DuckInteractable : NetworkBehaviour, IInteractable
{
    private bool collectedServerSide;

    public bool CanInteract() => true;

    public bool Interact(Interactor interactor)
    {
        // Local player sends the request
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

        // Despawns for everyone
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}