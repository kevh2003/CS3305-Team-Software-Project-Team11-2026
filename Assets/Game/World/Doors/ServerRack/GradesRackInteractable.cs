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

    private static bool s_winTriggered;

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
        if (ObjectiveState.Instance == null) return;
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
}