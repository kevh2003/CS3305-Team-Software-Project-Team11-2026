using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public sealed class PauseMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button returnToLobbyButton; // host-only
    [SerializeField] private Button leaveSessionButton;  // everyone

    private bool _open;

    private void Awake()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        if (leaveSessionButton != null) leaveSessionButton.onClick.AddListener(LeaveSession);

        SetOpen(false);
    }

    private void Update()
    {
        // Require local player to exist before allowing pause
        if (LocalPlayerReference.Instance == null)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_open) Resume();
            else Open();
        }
    }

    private void Open()
    {
        SetOpen(true);

        bool isHost = IsHost();
        if (returnToLobbyButton != null)
            returnToLobbyButton.gameObject.SetActive(isHost);

        // Disable local player controls while menu is open
        var input = LocalPlayerReference.Instance.PlayerInput;
        if (input != null) input.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Resume()
    {
        SetOpen(false);

        // Only re-enable input if player is in Game scene
        var input = LocalPlayerReference.Instance != null ? LocalPlayerReference.Instance.PlayerInput : null;
        if (input != null)
            input.enabled = (SceneManager.GetActiveScene().name == "03_Game");

        // Re-lock cursor if still in game
        if (SceneManager.GetActiveScene().name == "03_Game")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ReturnToLobby()
    {
        if (!IsHost())
            return;

        SetOpen(false);

        // Keep session alive and return everyone to lobby
        if (Services.NetSession != null)
            Services.NetSession.LoadSceneForAll(lobbySceneName);
        else
            Debug.LogWarning("NetSession is null - cannot LoadSceneForAll.");
    }

    private void LeaveSession()
    {
        // Client: disconnect self. Host: shutdown server (everyone disconnects).
        if (Services.NetSession != null)
            Services.NetSession.Shutdown();

        // Load menu locally now; others will go via disconnect listener
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetOpen(bool open)
    {
        _open = open;
        if (root != null) root.SetActive(open);
    }

    private static bool IsHost()
    {
        // Prefer NetSession if it exposes host state
        if (Services.NetSession != null)
            return Services.NetSession.IsHost;

        // Fallback (in case NetSession isn't initialized yet)
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}