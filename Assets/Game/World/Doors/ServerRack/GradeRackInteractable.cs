using Unity.Netcode;
using UnityEngine;

public class GradeRackInteractable : NetworkBehaviour, IInteractable
{
    [Header("Hold Settings")]
    public float holdSeconds = 10f;

    public bool CanInteract()
    {
        // Only after elevator is opened, and only once
        if (ObjectiveState.Instance == null) return false;
        if (!ObjectiveState.Instance.ElevatorOpened.Value) return false;
        if (ObjectiveState.Instance.GradesChanged.Value) return false;
        return true;
    }

    public bool Interact(Interactor interactor)
    {
        // Interactor handles the hold, same as PC
        return CanInteract();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeGradesServerRpc(ServerRpcParams rpcParams = default)
    {
        if (ObjectiveState.Instance == null) return;
        if (ObjectiveState.Instance.GradesChanged.Value) return;

        ObjectiveState.Instance.GradesChanged.Value = true;

        // TODO: trigger win flow here (end screen / return to lobby / etc.)
        Debug.Log("[Grades] Grades changed. Win!");
    }
}