using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public sealed class PauseMenuController : MonoBehaviour
{
    private const string PrefSensitivity = "settings_sensitivity";
    private const float MinSensitivity = 0.02f;
    private const float MaxSensitivity = 2.00f;
    private const float DefaultSensitivity = 0.12f;
    private const float MinBrightness = -2.0f;
    private const float MaxBrightness = 2.0f;

    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button returnToLobbyButton; // host-only
    [SerializeField] private Button leaveSessionButton;  // everyone
    [SerializeField] private Button optionsButton;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Button applyOptionsButton;
    [SerializeField] private Button closeOptionsButton;

    private bool _open;
    private bool _optionsLoaded;
    private float _appliedSensitivity = DefaultSensitivity;
    private float _appliedBrightness;

    private void Awake()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        if (leaveSessionButton != null) leaveSessionButton.onClick.AddListener(LeaveSession);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (applyOptionsButton != null) applyOptionsButton.onClick.AddListener(ApplyOptions);
        if (closeOptionsButton != null) closeOptionsButton.onClick.AddListener(CloseOptions);

        if (brightnessSlider != null)
        {
            brightnessSlider.minValue = MinBrightness;
            brightnessSlider.maxValue = MaxBrightness;
            brightnessSlider.wholeNumbers = false;
            brightnessSlider.onValueChanged.AddListener(PreviewBrightness);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = MinSensitivity;
            sensitivitySlider.maxValue = MaxSensitivity;
            sensitivitySlider.wholeNumbers = false;
        }

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
        SetMainButtonsVisible(true, isHost);
        SetOptionsVisible(false);
        LoadOptions();

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

        if (!open)
        {
            RestoreAppliedOptions();
            SetOptionsVisible(false);
        }
    }

    private void OpenOptions()
    {
        if (!_open) return;

        SetMainButtonsVisible(false, IsHost());
        SetOptionsVisible(true);
        LoadOptions();
    }

    private void CloseOptions()
    {
        if (!_open) return;

        RestoreAppliedOptions();
        SetOptionsVisible(false);
        SetMainButtonsVisible(true, IsHost());
    }

    private void SetMainButtonsVisible(bool visible, bool isHost)
    {
        if (resumeButton != null) resumeButton.gameObject.SetActive(visible);
        if (leaveSessionButton != null) leaveSessionButton.gameObject.SetActive(visible);
        if (optionsButton != null) optionsButton.gameObject.SetActive(visible);
        if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(visible && isHost);
    }

    private void SetOptionsVisible(bool visible)
    {
        if (optionsPanel != null) optionsPanel.SetActive(visible);
    }

    private void LoadOptions()
    {
        _appliedBrightness = Mathf.Clamp(GlobalBrightnessManager.LoadSavedBrightness(), MinBrightness, MaxBrightness);
        _appliedSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivity, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        _optionsLoaded = true;

        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(_appliedBrightness);

        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(_appliedSensitivity);
    }

    private void PreviewBrightness(float value)
    {
        GlobalBrightnessManager.SetBrightness(Mathf.Clamp(value, MinBrightness, MaxBrightness), false);
    }

    private void ApplyOptions()
    {
        float sensitivity = sensitivitySlider != null
            ? Mathf.Clamp(sensitivitySlider.value, MinSensitivity, MaxSensitivity)
            : Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivity, DefaultSensitivity), MinSensitivity, MaxSensitivity);
        float brightness = brightnessSlider != null
            ? Mathf.Clamp(brightnessSlider.value, MinBrightness, MaxBrightness)
            : Mathf.Clamp(GlobalBrightnessManager.GetCurrentBrightness(), MinBrightness, MaxBrightness);

        PlayerPrefs.SetFloat(PrefSensitivity, sensitivity);
        PlayerPrefs.Save();

        _appliedSensitivity = sensitivity;
        _appliedBrightness = brightness;
        _optionsLoaded = true;

        ApplySensitivityToLocalPlayer(sensitivity);
        GlobalBrightnessManager.SetBrightness(_appliedBrightness, true);
    }

    private void RestoreAppliedOptions()
    {
        if (!_optionsLoaded) return;

        if (brightnessSlider != null)
            brightnessSlider.SetValueWithoutNotify(_appliedBrightness);
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(_appliedSensitivity);

        ApplySensitivityToLocalPlayer(_appliedSensitivity);
        GlobalBrightnessManager.SetBrightness(_appliedBrightness, false);
    }

    private static void ApplySensitivityToLocalPlayer(float value)
    {
        LocalPlayerReference local = LocalPlayerReference.Instance;
        if (local == null) return;

        local.NetworkPlayer?.SetLookSensitivity(value);
        var cameraMovements = local.GetComponentsInChildren<CameraMovement>(true);
        for (int i = 0; i < cameraMovements.Length; i++)
            cameraMovements[i].SetSensitivity(value);
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