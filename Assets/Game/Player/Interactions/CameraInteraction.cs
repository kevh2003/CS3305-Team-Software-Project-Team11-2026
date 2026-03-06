using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// CameraInteraction using LocalPlayerReference and direct Keyboard input for exit.
/// While viewing a CCTV camera, left-clicking on the world will lure nearby enemies
/// to investigate that position before returning to their original location.
/// </summary>
public class CameraInteraction : MonoBehaviour, IInteractable
{
    [Header("CCTV Camera (Assign in Inspector)")]
    public Camera cctvCamera;
    public CameraMovement cctvCameraLook;

    [Header("Exit Key")]
    [Tooltip("Key to press to exit CCTV view")]
    public Key exitKey = Key.Q;

    [Header("Lure Settings")]
    [Tooltip("Radius around the clicked point in which enemies will be lured")]
    public float lureRadius = 15f;

    [Tooltip("LayerMask for surfaces the lure raycast can hit (e.g. Ground, Floor)")]
    public LayerMask lureSurfaceMask = ~0;   // All layers by default; restrict in Inspector

    private bool _isViewingCCTV = false;
    private Keyboard _keyboard;
    private Mouse _mouse;

    void Start()
    {
        // Safety: ensure CCTV never starts active
        if (cctvCamera != null)
            cctvCamera.enabled = false;


        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;

        _keyboard = Keyboard.current;
        _mouse = Mouse.current;
    }

    void Update()
    {
        if (!_isViewingCCTV) return;

        // Refresh device references if needed
        if (_keyboard == null) _keyboard = Keyboard.current;
        if (_mouse == null)    _mouse    = Mouse.current;

        // Exit CCTV view
        if (_keyboard != null && _keyboard[exitKey].wasPressedThisFrame)
        {
            ExitCCTVView();
            return;
        }

        // Lure enemies on left click
        if (_mouse != null && _mouse.leftButton.wasPressedThisFrame)
        {
            TryLureEnemies();
        }
    }

    // ─── Lure Logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Casts a ray from the centre of the CCTV camera into the world.
    /// Any EnemyAI within <see cref="lureRadius"/> of the hit point will be
    /// sent to investigate that location.
    /// </summary>
    void TryLureEnemies()
    {
        if (cctvCamera == null) return;

        // Ray from the centre of the CCTV camera viewport
        Ray ray = cctvCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, lureSurfaceMask))
        {
            Debug.Log("CameraInteraction: Lure raycast found no surface.");
            return;
        }

        Vector3 lurePoint = hit.point;
        Debug.Log($"CameraInteraction: Lure point set at {lurePoint}");

        // Host can invoke directly; clients request server-authoritative lure via ObjectiveState RPC.
        var state = ObjectiveState.Instance;
        if (state == null)
        {
            Debug.LogWarning("CameraInteraction: ObjectiveState unavailable, cannot send lure request.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            state.ServerLureEnemiesAtPoint(lurePoint, lureRadius);
        else
            state.RequestLureEnemiesServerRpc(lurePoint, lureRadius);
    }

    // ─── IInteractable ────────────────────────────────────────────────────────

    public bool CanInteract()
    {
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

    // ─── Camera Switching ─────────────────────────────────────────────────────

    void EnterCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        _isViewingCCTV = true;
        DropPromptUI.Instance?.SetCameraVisible(true, $"Press {exitKey} to exit");

        if (player.PlayerInput != null)
            player.PlayerInput.enabled = false;

        if (player.PlayerCamera != null)
            player.PlayerCamera.enabled = false;

        if (cctvCamera != null)
            cctvCamera.enabled = true;

        if (cctvCameraLook != null)
            cctvCameraLook.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"CameraInteraction: Entered CCTV view (Press {exitKey} to exit, Left-click to lure)");
    }

    void ExitCCTVView()
    {
        var player = LocalPlayerReference.Instance;
        if (player == null) return;

        _isViewingCCTV = false;

        if (cctvCameraLook != null)
            cctvCameraLook.enabled = false;

        if (cctvCamera != null)
            cctvCamera.enabled = false;

        if (player.PlayerCamera != null)
            player.PlayerCamera.enabled = true;

        if (player.PlayerInput != null)
            player.PlayerInput.enabled = true;

        DropPromptUI.Instance?.SetCameraVisible(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("CameraInteraction: Exited CCTV view");
    }

    void OnDrawGizmosSelected()
    {
        if (cctvCamera == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(cctvCamera.transform.position, lureRadius);
    }
}