using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Final objective station with hold validation and replicated win transition.
public class GradesRackInteractable : NetworkBehaviour, IInteractable
{
    [Header("Grades Task")]
    public float holdSeconds = 8f;
    public string taskLabel = "Change your grades";

    [Header("Win Flow")]
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private float winDelaySeconds = 3f;
    [SerializeField] private float serverInteractRange = 6f;
    [SerializeField] private float holdDurationToleranceSeconds = 0.2f;

    private static bool s_winTriggered;
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

    // Round-scoped flag reset by MatchStartResetter when a new match begins.
    public static void ServerResetGlobalWinFlag()
    {
        s_winTriggered = false;
    }

    public bool CanInteract()
    {
        if (ObjectiveState.Instance == null) return false;
        if (!ObjectiveState.Instance.ElevatorOpened.Value) return false;
        if (ObjectiveState.Instance.GradesChanged.Value) return false;
        return true;
    }

    public bool Interact(Interactor interactor) => CanInteract();

    [ServerRpc(RequireOwnership = false)]
    public void BeginGradeHoldServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
            return;

        if (ObjectiveState.Instance == null)
            return;
        if (!ObjectiveState.Instance.ElevatorOpened.Value || ObjectiveState.Instance.GradesChanged.Value)
            return;

        _holdStartedAtByClient[senderId] = Time.unscaledTime;
    }

    [ServerRpc(RequireOwnership = false)]
    public void CancelGradeHoldServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        _holdStartedAtByClient.Remove(senderId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ChangeGradesServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
        {
            Debug.LogWarning($"[GradesRackInteractable] Reject grade change from {senderId}: out of range.");
            return;
        }
        if (ObjectiveState.Instance == null)
        {
            Debug.LogWarning("[GradesRackInteractable] ObjectiveState missing, cannot change grades.");
            return;
        }
        if (!ObjectiveState.Instance.ElevatorOpened.Value)
            return;
        if (ObjectiveState.Instance.GradesChanged.Value) return;
        if (!_holdStartedAtByClient.TryGetValue(senderId, out float holdStartedAt))
            return;

        float requiredHold = Mathf.Max(0.1f, holdSeconds - Mathf.Max(0f, holdDurationToleranceSeconds));
        if ((Time.unscaledTime - holdStartedAt) < requiredHold)
            return;

        _holdStartedAtByClient.Remove(senderId);

        ObjectiveState.Instance.GradesChanged.Value = true;

        // Trigger win once
        TryTriggerWinServer();
    }

    private void TryTriggerWinServer()
    {
        if (!IsServer) return;
        if (s_winTriggered) return;

        s_winTriggered = true;

        ShowYouWinClientRpc();
        StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private System.Collections.IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSeconds(winDelaySeconds);

        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogError("[GradesRackInteractable] Network SceneManager not enabled on NetworkManager.");
            yield break;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    [ClientRpc]
    private void ShowYouWinClientRpc()
    {
        LocalPlayerReference.Instance?.GetComponent<PlayerSoundFX>()?.PlayWinSound();
        PlayerHealthUI.ShowYouWin();
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