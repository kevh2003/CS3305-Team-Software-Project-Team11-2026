using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * MainMenuController
 * 
 * Main menu UI logic
 * Calls into Services.NetSession (INetSession) and waits for OnConnected
 * Host triggers the networked scene load to Lobby; clients follow automatically
 */

public sealed class MainMenuController : MonoBehaviour
{
    [Header("LAN")]
    [SerializeField] private Button hostLanButton;
    [SerializeField] private Button joinLanButton;
    [SerializeField] private TMP_InputField joinIpField;
    [SerializeField] private TMP_InputField portField;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    private const ushort DefaultPort = 7777;

    private void OnEnable()
    {
        if (Services.NetSession != null)
        {
            Services.NetSession.OnConnected += HandleConnected;
            Services.NetSession.OnDisconnected += HandleDisconnected;
        }
    }

    private void OnDisable()
    {
        if (Services.NetSession != null)
        {
            Services.NetSession.OnConnected -= HandleConnected;
            Services.NetSession.OnDisconnected -= HandleDisconnected;
        }
    }

    private void Start()
    {
        hostLanButton.onClick.AddListener(HostLan);
        joinLanButton.onClick.AddListener(JoinLan);

        // Check if services are ready
        if (Services.NetSession == null)
        {
            SetStatus("Initializing network...");
            hostLanButton.interactable = false;
            joinLanButton.interactable = false;

            // Check again in a moment
            Invoke(nameof(CheckServicesReady), 0.5f);
        }
        else
        {
            SetStatus("Ready");
        }
    }

    private void CheckServicesReady()
    {
        if (Services.NetSession != null)
        {
            SetStatus("Ready");
            hostLanButton.interactable = true;
            joinLanButton.interactable = true;
        }
        else
        {
            SetStatus("Still initializing...");
            Invoke(nameof(CheckServicesReady), 0.5f);
        }
    }

    private void SetStatus(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (statusText != null) statusText.text = msg;
    }

    private ushort ReadPort()
    {
        if (portField != null && ushort.TryParse(portField.text, out var p)) return p;
        return DefaultPort;
    }

    private void HostLan()
    {
        SetStatus("Starting LAN host...");

        if (Services.NetSession == null)
        {
            Debug.LogError("❌ Services.NetSession is null! Services not initialized yet.");
            SetStatus("Error: Network not ready. Wait a moment and try again.");
            return;
        }

        var res = Services.NetSession.HostLan(ReadPort());
        SetStatus("Host LAN: " + res);
    }

    private void JoinLan()
    {
        string ip = (joinIpField != null && !string.IsNullOrWhiteSpace(joinIpField.text))
            ? joinIpField.text
            : "127.0.0.1";

        SetStatus("Joining LAN host...");

        if (Services.NetSession == null)
        {
            Debug.LogError("❌ Services.NetSession is null! Services not initialized yet.");
            SetStatus("Error: Network not ready. Wait a moment and try again.");
            return;
        }

        var res = Services.NetSession.JoinLan(ip, ReadPort());
        SetStatus("Join LAN: " + res);
    }

    private void HandleConnected()
    {
        // Host drives the network scene load; clients follow
        if (Services.NetSession.IsHost)
        {
            SetStatus("Connected (Host). Loading Lobby...");
            Services.NetSession.LoadSceneForAll("02_Lobby");
        }
        else
        {
            SetStatus("Connected (Client). Waiting for host...");
        }
    }

    private void HandleDisconnected(string reason)
    {
        SetStatus("Disconnected: " + reason);
    }
}