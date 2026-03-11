using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// CameraInteraction using LocalPlayerReference and direct Keyboard input for exit.
/// While viewing a CCTV camera, left-clicking on the world will lure nearby enemies
/// to investigate that position before returning to their original location.
/// </summary>
public class CameraInteraction : MonoBehaviour, IInteractable
{
    private const string PrefSensitivity = "settings_sensitivity";
    private const float MinSensitivity = 0.02f;
    private const float MaxSensitivity = 2f;
    private const float DefaultSensitivity = 0.12f;

    [Header("Legacy Single Camera (Optional)")]
    public Camera cctvCamera;
    public CameraMovement cctvCameraLook;

    [Header("CCTV Cameras (Preferred)")]
    [SerializeField] private Camera[] cctvCameras;
    [SerializeField] private CameraMovement[] cctvCameraLooks;
    [SerializeField] private int initialCameraIndex = 0;
    [SerializeField] private bool autoAddMissingCameraMovement = true;

    [Header("Exit Key")]
    [Tooltip("Key to press to exit CCTV view")]
    public Key exitKey = Key.Q;

    [Header("Lure Settings")]
    [Tooltip("Radius around the clicked point in which enemies will be lured")]
    public float lureRadius = 15f;

    [Tooltip("LayerMask for surfaces the lure raycast can hit (e.g. Ground, Floor)")]
    public LayerMask lureSurfaceMask = ~0;   // All layers by default; restrict in Inspector

    [Tooltip("Seconds between lure pings while using CCTV")]
    [SerializeField] private float pingCooldownSeconds = 20f;
    [SerializeField] private float lockAcquireTimeoutSeconds = 0.5f;

    public static bool IsAnyLocalCctvActive { get; private set; }
    public static int LastExitFrame { get; private set; } = -1;
    public static bool WasExitedThisFrame => Time.frameCount == LastExitFrame;

    private bool _isViewingCCTV = false;
    private Keyboard _keyboard;
    private Mouse _mouse;
    private readonly List<Camera> _resolvedCameras = new();
    private readonly List<CameraMovement> _resolvedLooks = new();
    private int _activeCameraIndex = 0;
    private float _nextPingAllowedAt = 0f;
    private int _lastDisplayedCooldownSeconds = int.MinValue;
    private bool _waitingForLock = false;
    private float _lastAppliedSensitivity = float.NaN;

    void Start()
    {
        BuildCameraList();
        DisableAllCctvCameras();
        _keyboard = Keyboard.current;
        _mouse = Mouse.current;
    }

    void Update()
    {
        if (!_isViewingCCTV) return;

        HideCrosshairInteractPrompt();

        // Refresh device references if needed
        if (_keyboard == null) _keyboard = Keyboard.current;
        if (_mouse == null)    _mouse    = Mouse.current;

        // Exit CCTV view
        if (_keyboard != null && _keyboard[exitKey].wasPressedThisFrame)
        {
            ExitCCTVView();
            return;
        }

        // Cycle cameras on right click
        if (_mouse != null && _mouse.rightButton.wasPressedThisFrame)
            NextCamera();

        // Lure enemies on left click
        if (_mouse != null && _mouse.leftButton.wasPressedThisFrame)
            TryLureEnemies();

        SyncCctvSensitivityFromSettings();
        RefreshCameraPrompt();
    }

