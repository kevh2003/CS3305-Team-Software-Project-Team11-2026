using Unity.Netcode;
using UnityEngine;

public class ElevatorDoorButton : NetworkBehaviour, IInteractable
{
    [SerializeField] private ElevatorDoorController door;

    public bool CanInteract() => true;

    public bool Interact(Interactor interactor)
    {
        if (interactor == null || !interactor.IsOwner) return false;

        PressServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void PressServerRpc(ServerRpcParams rpcParams = default)
    {
        if (door == null)
        {
            Debug.LogError($"{name}: ElevatorDoorButton missing Door reference.");
            return;
        }

        door.ServerOpenPermanently();
    }
}