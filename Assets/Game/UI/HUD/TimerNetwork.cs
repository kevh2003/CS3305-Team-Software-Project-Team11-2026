using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TimerNetwork : NetworkBehaviour
{
    public static TimerNetwork Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "03_Game";
    [SerializeField] private string lobbySceneName = "02_Lobby";

    [Header("Timer")]
    [SerializeField] private int matchSeconds = 10 * 60;   // 10 minutes
    [SerializeField] private float gameOverDelaySeconds = 3f;

    public NetworkVariable<int> RemainingSeconds =
        new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Coroutine tickRoutine;
    private bool gameOverTriggered;

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            if (IsServer) NetworkObject.Despawn();
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Start/stop based on network scene loads (best signal in NGO)
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        // Fallback (if NGO scene events aren't firing for your setup)
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        // If we already spawned inside the game scene, start immediately
        if (IsServer && SceneManager.GetActiveScene().name == gameSceneName)
            StartMatchTimerServer();
    }

    private void OnDestroy()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        if (Instance == this) Instance = null;
    }

    // NGO scene load completion (preferred)
    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServer) return;

        if (sceneName == gameSceneName)
            StartMatchTimerServer();
        else if (sceneName == lobbySceneName)
            StopAndResetServer();
    }

    // Unity fallback (works even if NGO event doesn’t fire)
    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (!IsServer) return;

        if (newScene.name == gameSceneName)
            StartMatchTimerServer();
        else if (newScene.name == lobbySceneName)
            StopAndResetServer();
    }

    private void StartMatchTimerServer()
    {
        if (!IsServer) return;

        gameOverTriggered = false;

        // restart cleanly
        if (tickRoutine != null)
            StopCoroutine(tickRoutine);

        RemainingSeconds.Value = matchSeconds;
        tickRoutine = StartCoroutine(TickDownServer());
        Debug.Log($"[TimerNetwork] Started timer: {matchSeconds}s");
    }

    private IEnumerator TickDownServer()
    {
        // Tick once per second (authoritative on server)
        while (IsServer && !gameOverTriggered && RemainingSeconds.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            RemainingSeconds.Value = Mathf.Max(0, RemainingSeconds.Value - 1);
        }

        if (IsServer && !gameOverTriggered && RemainingSeconds.Value <= 0)
        {
            TriggerGameOverServer();
        }
    }

    private void StopAndResetServer()
    {
        if (!IsServer) return;

        gameOverTriggered = false;

        if (tickRoutine != null)
        {
            StopCoroutine(tickRoutine);
            tickRoutine = null;
        }

        RemainingSeconds.Value = 0;
        Debug.Log("[TimerNetwork] Stopped/reset timer");
    }

    private void TriggerGameOverServer()
    {
        if (!IsServer) return;
        if (gameOverTriggered) return;

        gameOverTriggered = true;

        if (tickRoutine != null)
        {
            StopCoroutine(tickRoutine);
            tickRoutine = null;
        }

        Debug.Log("[TimerNetwork] Timer reached 0. Game Over.");

        ShowGameOverClientRpc();
        StartCoroutine(ReturnToLobbyAfterDelayServer());
    }

    private IEnumerator ReturnToLobbyAfterDelayServer()
    {
        yield return new WaitForSeconds(gameOverDelaySeconds);

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null) yield break;

        nm.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    [ClientRpc]
    private void ShowGameOverClientRpc()
    {
        PlayerHealthUI.ShowGameOver();
    }
}