    // ─── Lure Logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Casts a ray from the centre of the CCTV camera into the world.
    /// Any EnemyAI within <see cref="lureRadius"/> of the hit point will be
    /// sent to investigate that location.
    /// </summary>
    void TryLureEnemies()
    {
        if (!_isViewingCCTV) return;

        float cooldownRemaining = _nextPingAllowedAt - Time.time;
        if (cooldownRemaining > 0f)
        {
            RefreshCameraPrompt(force: true);
            return;
        }

        Camera activeCamera = GetActiveCamera();
        if (activeCamera == null) return;

        // Ray from the centre of the CCTV camera viewport
        Ray ray = activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, lureSurfaceMask))
        {
            Debug.Log("CameraInteraction: Lure raycast found no surface.");
            return;
        }

        Vector3 lurePoint = hit.point;
        Debug.Log($"CameraInteraction: Lure point set at {lurePoint}");

        // Server-authoritative lure request.
        var system = CctvSystemManager.Instance;
        if (system == null)
        {
            Debug.LogWarning("CameraInteraction: CctvSystemManager unavailable, cannot send lure request.");
            return;
        }

        system.RequestLureAtPointServerRpc(lurePoint, lureRadius);

        _nextPingAllowedAt = Time.time + Mathf.Max(0.1f, pingCooldownSeconds);
        RefreshCameraPrompt(force: true);
    }

    // ─── IInteractable ────────────────────────────────────────────────────────

    public bool CanInteract()
    {
        return !_isViewingCCTV && !_waitingForLock && LocalPlayerReference.Instance != null;
    }

    public bool Interact(Interactor interactor)
    {
        if (LocalPlayerReference.Instance == null)
        {
            Debug.LogWarning("CameraInteraction: No local player reference available");
            return false;
        }

        if (_waitingForLock || _isViewingCCTV)
            return false;

        var system = CctvSystemManager.Instance;
        var networkManager = NetworkManager.Singleton;
        if (system == null || networkManager == null || !networkManager.IsClient)
            return false;

        ulong localClientId = networkManager.LocalClientId;
        if (system.IsInUseByAnotherClient(localClientId))
        {
            ShowAlreadyInUsePrompt();
            return false;
        }

        StartCoroutine(TryEnterWhenLockAcquired(system, localClientId));
        return false;
    }

    // ─── Camera Switching ─────────────────────────────────────────────────────

    void EnterCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;
        if (!BuildCameraList()) return;

        _isViewingCCTV = true;
        IsAnyLocalCctvActive = true;

        if (player.PlayerInput != null)
            player.PlayerInput.enabled = false;

        if (player.PlayerCamera != null)
            player.PlayerCamera.enabled = false;

        if (player.Interactor != null)
            player.Interactor.enabled = false;

        HideCrosshairInteractPrompt();

        if (_activeCameraIndex < 0 || _activeCameraIndex >= _resolvedCameras.Count)
            _activeCameraIndex = Mathf.Clamp(initialCameraIndex, 0, Mathf.Max(0, _resolvedCameras.Count - 1));
        SetActiveCamera(_activeCameraIndex);
        SyncCctvSensitivityFromSettings(force: true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _lastDisplayedCooldownSeconds = int.MinValue;
        RefreshCameraPrompt(force: true);

        Debug.Log($"CameraInteraction: Entered CCTV view (Q exit, RMB next camera, LMB ping)");
    }

    void ExitCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        var system = CctvSystemManager.Instance;

        _isViewingCCTV = false;
        IsAnyLocalCctvActive = false;
        LastExitFrame = Time.frameCount;

        DisableAllCctvCameras();

        if (player != null)
        {
            if (player.PlayerCamera != null)
                player.PlayerCamera.enabled = true;

            if (player.PlayerInput != null)
                player.PlayerInput.enabled = true;

            if (player.Interactor != null)
                player.Interactor.enabled = true;
        }

        DropPromptUI.Instance?.SetCameraVisible(false);

        if (system != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            system.ReleaseCctvServerRpc();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("CameraInteraction: Exited CCTV view");
    }

    private void OnDisable()
    {
        _waitingForLock = false;

        if (_isViewingCCTV)
            ExitCCTVView();
    }

    private System.Collections.IEnumerator TryEnterWhenLockAcquired(CctvSystemManager system, ulong localClientId)
    {
        _waitingForLock = true;
        system.RequestEnterCctvServerRpc();

        float timeout = Mathf.Max(0.1f, lockAcquireTimeoutSeconds);
        float deadline = Time.time + timeout;

        while (Time.time < deadline)
        {
            if (system == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                break;

            if (system.IsInUseByClient(localClientId))
            {
                _waitingForLock = false;
                EnterCCTVView();
                yield break;
            }

            if (system.IsInUseByAnotherClient(localClientId))
                break;

            yield return null;
        }

        _waitingForLock = false;
        ShowAlreadyInUsePrompt();
    }

    private void SyncCctvSensitivityFromSettings(bool force = false)
    {
        float sensitivity = DefaultSensitivity;
        var local = LocalPlayerReference.Instance;
        if (local != null && local.NetworkPlayer != null)
        {
            sensitivity = local.NetworkPlayer.LookSensitivity;
        }
        else
        {
            sensitivity = PlayerPrefs.GetFloat(PrefSensitivity, DefaultSensitivity);
        }

        sensitivity = Mathf.Clamp(sensitivity, MinSensitivity, MaxSensitivity);

        if (!force && Mathf.Abs(_lastAppliedSensitivity - sensitivity) < 0.0001f)
            return;

        _lastAppliedSensitivity = sensitivity;

        for (int i = 0; i < _resolvedLooks.Count; i++)
        {
            if (_resolvedLooks[i] != null)
                _resolvedLooks[i].SetSensitivity(sensitivity);
        }
    }

    private void ShowAlreadyInUsePrompt()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        var crosshair = player.GetComponent<Crosshair>();
        if (crosshair == null) return;

        crosshair.ShowInteractPrompt();
        crosshair.SetPromptText("Already in use");
    }

    private static void HideCrosshairInteractPrompt()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        var crosshair = player.GetComponent<Crosshair>();
        if (crosshair == null) return;

        crosshair.Hide();
    }

    private bool BuildCameraList()
    {
        _resolvedCameras.Clear();
        _resolvedLooks.Clear();

        if (cctvCameras != null && cctvCameras.Length > 0)
        {
            for (int i = 0; i < cctvCameras.Length; i++)
            {
                Camera camera = cctvCameras[i];
                if (camera == null) continue;

                CameraMovement look = null;
                if (cctvCameraLooks != null && i < cctvCameraLooks.Length)
                    look = cctvCameraLooks[i];

                look = ResolveLookController(camera, look);

                _resolvedCameras.Add(camera);
                _resolvedLooks.Add(look);
            }
        }

        if (_resolvedCameras.Count == 0 && cctvCamera != null)
        {
            CameraMovement look = ResolveLookController(cctvCamera, cctvCameraLook);
            _resolvedCameras.Add(cctvCamera);
            _resolvedLooks.Add(look);
        }

        _activeCameraIndex = Mathf.Clamp(initialCameraIndex, 0, Mathf.Max(0, _resolvedCameras.Count - 1));
        return _resolvedCameras.Count > 0;
    }

    private CameraMovement ResolveLookController(Camera camera, CameraMovement look)
    {
        if (camera == null) return null;
        if (look != null) return look;

        if (!camera.TryGetComponent(out look) && autoAddMissingCameraMovement)
            look = camera.gameObject.AddComponent<CameraMovement>();

        return look;
    }

    private void DisableAllCctvCameras()
    {
        for (int i = 0; i < _resolvedCameras.Count; i++)
        {
            if (_resolvedCameras[i] != null)
                _resolvedCameras[i].enabled = false;

            if (i < _resolvedLooks.Count && _resolvedLooks[i] != null)
                _resolvedLooks[i].enabled = false;
        }
    }

    private void NextCamera()
    {
        if (_resolvedCameras.Count <= 1) return;

        int nextIndex = _activeCameraIndex + 1;
        if (nextIndex >= _resolvedCameras.Count)
            nextIndex = 0;

        SetActiveCamera(nextIndex);
        RefreshCameraPrompt(force: true);
    }

    private void SetActiveCamera(int index)
    {
        if (index < 0 || index >= _resolvedCameras.Count) return;

        DisableAllCctvCameras();
        _activeCameraIndex = index;

        Camera activeCamera = _resolvedCameras[_activeCameraIndex];
        CameraMovement activeLook = _activeCameraIndex < _resolvedLooks.Count ? _resolvedLooks[_activeCameraIndex] : null;

        if (activeCamera != null) activeCamera.enabled = true;
        if (activeLook != null) activeLook.enabled = true;

        // Keep legacy fields in sync so older gizmos/inspector setup still make sense.
        cctvCamera = activeCamera;
        cctvCameraLook = activeLook;
    }

    private Camera GetActiveCamera()
    {
        if (_activeCameraIndex < 0 || _activeCameraIndex >= _resolvedCameras.Count)
            return null;
        return _resolvedCameras[_activeCameraIndex];
    }

    private void RefreshCameraPrompt(bool force = false)
    {
        if (!_isViewingCCTV) return;

        int secondsRemaining = Mathf.CeilToInt(Mathf.Max(0f, _nextPingAllowedAt - Time.time));
        if (!force && secondsRemaining == _lastDisplayedCooldownSeconds)
            return;

        _lastDisplayedCooldownSeconds = secondsRemaining;

        string leftClickText = secondsRemaining > 0
            ? $"Left Click: Ping ({secondsRemaining}s)"
            : "Left Click: Ping";

        string prompt = $"{leftClickText}\nRight Click: Next Camera\nPress {exitKey} to exit";
        DropPromptUI.Instance?.SetCameraVisible(true, prompt);
    }

    void OnDrawGizmosSelected()
    {
        Camera cameraToDraw = cctvCamera;
        if (cameraToDraw == null && cctvCameras != null && cctvCameras.Length > 0)
            cameraToDraw = cctvCameras[0];
        if (cameraToDraw == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(cameraToDraw.transform.position, lureRadius);
    }
}