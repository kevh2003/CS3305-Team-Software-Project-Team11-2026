using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/*
 * NetworkPlayer
 * 
 * Networked player avatar controller
 * - Uses the New Input System via PlayerInput actions (Move/Look)
 * - Enables input + camera only for the owning client
 * - Keeps the player "inactive" outside the Game scene (03_Game)
 *
 * Note: This is intentionally minimal; movement/system teams should expand on it
 */

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.12f;

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;

    private CharacterController _cc;
    private PlayerInput _playerInput;

    private InputAction _move;
    private InputAction _look;

    private float _pitch;
    private bool _inGameScene;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        // Scene gate: player exists across scenes but only "plays" in 03_Game scene (currently)
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Cache actions by name (must match InputActions asset)
        _move = _playerInput.actions["Move"];
        _look = _playerInput.actions["Look"];

        ApplySceneState(SceneManager.GetActiveScene().name);
    }

    public override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneState(scene.name);
    }

    private void ApplySceneState(string sceneName)
    {
        _inGameScene = sceneName == "03_Game";
        bool isOwner = IsOwner;

        // Only local owner gets a camera + input, and only during gameplay i.e when in 03_Game scene 
        if (playerCamera != null)
            playerCamera.gameObject.SetActive(_inGameScene && isOwner);

        if (_playerInput != null)
            _playerInput.enabled = _inGameScene && isOwner;

        // Hide's body visuals outside of gameplay scene e.g. when in 02_Lobby scene
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = _inGameScene;

        if (_inGameScene && isOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!_inGameScene && isOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (!_inGameScene) return;
        if (_playerInput == null || !_playerInput.enabled) return;

        // WASD movement
        Vector2 move = _move.ReadValue<Vector2>();
        Vector3 moveWorld = (transform.right * move.x + transform.forward * move.y).normalized;
        _cc.SimpleMove(moveWorld * moveSpeed);

        // Mouse look
        Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f);

        _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }
}