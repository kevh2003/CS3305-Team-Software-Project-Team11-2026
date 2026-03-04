using Unity.Netcode;
using UnityEngine;

public class GradesRackInteractable : NetworkBehaviour, IInteractable
{
    [Header("Grades Task")]
    public float holdSeconds = 8f;
    public string taskLabel = "Change your grades";

    public bool CanInteract()
    {
        if (ObjectiveState.Instance == null) return false;

        // Only after elevator is opened
        if (!ObjectiveState.Instance.ElevatorOpened.Value) return false;

        // Once done, don't allow again
        if (ObjectiveState.Instance.GradesChanged.Value) return false;

        return true;
    }

    public bool Interact(Interactor interactor)
    {
        // Interactor handles hold timing; we just say if it's allowed
        return CanInteract();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeGradesServerRpc(ServerRpcParams rpcParams = default)
    {
        if (ObjectiveState.Instance == null) return;

        // Double-complete guard
        if (ObjectiveState.Instance.GradesChanged.Value) return;

        ObjectiveState.Instance.GradesChanged.Value = true;
    }
}