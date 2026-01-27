using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/*
 * LobbyController
 * 
 * Lobby UI logic, currently;
 * - Shows player count
 * - Host can start game (networked scene load to 03_Game)
 * - Leave returns locally to MainMenu after shutting down networking
 */

public sealed class LobbyController : MonoBehaviour
{
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text statusText;

    private void Start()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(Leave);

        RefreshUI();
        InvokeRepeating(nameof(RefreshUI), 0.2f, 0.5f);
    }

    private void RefreshUI()
    {
        if (Services.NetSession == null) return;

        int count = Services.NetSession.ConnectedPlayers;
        if (playersText != null) playersText.text = $"Players: {count}";

        bool isHost = Services.NetSession.IsHost;
        if (startGameButton != null) startGameButton.gameObject.SetActive(isHost);

        if (statusText != null)
            statusText.text = isHost ? "Host: you can start the game." : "Client: waiting for host.";
    }

    private void StartGame()
    {
        if (Services.NetSession == null || !Services.NetSession.IsHost) return;
        Services.NetSession.LoadSceneForAll("03_Game");
    }

    private void Leave()
    {
        if (Services.NetSession != null)
            Services.NetSession.Shutdown();

        // Leaving is a local UI action
        SceneManager.LoadScene("01_MainMenu");
    }
}