using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float lookSensitivity = 0.12f;

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    private PlayerSoundFX soundFX;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -25f;
    private bool _wasAirborne = false;
    private float _airborneStartY = 0f;
    private const float _minFallHeight = 3f;

    [Header("Sprint / Stamina")]
    [SerializeField] private float sprintMultiplier = 1.6f;     // adjust sprint speed here
    [SerializeField] private float maxStaminaSeconds = 4f;      // sprint time : 4 seconds
    [SerializeField] private float staminaRegenPerSecond = 1.0f; // sprint regen regen time : 4 seconds
    [SerializeField] private float minMoveToSprint = 0.1f;       // prevents sprinting on the spot

    // Exposed for UI later (0..1)
    public float Stamina01 => (maxStaminaSeconds <= 0f) ? 0f : Mathf.Clamp01(_staminaSeconds / maxStaminaSeconds);

    private float _staminaSeconds;
    private bool _isSprinting;
    private bool _frozen = false;

    private InputAction _jump;
    private InputAction _move;
    private InputAction _look;
    private InputAction _sprint;

    private float _jumpCooldown = 0.05f;
    private float _jumpTimer;
    private float _verticalVelocity;
    private float _pitch;
    private bool _inGameScene;

    private CharacterController _cc;
    private PlayerInput _playerInput;

    private void Awake()
    {   
        soundFX = GetComponent<PlayerSoundFX>();
        _cc = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();

        _staminaSeconds = maxStaminaSeconds;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        _move = _playerInput.actions["Move"];
        _look = _playerInput.actions["Look"];
        _jump = _playerInput.actions["Jump"];

        // Safer lookup (won't hard-crash if renamed)
        _sprint = _playerInput.actions.FindAction("Sprint", throwIfNotFound: false);

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

        if (playerCamera != null)
            playerCamera.gameObject.SetActive(_inGameScene && isOwner);

        if (_playerInput != null)
            _playerInput.enabled = _inGameScene && isOwner;

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
        if (_frozen) return;

        // Movement input
        Vector2 move = _move.ReadValue<Vector2>();
        Vector3 moveWorld = (transform.right * move.x + transform.forward * move.y);

        if (moveWorld.sqrMagnitude > 1f) moveWorld.Normalize();

        bool isMoving = moveWorld.sqrMagnitude > (minMoveToSprint * minMoveToSprint);

        bool grounded = _cc.isGrounded;
        if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

        if (!grounded && !_wasAirborne)
            _airborneStartY = transform.position.y;

        if (grounded && _wasAirborne)
        {
            float fallDistance = _airborneStartY - transform.position.y;

            if (fallDistance >= _minFallHeight)
                soundFX.PlayImpactSound();
        }

        _wasAirborne = !grounded;
        _jumpTimer -= Time.deltaTime;

        if (_jump != null && _jump.IsPressed() && grounded && _jumpTimer <= 0f)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpTimer = _jumpCooldown;
        }

        // Sprint / stamina
        bool sprintHeld = _sprint != null && _sprint.IsPressed();

        // Can sprint only if: holding sprint, moving, and have stamina
        bool wantsSprint = sprintHeld && isMoving && _staminaSeconds > 0f;

        if (wantsSprint)
        {
            _isSprinting = true;
            _staminaSeconds -= Time.deltaTime;
        }
        else
        {
            _isSprinting = false;

            // Regen whenever not sprinting (including standing still).
            if (!sprintHeld)
            {
                _staminaSeconds += staminaRegenPerSecond * Time.deltaTime;
            }
        }
    

        _staminaSeconds = Mathf.Clamp(_staminaSeconds, 0f, maxStaminaSeconds);

        float finalSpeed = _isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        // Gravity + move
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

        ApplyReset(position, rotation);

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
        if (_cc != null) _cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        _verticalVelocity = 0f;
        _pitch = 0f;

        _staminaSeconds = maxStaminaSeconds;
        _isSprinting = false;

        if (playerCamera != null)
            playerCamera.transform.localEulerAngles = Vector3.zero;

        StartCoroutine(ReenableController());
    }

    private IEnumerator ReenableController()
    {
        yield return new WaitForSeconds(0.2f);
        if (_cc != null) _cc.enabled = true;
    }


    /// <summary>
    /// Called by RedLightGreenLightBoss via ClientRpc to freeze/unfreeze input.
    /// Only acts on the owning client.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        if (!IsOwner) return;
        _frozen = frozen;
    }
}