using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string LobbySceneName = "02_Lobby";
    private const string PrefSensitivity = "settings_sensitivity";
    private const string PrefFullscreen = "settings_fullscreen";
    private const string PrefWifiLoopVolume = "settings_wifi_loop_volume";
    private const float MinSensitivity = 0.02f;
    private const float MaxSensitivity = 2.00f;
    private const float DefaultSensitivity = 0.12f;
    private const float MinBrightness = -2.0f;
    private const float MaxBrightness = 2.0f;
    private const float DefaultBrightness = 1f;
    private const float MinWifiLoopVolume = 0f;
    private const float MaxWifiLoopVolume = 1f;
    private const float DefaultWifiLoopVolume = 0.25f;

    [Header("Main Buttons")]
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Multiplayer Buttons")]
    [SerializeField] private Button hostOnlineButton;
    [SerializeField] private Button hostLanButton;
    [SerializeField] private Button joinOnlineButton;
    [SerializeField] private Button joinLanButton;

    [Header("Join Prompt")]
    [SerializeField] private RectTransform joinPromptPanel;
    [SerializeField] private TMP_InputField joinIpField;
    [SerializeField] private TMP_InputField portField;
    [SerializeField] private TMP_InputField roomCodeField;
    [SerializeField] private Button confirmJoinLanButton;
    [SerializeField] private Button confirmJoinOnlineButton;
    [SerializeField] private Button cancelJoinPromptButton;

    [Header("Settings")]
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private TMP_Text brightnessLabel;
    [SerializeField] private TMP_Text sensitivityLabel;
    [SerializeField] private TMP_Text wifiLoopVolumeLabel;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider wifiLoopVolumeSlider;
    [SerializeField] private Button fullscreenModeButton;
    [SerializeField] private Button applySettingsButton;
    [SerializeField] private Button closeSettingsButton;

    [Header("Status")]
    [SerializeField] private Image modalDimmer;
    [SerializeField] private TMP_Text statusText;

    private Coroutine joinTimeoutRoutine;
    private Coroutine statusHideRoutine;
    private bool fullscreenEnabled;
    private bool isMultiplayerOpen;
    private bool isJoinPromptOpen;
    private bool isSettingsOpen;
    private float appliedSensitivity = DefaultSensitivity;
    private float appliedBrightness = DefaultBrightness;
    private float appliedWifiLoopVolume = DefaultWifiLoopVolume;
    private bool appliedFullscreen;

    private void OnEnable()
    {
        if (Services.NetSession == null) return;
        Services.NetSession.OnConnected += HandleConnected;
        Services.NetSession.OnDisconnected += HandleDisconnected;
    }

    private void OnDisable()
    {
        if (Services.NetSession != null)
        {
            Services.NetSession.OnConnected -= HandleConnected;
            Services.NetSession.OnDisconnected -= HandleDisconnected;
        }

        if (joinTimeoutRoutine != null) StopCoroutine(joinTimeoutRoutine);
        if (statusHideRoutine != null) StopCoroutine(statusHideRoutine);
        joinTimeoutRoutine = null;
        statusHideRoutine = null;
    }

    private void Start()
    {
        WireUi();
        ConfigureSlider(brightnessSlider, MinBrightness, MaxBrightness);
        ConfigureSlider(sensitivitySlider, MinSensitivity, MaxSensitivity);
        ConfigureSlider(wifiLoopVolumeSlider, MinWifiLoopVolume, MaxWifiLoopVolume);
        LoadSettingsFromPrefs();
        HideMultiplayer();
        HideJoinPrompt();
        HideSettings();
        HideStatusInstant();
    }

    private void WireUi()
    {
        BindButton(multiplayerButton, ShowMultiplayer);
        BindButton(hostOnlineButton, OnHostOnlineClicked);
        BindButton(hostLanButton, HostLan);
        BindButton(joinOnlineButton, ShowJoinOnlinePrompt);
        BindButton(joinLanButton, ShowJoinLanPrompt);
        BindButton(settingsButton, ShowSettings);
        BindButton(quitButton, QuitGame);

        BindButton(confirmJoinLanButton, JoinLan);
        BindButton(confirmJoinOnlineButton, OnJoinOnlineClicked);
        BindButton(cancelJoinPromptButton, HideJoinPrompt);

        BindButton(fullscreenModeButton, ToggleFullscreenMode);
        BindButton(applySettingsButton, ApplySettings);
        BindButton(closeSettingsButton, OnCloseButtonClicked);
        BindSlider(brightnessSlider, HandleBrightnessSliderChanged);
        BindSlider(wifiLoopVolumeSlider, HandleWifiLoopVolumeSliderChanged);
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null || action == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void ConfigureSlider(Slider slider, float min, float max)
    {
        if (slider == null) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
    }

    private static void BindSlider(Slider slider, UnityAction<float> action)
    {
        if (slider == null || action == null) return;
        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    private void HandleBrightnessSliderChanged(float value)
    {
        GlobalBrightnessManager.SetBrightness(value, false);
    }

    private void HandleWifiLoopVolumeSliderChanged(float value)
    {
        ApplyWifiLoopVolumeToLocalPlayer(Mathf.Clamp(value, MinWifiLoopVolume, MaxWifiLoopVolume), persist: false);
    }

    private void SetStatus(string message, float duration = 2.25f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Debug.Log("[MainMenu] " + message);
        ShowTransientStatus(message, duration);
    }

    private void ShowTransientStatus(string message, float duration = 2.25f)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.gameObject.SetActive(true);

        if (statusHideRoutine != null)
            StopCoroutine(statusHideRoutine);
        statusHideRoutine = StartCoroutine(HideStatusAfter(duration));
    }

    private IEnumerator HideStatusAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideStatusInstant();
        statusHideRoutine = null;
    }

    private void HideStatusInstant()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void SetMainButtonsVisible(bool visible)
    {
        SetActive(multiplayerButton, visible);
        SetActive(settingsButton, visible);
        SetActive(quitButton, visible);
    }

    private void ApplyModalState()
    {
        bool hideMainButtons = isMultiplayerOpen || isJoinPromptOpen || isSettingsOpen;
        SetMainButtonsVisible(!hideMainButtons);
        SetActive(modalDimmer, false);
    }

    private void SetMultiplayerOptionsVisible(bool visible)
    {
        SetActive(hostOnlineButton, visible);
        SetActive(hostLanButton, visible);
        SetActive(joinOnlineButton, visible);
        SetActive(joinLanButton, visible);
        SetActive(closeSettingsButton, visible);

        if (visible)
        {
            BringToFront(hostOnlineButton);
            BringToFront(hostLanButton);
            BringToFront(joinOnlineButton);
            BringToFront(joinLanButton);
            BringToFront(closeSettingsButton);
        }
    }

    private static void SetPanelVisible(RectTransform panel, bool visible)
    {
        if (panel != null) panel.gameObject.SetActive(visible);
    }

    private static void SetActive(Component c, bool visible)
    {
        if (c != null) c.gameObject.SetActive(visible);
    }

    private static void BringToFront(Component c)
    {
        if (c != null) c.transform.SetAsLastSibling();
    }

    private static void UpdatePlaceholder(TMP_InputField field, string text)
    {
        if (field?.placeholder is TMP_Text placeholder) placeholder.text = text;
    }

    private void ShowJoinLanPrompt()
    {
        HideSettings();
        if (isMultiplayerOpen)
            SetMultiplayerOptionsVisible(false);
        isJoinPromptOpen = true;
        SetPanelVisible(joinPromptPanel, true);
        SetActive(joinIpField, true);
        SetActive(portField, true);
        SetActive(roomCodeField, false);
        SetActive(confirmJoinLanButton, true);
        SetActive(confirmJoinOnlineButton, false);
        SetActive(cancelJoinPromptButton, true);
        UpdatePlaceholder(joinIpField, "Host IP (e.g. 192.168.1.10)");
        joinIpField?.Select();
        ApplyModalState();
    }

    private void ShowJoinOnlinePrompt()
    {
        HideSettings();
        if (isMultiplayerOpen)
            SetMultiplayerOptionsVisible(false);
        if (roomCodeField == null)
        {
            SetStatus("Room code field is not configured.");
            return;
        }

        isJoinPromptOpen = true;
        SetPanelVisible(joinPromptPanel, true);
        SetActive(joinIpField, false);
        SetActive(portField, false);
        SetActive(roomCodeField, true);
        SetActive(confirmJoinLanButton, false);
        SetActive(confirmJoinOnlineButton, true);
        SetActive(cancelJoinPromptButton, true);
        UpdatePlaceholder(roomCodeField, "Room Code");
        roomCodeField.Select();
        ApplyModalState();
    }

    private void HideJoinPrompt()
    {
        isJoinPromptOpen = false;
        SetPanelVisible(joinPromptPanel, false);
        SetActive(joinIpField, false);
        SetActive(portField, false);
        SetActive(roomCodeField, false);
        SetActive(confirmJoinLanButton, false);
        SetActive(confirmJoinOnlineButton, false);
        SetActive(cancelJoinPromptButton, false);
        ApplyModalState();
        if (isMultiplayerOpen)
            SetMultiplayerOptionsVisible(true);
    }

    private void ShowMultiplayer()
    {
        HideSettings();
        HideJoinPrompt();
        isMultiplayerOpen = true;
        ConfigureCloseButtonForMultiplayer();
        SetMultiplayerOptionsVisible(true);
        ApplyModalState();
    }

    private void HideMultiplayer()
    {
        isMultiplayerOpen = false;
        SetMultiplayerOptionsVisible(false);
        ApplyModalState();
    }

    private void ShowSettings()
    {
        HideMultiplayer();
        HideJoinPrompt();
        RestoreAppliedSettings();
        isSettingsOpen = true;
        ConfigureCloseButtonForSettings();
        SetPanelVisible(settingsPanel, true);
        SetActive(brightnessLabel, true);
        SetActive(brightnessSlider, true);
        SetActive(sensitivityLabel, true);
        SetActive(sensitivitySlider, true);
        SetActive(wifiLoopVolumeLabel, true);
        SetActive(wifiLoopVolumeSlider, true);
        SetActive(fullscreenModeButton, true);
        SetActive(applySettingsButton, true);
        SetActive(closeSettingsButton, true);
        ApplyModalState();
    }

    private void HideSettings()
    {
        RestoreAppliedSettings();
        isSettingsOpen = false;
        SetPanelVisible(settingsPanel, false);
        SetActive(brightnessLabel, false);
        SetActive(brightnessSlider, false);
        SetActive(sensitivityLabel, false);
        SetActive(sensitivitySlider, false);
        SetActive(wifiLoopVolumeLabel, false);
        SetActive(wifiLoopVolumeSlider, false);
        SetActive(fullscreenModeButton, false);
        SetActive(applySettingsButton, false);
        SetActive(closeSettingsButton, false);
        ApplyModalState();
    }

    private void OnCloseButtonClicked()
    {
        if (isSettingsOpen)
            HideSettings();
        else if (isMultiplayerOpen)
            HideMultiplayer();
    }

    private void HostLan()
    {
        if (Services.NetSession == null)
        {
            SetStatus("Network session not available.");
            return;
        }

        SetStatus("Starting LAN host...");
        NetStartResult result = Services.NetSession.HostLanAutoPort(out ushort chosenPort);
        SetStatus(result == NetStartResult.Success
            ? $"Hosting LAN on {NetUtil.GetLocalIPv4()}:{chosenPort}"
            : "Host LAN failed: " + result);
    }

    private void JoinLan()
    {
        if (Services.NetSession == null)
        {
            SetStatus("Network session not available.");
            return;
        }

        if (joinIpField == null || string.IsNullOrWhiteSpace(joinIpField.text))
        {
            SetStatus("Enter a host IP address.");
            return;
        }

        if (portField == null || !ushort.TryParse(portField.text, out ushort port))
        {
            SetStatus("Enter a valid host port.");
            return;
        }

        SetStatus("Joining LAN host...");
        NetStartResult result = Services.NetSession.JoinLan(joinIpField.text.Trim(), port);
        if (result != NetStartResult.Success)
        {
            SetStatus("Join LAN failed: " + result);
            return;
        }

        HideJoinPrompt();
        SetStatus("Joining... (waiting for connection)");
        StartJoinTimeout();
    }

    private void OnHostOnlineClicked() => _ = HostOnlineAsync();
    private async Task HostOnlineAsync()
    {
        if (Services.NetSession == null)
        {
            SetStatus("Network session not available.");
            return;
        }

        SetStatus("Creating online room...");
        (NetStartResult result, string joinCode) = await Services.NetSession.HostOnlineAsync(6);
        if (result != NetStartResult.Success)
        {
            SetStatus("Host Online failed: " + result);
            return;
        }

        SetStatus(string.IsNullOrWhiteSpace(joinCode) ? "Online room created." : $"Online room code: {joinCode}", 10f);
    }

    private void OnJoinOnlineClicked() => _ = JoinOnlineAsync();
    private async Task JoinOnlineAsync()
    {
        if (Services.NetSession == null)
        {
            SetStatus("Network session not available.");
            return;
        }

        if (roomCodeField == null)
        {
            SetStatus("Room code field is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCodeField.text))
        {
            SetStatus("Enter a room code.");
            return;
        }

        string roomCode = roomCodeField.text.Trim().ToUpperInvariant();
        roomCodeField.text = roomCode;

        SetStatus("Joining online room...");
        NetStartResult result = await Services.NetSession.JoinOnlineAsync(roomCode);
        if (result != NetStartResult.Success)
        {
            SetStatus("Join Online failed: " + result);
            return;
        }

        HideJoinPrompt();
        SetStatus("Joining... (waiting for connection)");
        StartJoinTimeout();
    }

    private void StartJoinTimeout()
    {
        if (joinTimeoutRoutine != null) StopCoroutine(joinTimeoutRoutine);
        joinTimeoutRoutine = StartCoroutine(JoinTimeoutRoutine());
    }

    private IEnumerator JoinTimeoutRoutine()
    {
        const float timeoutSeconds = 10f;
        float start = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (Services.NetSession != null && Services.NetSession.IsConnected)
                yield break;
            yield return null;
        }

        Services.NetSession?.Shutdown();
        SetStatus("Join timed out. Check details and try again.");
    }

    private void HandleConnected()
    {
        if (joinTimeoutRoutine != null) StopCoroutine(joinTimeoutRoutine);
        joinTimeoutRoutine = null;

        if (Services.NetSession == null) return;
        if (Services.NetSession.IsHost)
        {
            SetStatus("Connected (Host). Loading Lobby...");
            Services.NetSession.LoadSceneForAll(LobbySceneName);
        }
        else
        {
            SetStatus("Connected (Client). Waiting for host...");
        }
    }

    private void HandleDisconnected(string reason)
    {
        if (joinTimeoutRoutine != null) StopCoroutine(joinTimeoutRoutine);
        joinTimeoutRoutine = null;
        SetStatus("Disconnected: " + reason);
    }

    private void ToggleFullscreenMode()
    {
        fullscreenEnabled = !fullscreenEnabled;
        ApplyFullscreenMode(fullscreenEnabled);
        SetButtonLabel(fullscreenModeButton, fullscreenEnabled ? "Display: Fullscreen" : "Display: Windowed");
    }

    private void ApplySettings()
    {
        float sensitivity = sensitivitySlider != null
            ? Mathf.Clamp(sensitivitySlider.value, MinSensitivity, MaxSensitivity)
            : DefaultSensitivity;
        float brightness = brightnessSlider != null
            ? Mathf.Clamp(brightnessSlider.value, MinBrightness, MaxBrightness)
            : DefaultBrightness;
        float wifiLoopVolume = wifiLoopVolumeSlider != null
            ? Mathf.Clamp(wifiLoopVolumeSlider.value, MinWifiLoopVolume, MaxWifiLoopVolume)
            : DefaultWifiLoopVolume;

        PlayerPrefs.SetFloat(PrefSensitivity, sensitivity);
        PlayerPrefs.SetFloat(PrefWifiLoopVolume, wifiLoopVolume);
        PlayerPrefs.SetInt(PrefFullscreen, fullscreenEnabled ? 1 : 0);
        PlayerPrefs.Save();

        appliedSensitivity = sensitivity;
        appliedBrightness = brightness;
        appliedWifiLoopVolume = wifiLoopVolume;
        appliedFullscreen = fullscreenEnabled;

        GlobalBrightnessManager.SetBrightness(appliedBrightness, true);
        ApplyWifiLoopVolumeToLocalPlayer(appliedWifiLoopVolume, persist: false);
        ShowTransientStatus($"Settings saved (Sensitivity {sensitivity:0.00}, Brightness {brightness:0.00}).");
    }

    private void LoadSettingsFromPrefs()
    {
        appliedSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivity, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        appliedBrightness = GlobalBrightnessManager.LoadSavedBrightness();
        appliedWifiLoopVolume = Mathf.Clamp(PlayerPrefs.GetFloat(PrefWifiLoopVolume, DefaultWifiLoopVolume), MinWifiLoopVolume, MaxWifiLoopVolume);
        appliedFullscreen = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        fullscreenEnabled = appliedFullscreen;

        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(appliedSensitivity);
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(appliedBrightness);
        if (wifiLoopVolumeSlider != null) wifiLoopVolumeSlider.SetValueWithoutNotify(appliedWifiLoopVolume);

        ApplyFullscreenMode(appliedFullscreen);
        GlobalBrightnessManager.SetBrightness(appliedBrightness, false);
        ApplyWifiLoopVolumeToLocalPlayer(appliedWifiLoopVolume, persist: false);
        SetButtonLabel(fullscreenModeButton, appliedFullscreen ? "Display: Fullscreen" : "Display: Windowed");
    }

    private void RestoreAppliedSettings()
    {
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(appliedSensitivity);
        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(appliedBrightness);
        if (wifiLoopVolumeSlider != null)
            wifiLoopVolumeSlider.SetValueWithoutNotify(appliedWifiLoopVolume);

        fullscreenEnabled = appliedFullscreen;
        ApplyFullscreenMode(appliedFullscreen);
        GlobalBrightnessManager.SetBrightness(appliedBrightness, false);
        ApplyWifiLoopVolumeToLocalPlayer(appliedWifiLoopVolume, persist: false);
        SetButtonLabel(fullscreenModeButton, appliedFullscreen ? "Display: Fullscreen" : "Display: Windowed");
    }

    private static void ApplyWifiLoopVolumeToLocalPlayer(float volume, bool persist)
    {
        LocalPlayerReference local = LocalPlayerReference.Instance;
        if (local == null) return;

        var soundFx = local.GetComponent<PlayerSoundFX>();
        if (soundFx == null) return;

        soundFx.SetWifiLoopVolume(volume, persist);
    }

    private static void ApplyFullscreenMode(bool enabled)
    {
        Screen.fullScreenMode = enabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreen = enabled;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private void ConfigureCloseButtonForSettings()
    {
        SetButtonLabel(closeSettingsButton, "Close");
        SetButtonLayout(closeSettingsButton, new Vector2(85f, -130f), new Vector2(150f, 38f));
    }

    private void ConfigureCloseButtonForMultiplayer()
    {
        SetButtonLabel(closeSettingsButton, "Back");
        SetButtonLayout(closeSettingsButton, new Vector2(0f, -50f), new Vector2(280f, 42f));
    }

    private static void SetButtonLayout(Button button, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (button == null) return;
        if (button.transform is not RectTransform rect) return;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void QuitGame()
    {
        Services.NetSession?.Shutdown();
#if UNITY_EDITOR
        SetStatus("Quit requested. Stop Play Mode to exit in editor.");
#else
        Application.Quit();
#endif
    }
}