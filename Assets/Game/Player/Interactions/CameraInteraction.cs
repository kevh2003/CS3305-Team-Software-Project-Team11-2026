using UnityEngine;
using Unity.Netcode;

public class CameraInteraction : NetworkBehaviour, IInteractable
{
    [Header("Player Camera")]
    public Camera fpsCamera;

    [Header("CCTV Camera")]
    public Camera cctvCamera;
    public CameraMovement cctvCameraLook;

    [Header("Player Scripts")]
    public PlayerMovement playerMovement;
    public PlayerLook playerLook;

    private bool isViewingCCTV = false;
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            inputActions.Enable();
            // Listen for Q key to exit CCTV
            inputActions.Player.DropItem.performed += ctx => 
            {
                if (isViewingCCTV)
                {
                    ExitCCTVView();
                }
            };
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }

    void Start()
    {
        // Safety: ensure CCTV never starts active
        if (cctvCamera != null)
            cctvCamera.enabled = false;
        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;
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
        if (playerMovement != null)
            playerMovement.enabled = false;
        if (playerLook != null)
            playerLook.enabled = false;

        // Switch cameras
        if (fpsCamera != null)
            fpsCamera.enabled = false;
        if (cctvCamera != null)
            cctvCamera.enabled = true;

        // Enable CCTV control
        if (cctvCameraLook != null)
            cctvCameraLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("📹 Entered CCTV view - Press Q to exit");
    }

    void ExitCCTVView()
    {
        isViewingCCTV = false;

        // Disable CCTV control FIRST
        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;

        // Switch cameras back
        if (cctvCamera != null)
            cctvCamera.enabled = false;
        if (fpsCamera != null)
            fpsCamera.enabled = true;

        // Restore player control
        if (playerMovement != null)
            playerMovement.enabled = true;
        if (playerLook != null)
            playerLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("👤 Exited CCTV view");
    }
}