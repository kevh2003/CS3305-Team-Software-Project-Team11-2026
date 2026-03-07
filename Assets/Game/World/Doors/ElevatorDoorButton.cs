using Unity.Netcode;
using UnityEngine;

public class ElevatorDoorButton : NetworkBehaviour, IInteractable
{
    [SerializeField] private ElevatorDoorController door;
    [SerializeField] private float serverInteractRange = 4f;

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
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId)) return;

        if (door == null)
        {
            Debug.LogError($"{name}: ElevatorDoorButton missing Door reference.");
            return;
        }

        door.ServerOpenPermanently();
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= serverInteractRange;
    }
}