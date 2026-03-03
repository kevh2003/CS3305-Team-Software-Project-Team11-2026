using Unity.Netcode;
using UnityEngine;

public class PCInteractable : NetworkBehaviour, IInteractable
{
    [Header("Assignment")]
    public float holdSeconds = 10f;
    public string taskLabel = "Submit an assignment in Room 1.10";

    public bool CanInteract()
    {
        // If client already submitted this round, don't allow interacting again
        if (ObjectiveState.Instance != null && NetworkManager.Singleton != null)
        {
            ulong me = NetworkManager.Singleton.LocalClientId;
            if (ObjectiveState.Instance.HasSubmittedClient(me))
                return false;
        }
        return true;
    }

    public bool Interact(Interactor interactor)
    {
        // Interactor handles the hold; we just allow it if CanInteract is true
        return CanInteract();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitAssignmentServerRpc(ServerRpcParams rpcParams = default)
    {
        if (ObjectiveState.Instance == null) return;

        var senderId = rpcParams.Receive.SenderClientId;

        // double-submit guard
        if (ObjectiveState.Instance.ServerHasSubmitted(senderId))
            return;

        ObjectiveState.Instance.ServerRegisterAssignmentSubmit(senderId);
    }
}