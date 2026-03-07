using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GradesRackInteractable : NetworkBehaviour, IInteractable
{
    [Header("Grades Task")]
    public float holdSeconds = 8f;
    public string taskLabel = "Change your grades";

    [Header("Win Flow")]
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private float winDelaySeconds = 3f;
    [SerializeField] private float serverInteractRange = 6f;

    private static bool s_winTriggered;
    private Collider[] _interactionColliders;

    private void Awake()
    {
        _interactionColliders = GetComponentsInChildren<Collider>(true);
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
        if (ObjectiveState.Instance.GradesChanged.Value) return;

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