using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Assignment submission station with server-validated hold completion.
public class PCInteractable : NetworkBehaviour, IInteractable
{
    [Header("Assignment")]
    public float holdSeconds = 10f;
    public string taskLabel = "Submit an assignment in Room 1.10";
    [SerializeField] private float serverInteractRange = 6f;
    [SerializeField] private float holdDurationToleranceSeconds = 0.2f;

    private Collider[] _interactionColliders;
    private readonly Dictionary<ulong, float> _holdStartedAtByClient = new();

    private void Awake()
    {
        _interactionColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnDisable()
    {
        _holdStartedAtByClient.Clear();
    }

    public bool CanInteract()
    {
        if (ObjectiveState.Instance != null && !ObjectiveState.Instance.IsWifiObjectiveCompleteClient())
            return false;

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
    public void BeginSubmitHoldServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
            return;

        if (ObjectiveState.Instance == null || !ObjectiveState.Instance.IsWifiObjectiveCompleteServer())
            return;

        if (ObjectiveState.Instance.ServerHasSubmitted(senderId))
        {
            _holdStartedAtByClient.Remove(senderId);
            return;
        }

        _holdStartedAtByClient[senderId] = Time.unscaledTime;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CancelSubmitHoldServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        _holdStartedAtByClient.Remove(senderId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitAssignmentServerRpc(ServerRpcParams rpcParams = default)
    {
        var senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
        {
            Debug.LogWarning($"[PCInteractable] Reject submit from {senderId}: out of range.");
            return;
        }
        if (ObjectiveState.Instance == null)
        {
            Debug.LogWarning("[PCInteractable] ObjectiveState missing, cannot submit assignment.");
            return;
        }

        if (!ObjectiveState.Instance.IsWifiObjectiveCompleteServer())
        {
            Debug.LogWarning($"[PCInteractable] Reject submit from {senderId}: WiFi objective not complete.");
            return;
        }

        // double-submit guard
        if (ObjectiveState.Instance.ServerHasSubmitted(senderId))
            return;

        if (!_holdStartedAtByClient.TryGetValue(senderId, out float holdStartedAt))
            return;

        float requiredHold = Mathf.Max(0.1f, holdSeconds - Mathf.Max(0f, holdDurationToleranceSeconds));
        if ((Time.unscaledTime - holdStartedAt) < requiredHold)
            return;

        _holdStartedAtByClient.Remove(senderId);
        ObjectiveState.Instance.ServerRegisterAssignmentSubmit(senderId);
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        Vector3 playerPos = client.PlayerObject.transform.position;
        float maxSqr = serverInteractRange * serverInteractRange;
        float bestSqr = float.PositiveInfinity;

        if (_interactionColliders != null)
        {
            for (int i = 0; i < _interactionColliders.Length; i++)
            {
                var col = _interactionColliders[i];
                if (col == null) continue;

                Vector3 closest = col.ClosestPoint(playerPos);
                float sqr = (closest - playerPos).sqrMagnitude;
                if (sqr < bestSqr) bestSqr = sqr;
            }
        }

        if (bestSqr < float.PositiveInfinity)
            return bestSqr <= maxSqr;

        return (transform.position - playerPos).sqrMagnitude <= maxSqr;
    }
}