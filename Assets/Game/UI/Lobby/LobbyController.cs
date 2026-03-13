using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text;

/*
 * LobbyController
 * Lobby UI logic;
 * - Shows player count, join info, ready/unready, character select
 * - Host can start game (networked scene load to 03_Game)
 * - Leave returns locally to MainMenu after shutting down networking
 */

public sealed class LobbyController : MonoBehaviour
{
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text joinInfoText;
    [SerializeField] private TMP_Text readySummaryText;
    [SerializeField] private bool requireAllPlayersReadyToStart = true;

    private void Start()
    {
        if (GetComponent<LobbyAvatarSelectorUI>() == null)
            gameObject.AddComponent<LobbyAvatarSelectorUI>();
        if (GetComponent<LobbyAvatarPodiumRoster>() == null)
            gameObject.AddComponent<LobbyAvatarPodiumRoster>();

        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(Leave);

        RefreshUI();
        InvokeRepeating(nameof(RefreshUI), 0.2f, 0.5f);
    }

    private void OnDestroy()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(StartGame);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(Leave);
    }

    private void RefreshUI()
    {
        if (Services.NetSession == null) return;

        int count = Services.NetSession.ConnectedPlayers;
        GetLobbyReadyStats(out int readyCount, out int totalCount, out var players);
        if (playersText != null) playersText.text = $"Players: {count}";
        if (readySummaryText != null) readySummaryText.text = BuildReadySummaryText(players);

        bool isHost = Services.NetSession.IsHost;
        bool canStart = !requireAllPlayersReadyToStart || (totalCount > 0 && readyCount >= totalCount);
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = isHost && canStart;
        }

        if (statusText != null)
        {
            if (isHost && requireAllPlayersReadyToStart && !canStart)
                statusText.text = $"Host: waiting for ready checks ({readyCount}/{Mathf.Max(1, totalCount)}).";
            else
                statusText.text = isHost ? "Host: you can start the game." : "Client: waiting for host.";
        }

        if (joinInfoText != null)
        {
            joinInfoText.text = BuildHostJoinInfo();
        }
    }

    private static string BuildHostJoinInfo()
    {
        if (Services.NetSession == null || !Services.NetSession.IsHost || Services.NetSession is not NgoNetSession ngo)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(ngo.LastOnlineJoinCode))
            return $"Room Code: {ngo.LastOnlineJoinCode}";

        if (ngo.LastHostPort != 0)
            return $"Hosting on {ngo.LastHostIp}:{ngo.LastHostPort}";

        return string.Empty;
    }

    private void StartGame()
    {
        if (Services.NetSession == null || !Services.NetSession.IsHost) return;
        if (requireAllPlayersReadyToStart)
        {
            GetLobbyReadyStats(out int readyCount, out int totalCount, out _);
            if (totalCount <= 0 || readyCount < totalCount)
            {
                if (statusText != null)
                    statusText.text = $"Host: waiting for ready checks ({readyCount}/{Mathf.Max(1, totalCount)}).";
                return;
            }
        }

        Services.NetSession.LoadSceneForAll("03_Game");
    }

    private void Leave()
    {
        if (Services.NetSession != null)
            Services.NetSession.Shutdown();

        // Leaving is a local UI action
        SceneManager.LoadScene("01_MainMenu");
    }

    private static string BuildReadySummaryText(NetworkPlayer[] players)
    {
        if (players == null || players.Length == 0)
            return "No players connected.";

        var sb = new StringBuilder(128);

        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append("Player ")
              .Append(i + 1)
              .Append(" : ")
              .Append(p.IsReadyInLobby ? "Ready" : "Not Ready");
        }

        return sb.ToString();
    }

    private void GetLobbyReadyStats(out int readyCount, out int totalCount, out NetworkPlayer[] uniquePlayers)
    {
        readyCount = 0;
        totalCount = 0;
        uniquePlayers = System.Array.Empty<NetworkPlayer>();

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
        {
            if (Services.NetSession != null)
                totalCount = Mathf.Max(Services.NetSession.ConnectedPlayers, 0);
            return;
        }

        System.Array.Sort(players, (a, b) =>
        {
            int ownerCompare = a.OwnerClientId.CompareTo(b.OwnerClientId);
            if (ownerCompare != 0) return ownerCompare;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        var list = new System.Collections.Generic.List<NetworkPlayer>(players.Length);
        var seen = new System.Collections.Generic.HashSet<ulong>();
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.IsSpawned) continue;
            if (!seen.Add(p.OwnerClientId)) continue;

            list.Add(p);
            totalCount++;
            if (p.IsReadyInLobby) readyCount++;
        }

        if (totalCount == 0 && Services.NetSession != null)
            totalCount = Mathf.Max(Services.NetSession.ConnectedPlayers, 0);

        uniquePlayers = list.ToArray();
    }
}