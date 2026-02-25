using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

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

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -25f;

    [Header("Sprint Settings")]
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float maxSprintTime = 3f;
    [SerializeField] private float sprintRegenRate = 1f;

    private float _currentSprintTime;
    private bool _isSprinting;
        
    private InputAction _jump;
    private float _jumpCooldown = 0.05f;
    private float _jumpTimer;
    private float _verticalVelocity;

    private CharacterController _cc;
    private PlayerInput _playerInput;

    private InputAction _move;
    private InputAction _look;
    private InputAction _sprint;

    private float _pitch;
    private bool _inGameScene;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        _currentSprintTime = maxSprintTime;
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        // Scene gate: player exists across scenes but only "plays" in 03_Game scene (currently)
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Cache actions by name (must match InputActions asset)    
        _move = _playerInput.actions["Move"];
        _look = _playerInput.actions["Look"];
        _jump = _playerInput.actions["Jump"]; 
        _sprint = _playerInput.actions["Sprint"];
 

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
        if (_cc == null || !_cc.enabled) return;

        // WASD movement
        Vector2 move = _move.ReadValue<Vector2>();
        Vector3 moveWorld = (transform.right * move.x + transform.forward * move.y);

        // _cc.SimpleMove(moveWorld * moveSpeed);
        if (moveWorld.sqrMagnitude > 1f) moveWorld.Normalize();

        bool grounded = _cc.isGrounded;

        if (grounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;


        _jumpTimer -= Time.deltaTime;

        if (_jump != null && _jump.IsPressed() && grounded && _jumpTimer <= 0f)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpTimer = _jumpCooldown;
        }


        bool sprintHeld = _sprint != null && _sprint.IsPressed();

        if (sprintHeld)
        {
            Debug.Log("SPRINT HELD");
        }

        if (sprintHeld && _currentSprintTime > 0f && moveWorld.sqrMagnitude > 0.1f)
        {
            _isSprinting = true;
            _currentSprintTime -= Time.deltaTime;
        }
        else
        {
            _isSprinting = false;
            _currentSprintTime += sprintRegenRate * Time.deltaTime;
        }

        _currentSprintTime = Mathf.Clamp(_currentSprintTime, 0f, maxSprintTime);

        float finalSpeed = _isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = (moveWorld * finalSpeed) + (Vector3.up * _verticalVelocity);
        _cc.Move(velocity * Time.deltaTime);


        // Mouse look
        Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f);

        _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    public void ServerResetForNewMatch(Vector3 position, Quaternion rotation)
    {
        if (!IsServer) return;

        // Apply on server
        ApplyReset(position, rotation);

        // Tell the owning client to apply the same reset
        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };

        ResetForNewMatchClientRpc(position, rotation, rpcParams);
    }

    [ClientRpc]
    private void ResetForNewMatchClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        ApplyReset(position, rotation);
    }

    private void ApplyReset(Vector3 position, Quaternion rotation)
    {
        if (_cc != null)
            _cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        _verticalVelocity = 0f;
        _pitch = 0f;

        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = Vector3.zero;

        StartCoroutine(ReenableController());
    }

    private IEnumerator ReenableController()
    {
        yield return new WaitForSeconds(0.2f);

        if (_cc != null)
            _cc.enabled = true;
    }
}