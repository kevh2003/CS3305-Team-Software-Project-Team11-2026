using UnityEngine;

public class CameraInteraction : MonoBehaviour, IInteractable
{
    [Header("Player Camera")]
    public Camera fpsCamera;

    [Header("CCTV Camera")]
    public Camera cctvCamera;
    public CameraMovement cctvCameraLook;

    [Header("Player Scripts")]
    public PlayerMovement playerMovement;
    public PlayerLook playerLook;

    [Header("Input")]
    public KeyCode exitKey = KeyCode.Q; // Changed from E to Q to avoid conflict

    private bool isViewingCCTV = false;

    void Start()
    {
        // Safety: ensure CCTV never starts active
        cctvCamera.enabled = false;
        cctvCameraLook.enabled = false;
    }

    void Update()
    {
        if (isViewingCCTV && Input.GetKeyDown(exitKey))
        {
            ExitCCTVView();
        }
    }

    public bool CanInteract()
    {
        return !isViewingCCTV;
    }

    public bool Interact(Interactor interactor)
    {
        EnterCCTVView();
        return true;
    }

    void EnterCCTVView()
    {
        isViewingCCTV = true;

        // Disable player control FIRST (important for Input System)
        playerMovement.enabled = false;
        playerLook.enabled = false;

        // Switch cameras
        fpsCamera.enabled = false;
        cctvCamera.enabled = true;

        // Enable CCTV control
        cctvCameraLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ExitCCTVView()
    {
        isViewingCCTV = false;

        // Disable CCTV control FIRST
        cctvCameraLook.enabled = false;

        // Switch cameras back
        cctvCamera.enabled = false;
        fpsCamera.enabled = true;

        // Restore player control
        playerMovement.enabled = true;
        playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}



