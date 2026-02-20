using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CameraInteraction using LocalPlayerReference and direct Keyboard input for exit.
/// This version doesn't require an "ExitCCTV" action in your Input Actions asset.
/// Just press Q (or whatever key you set) to exit CCTV view.
/// </summary>
public class CameraInteraction : MonoBehaviour, IInteractable
{
    [Header("CCTV Camera (Assign in Inspector)")]
    public Camera cctvCamera;
    public CameraMovement cctvCameraLook;

    [Header("Exit Key")]
    [Tooltip("Key to press to exit CCTV view")]
    public Key exitKey = Key.Q;

    private bool _isViewingCCTV = false;
    private Keyboard _keyboard;

    void Start()
    {
        // Safety: ensure CCTV never starts active
        if (cctvCamera != null)
            cctvCamera.enabled = false;

        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;

        // Get keyboard reference
        _keyboard = Keyboard.current;
    }

    void Update()
    {
        if (!_isViewingCCTV) return;

        // Get keyboard if we don't have it
        if (_keyboard == null)
        {
            _keyboard = Keyboard.current;
            if (_keyboard == null) return;
        }

        // Check if exit key was pressed
        if (_keyboard[exitKey].wasPressedThisFrame)
        {
            ExitCCTVView();
        }
    }

    public bool CanInteract()
    {
        // Can interact if we have a valid local player reference and aren't already viewing
        return !_isViewingCCTV && LocalPlayerReference.Instance != null;
    }

    public bool Interact(Interactor interactor)
    {
        if (LocalPlayerReference.Instance == null)
        {
            Debug.LogWarning("CameraInteraction: No local player reference available");
            return false;
        }

        EnterCCTVView();
        return true;
    }

    void EnterCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        _isViewingCCTV = true;

        // Disable player input
        if (player.PlayerInput != null)
            player.PlayerInput.enabled = false;

        // Switch cameras
        if (player.PlayerCamera != null)
            player.PlayerCamera.enabled = false;

        if (cctvCamera != null)
            cctvCamera.enabled = true;

        // Enable CCTV control
        if (cctvCameraLook != null)
            cctvCameraLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"CameraInteraction: Entered CCTV view (Press {exitKey} to exit)");
    }

    void ExitCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        _isViewingCCTV = false;

        // Disable CCTV control
        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;

        // Switch cameras back
        if (cctvCamera != null)
            cctvCamera.enabled = false;

        if (player.PlayerCamera != null)
            player.PlayerCamera.enabled = true;

        // Restore player input
        if (player.PlayerInput != null)
            player.PlayerInput.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("CameraInteraction: Exited CCTV view");
    }
}