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

    private Coroutine joinTimeoutRoutine;

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
        
        SetStatus("Ready");
    }

    private void SetStatus(string msg)
    {
        Debug.Log("[UI] " + msg);
        if (statusText != null) statusText.text = msg;
    }

    private bool TryReadPort(out ushort port)
    {
        port = 0;
        if (portField == null) return false;
        return ushort.TryParse(portField.text, out port);
    }
    
    private void HostLan()
    {
        SetStatus("Starting LAN host...");

        var res = Services.NetSession.HostLanAutoPort(out ushort chosenPort);

        if (res == NetStartResult.Success)
        {
            // Provides join information, will be useful for LAN list implementation in future - kev
            string ip = NetUtil.GetLocalIPv4();
            SetStatus($"Hosting on {ip}:{chosenPort}");
        }
        else
        {
            SetStatus("Host LAN: " + res);
        }
    }

    private void JoinLan()
    {
        if (joinIpField == null || string.IsNullOrWhiteSpace(joinIpField.text))
        {
            SetStatus("Enter the host IP address.");
            return;
        }
        string ip = joinIpField.text.Trim();

        if (!TryReadPort(out ushort port))
        {
            SetStatus("Enter the host port number.");
            return;
        }

        SetStatus("Joining LAN host...");
        var res = Services.NetSession.JoinLan(ip, port);

        if (res == NetStartResult.Success)
        {
            SetStatus("Joining... (waiting for connection)");

            if (joinTimeoutRoutine != null) StopCoroutine(joinTimeoutRoutine); // times out if failed to join
            joinTimeoutRoutine = StartCoroutine(JoinTimeout());
            Debug.Log("[UI] JoinTimeout coroutine started"); // DEBUG temporary - kev
        }
        else
        {
            SetStatus("Join LAN: " + res);
        }
    }

    private System.Collections.IEnumerator JoinTimeout()
    {
        const float timeoutSeconds = 10f;
        float start = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (Services.NetSession != null && Services.NetSession.IsConnected)
                yield break;

            yield return null;
        }

        Debug.Log("[UI] JoinTimeout activated - shutting down client"); // DEBUG temporary - kev

        Services.NetSession?.Shutdown();
        SetStatus("Join failed (timeout). Check IP/port and that host is running.");
    }
    
    private void HandleConnected()
    {
        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
            joinTimeoutRoutine = null;
        }

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
        Debug.Log("[UI] HandleDisconnected fired: " + reason); // DEBUG temporary - kev
        if (joinTimeoutRoutine != null)
        {
            StopCoroutine(joinTimeoutRoutine);
            joinTimeoutRoutine = null;
        }

        SetStatus("Disconnected: " + reason);
    }
}