using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    private const string PrefSensitivity = "settings_sensitivity";

    [Header("Tuning")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float lookPitchSyncInterval = 0.033f;
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [Header("First Person")]
    [SerializeField] private bool hideOwnerBodyInFirstPerson = false;
    [SerializeField] private bool hideOwnerHeadInFirstPerson = true;
    [Range(0.001f, 1f)]
    [SerializeField] private float ownerHeadScaleMultiplier = 0.01f;
    [SerializeField] private float ownerNearClipPlane = 0.05f;
    [SerializeField] private bool hideLivePlayerBodiesOutsideGameScene = true;
    [SerializeField] private Transform ownerHiddenVisualRoot;
    private PlayerSoundFX soundFX;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float minFallDistanceForImpactSound = 3f;
    private bool _wasAirborne = false;
    private float _airborneStartY = 0f;

    [Header("Sprint / Stamina")]
    [SerializeField] private float sprintMultiplier = 1.6f;     // adjust sprint speed here
    [SerializeField] private float maxStaminaSeconds = 4f;      // sprint time : 4 seconds
    [SerializeField] private float staminaRegenPerSecond = 1.0f; // sprint regen regen time : 4 seconds
    [SerializeField] private float minMoveToSprint = 0.1f;       // prevents sprinting on the spot

    [Header("View Bob")]
    [SerializeField] private float walkBobFrequency = 8.4f;
    [SerializeField] private float runBobFrequency = 12f;
    [SerializeField] private float walkBobAmplitude = 0.03f;
    [SerializeField] private float runBobAmplitude = 0.048f;
    [SerializeField] private float bobLerpSpeed = 10f;

    [Header("Sprint Camera Offset")]
    [SerializeField] private Vector3 sprintCameraLocalOffset = new Vector3(0f, -0.05f, 0.13f);
    [SerializeField] private float sprintOffsetEnterSpeed = 14f;
    [SerializeField] private float sprintOffsetExitSpeed = 10f;

    [Header("Footsteps")]
    [SerializeField] private float walkStepInterval = 0.48f;
    [SerializeField] private float runStepInterval = 0.34f;

    // Exposed for UI later (0..1)
    public float Stamina01 => (maxStaminaSeconds <= 0f) ? 0f : Mathf.Clamp01(_staminaSeconds / maxStaminaSeconds);
    public Transform PlayerCameraTransform => playerCamera != null ? playerCamera.transform : null;
    public readonly NetworkVariable<float> LookPitch = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<int> _selectedAvatarId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> _readyInLobby = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Header("Avatar Selection")]
    [SerializeField] private AvatarCatalog avatarCatalog;
    [SerializeField] private Transform avatarVisualRoot;

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
    private Vector3 _cameraBaseLocalPos;
    private Vector3 _defaultCameraLocalPos;
    private float _bobTime;
    private float _currentBobOffset;
    private Vector3 _currentSprintCameraOffset;
    private float _footstepTimer;

    private CharacterController _cc;
    private PlayerInput _playerInput;
    private Renderer[] _ownerHiddenRenderers = System.Array.Empty<Renderer>();
    private float _defaultNearClipPlane = -1f;
    private Transform _ownerHeadTransform;
    private Vector3 _ownerHeadOriginalScale = Vector3.one;
    private bool _hasOwnerHeadScale;
    private float _lastSyncedPitch = float.NaN;
    private float _nextPitchSyncTime;
    private bool _avatarDefaultsInitialized;
    private Transform _defaultAvatarVisualRoot;
    private Vector3 _defaultAvatarLocalPosition;
    private Quaternion _defaultAvatarLocalRotation;
    private Vector3 _defaultAvatarLocalScale = Vector3.one;
    private RuntimeAnimatorController _defaultAvatarAnimatorController;
    private float _defaultControllerHeight = 2f;
    private float _defaultControllerRadius = 0.5f;
    private Vector3 _defaultControllerCenter = Vector3.zero;
    private GameObject _runtimeAvatarInstance;
    private GameObject _runtimeAvatarSourcePrefab;

    public int SelectedAvatarId => _selectedAvatarId.Value;
    public bool IsReadyInLobby => _readyInLobby.Value;
    public float LookSensitivity => lookSensitivity;

    public event System.Action<NetworkPlayer> LobbyStateChanged;
    public static event System.Action<NetworkPlayer> AnyLobbyStateChanged;

    public void SetLookSensitivity(float value)
    {
        lookSensitivity = Mathf.Clamp(value, 0.02f, 2f);
    }

    private void Awake()
    {   
        soundFX = GetComponent<PlayerSoundFX>();
        _cc = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();

        _staminaSeconds = maxStaminaSeconds;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerCamera != null)
        {
            _cameraBaseLocalPos = playerCamera.transform.localPosition;
            _defaultCameraLocalPos = _cameraBaseLocalPos;
            _defaultNearClipPlane = playerCamera.nearClipPlane;
        }

        InitializeAvatarDefaults();
        CacheOwnerHiddenRenderers();
        CacheOwnerHeadTransform();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (_ownerHiddenRenderers.Length == 0)
            CacheOwnerHiddenRenderers();
        if (_ownerHeadTransform == null)
            CacheOwnerHeadTransform();

        _move = _playerInput.actions["Move"];
        _look = _playerInput.actions["Look"];
        _jump = _playerInput.actions["Jump"];
        if (IsOwner)
            lookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefSensitivity, lookSensitivity), 0.02f, 2f);

        // Safer lookup (won't hard-crash if renamed)
        _sprint = _playerInput.actions.FindAction("Sprint", throwIfNotFound: false);

        _selectedAvatarId.OnValueChanged -= OnSelectedAvatarChanged;
        _selectedAvatarId.OnValueChanged += OnSelectedAvatarChanged;
        _readyInLobby.OnValueChanged -= OnReadyStateChanged;
        _readyInLobby.OnValueChanged += OnReadyStateChanged;

        if (IsServer)
        {
            int count = AvatarCatalogUtility.GetAvatarCount(avatarCatalog);
            int max = Mathf.Max(0, count - 1);
            _selectedAvatarId.Value = Mathf.Clamp(_selectedAvatarId.Value, 0, max);
        }

        ApplySelectedAvatarPresentation();
        ApplySceneState(SceneManager.GetActiveScene().name);
        NotifyLobbyStateChanged();

        if (IsOwner)
            SyncLookPitch(force: true);
    }

    public override void OnNetworkDespawn()
    {
        _selectedAvatarId.OnValueChanged -= OnSelectedAvatarChanged;
        _readyInLobby.OnValueChanged -= OnReadyStateChanged;
        if (_runtimeAvatarInstance != null)
        {
            Destroy(_runtimeAvatarInstance);
            _runtimeAvatarInstance = null;
            _runtimeAvatarSourcePrefab = null;
        }
        base.OnNetworkDespawn();
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
        _inGameScene = sceneName == gameSceneName;
        bool isOwner = IsOwner;

        if (IsServer && _inGameScene && _readyInLobby.Value)
            _readyInLobby.Value = false;

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(_inGameScene && isOwner);
            if (_inGameScene && isOwner)
            {
                playerCamera.nearClipPlane = Mathf.Max(0.01f, ownerNearClipPlane);
            }
            else if (_defaultNearClipPlane > 0f)
            {
                playerCamera.nearClipPlane = _defaultNearClipPlane;
            }
        }

        if (_playerInput != null)
            _playerInput.enabled = _inGameScene && isOwner;

        bool showLiveBody = _inGameScene || !hideLivePlayerBodiesOutsideGameScene;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = showLiveBody;

        // Owner body hiding is optional; by default the owner still sees their body.
        bool ownerBodyShouldBeVisible = !isOwner || !hideOwnerBodyInFirstPerson;
        SetOwnerBodyVisible(_inGameScene && ownerBodyShouldBeVisible);
        ApplyOwnerHeadVisibility(_inGameScene && isOwner && hideOwnerHeadInFirstPerson);

        if (_inGameScene && isOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
                playerCamera.transform.localPosition = _cameraBaseLocalPos;
            _currentSprintCameraOffset = Vector3.zero;
            if (isOwner)
                SyncLookPitch(force: true);
        }
        else if (!_inGameScene && isOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        NotifyLobbyStateChanged();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (!_inGameScene) return;
        if (_playerInput == null || !_playerInput.enabled) return;
        if (_cc == null || !_cc.enabled) return;

        // Movement input
        Vector2 move = _frozen ? Vector2.zero : _move.ReadValue<Vector2>();
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

            if (fallDistance >= minFallDistanceForImpactSound)
                soundFX?.PlayImpactSound();
        }

        _wasAirborne = !grounded;
        _jumpTimer -= Time.deltaTime;

        if (!_frozen && _jump != null && _jump.IsPressed() && grounded && _jumpTimer <= 0f)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpTimer = _jumpCooldown;
            soundFX?.PlayJumpSound();
        }

        // Sprint / stamina
        bool sprintHeld = !_frozen && _sprint != null && _sprint.IsPressed();

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
        UpdateFootsteps(isMoving, grounded);

        // Mouse/controller look from Input Actions
        Vector2 look = _look.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f);

        _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
        SyncLookPitch();
        if (playerCamera != null)
        {
            playerCamera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
            bool sprintOffsetActive = _isSprinting && isMoving && grounded && !_frozen;
            UpdateViewBob(isMoving, grounded, sprintOffsetActive);
        }
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
        SyncLookPitch(force: true);

        _staminaSeconds = maxStaminaSeconds;
        _isSprinting = false;
        _bobTime = 0f;
        _currentBobOffset = 0f;
        _currentSprintCameraOffset = Vector3.zero;
        _footstepTimer = 0f;

        if (playerCamera != null)
        {
            playerCamera.transform.localEulerAngles = Vector3.zero;
            playerCamera.transform.localPosition = _cameraBaseLocalPos;
        }

        bool ownerBodyShouldBeVisible = !IsOwner || !hideOwnerBodyInFirstPerson;
        SetOwnerBodyVisible(_inGameScene && ownerBodyShouldBeVisible);
        ApplyOwnerHeadVisibility(_inGameScene && IsOwner && hideOwnerHeadInFirstPerson);

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

    private void UpdateViewBob(bool isMoving, bool grounded, bool sprintOffsetActive)
    {
        if (playerCamera == null) return;

        float targetOffset = 0f;
        bool bobActive = !_frozen && grounded && isMoving;

        if (bobActive)
        {
            float freq = _isSprinting ? runBobFrequency : walkBobFrequency;
            float amp = _isSprinting ? runBobAmplitude : walkBobAmplitude;
            _bobTime += Time.deltaTime * freq;
            targetOffset = Mathf.Sin(_bobTime) * amp;
        }
        else
        {
            _bobTime = 0f;
        }

        _currentBobOffset = Mathf.Lerp(_currentBobOffset, targetOffset, bobLerpSpeed * Time.deltaTime);

        Vector3 targetSprintOffset = sprintOffsetActive ? sprintCameraLocalOffset : Vector3.zero;
        float sprintLerpSpeed = sprintOffsetActive ? sprintOffsetEnterSpeed : sprintOffsetExitSpeed;
        _currentSprintCameraOffset = Vector3.Lerp(_currentSprintCameraOffset, targetSprintOffset, sprintLerpSpeed * Time.deltaTime);

        playerCamera.transform.localPosition = _cameraBaseLocalPos + _currentSprintCameraOffset + new Vector3(0f, _currentBobOffset, 0f);
    }

    private void UpdateFootsteps(bool isMoving, bool grounded)
    {
        if (!IsOwner || soundFX == null)
            return;

        bool canStep = !_frozen && grounded && isMoving;
        if (!canStep)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer += Time.deltaTime;
        float stepInterval = _isSprinting ? runStepInterval : walkStepInterval;
        stepInterval = Mathf.Max(0.08f, stepInterval);

        if (_footstepTimer < stepInterval)
            return;

        _footstepTimer = 0f;
        if (_isSprinting)
            soundFX.PlayRunFootstepSound();
        else
            soundFX.PlayWalkFootstepSound();
    }

    private void InitializeAvatarDefaults()
    {
        if (_avatarDefaultsInitialized) return;

        _defaultControllerHeight = _cc != null ? _cc.height : _defaultControllerHeight;
        _defaultControllerRadius = _cc != null ? _cc.radius : _defaultControllerRadius;
        _defaultControllerCenter = _cc != null ? _cc.center : _defaultControllerCenter;

        if (playerCamera != null)
        {
            _cameraBaseLocalPos = playerCamera.transform.localPosition;
            _defaultCameraLocalPos = _cameraBaseLocalPos;
        }

        if (avatarVisualRoot == null)
            avatarVisualRoot = FindDefaultAvatarVisualRoot();

        _defaultAvatarVisualRoot = avatarVisualRoot;
        if (_defaultAvatarVisualRoot != null)
        {
            _defaultAvatarLocalPosition = _defaultAvatarVisualRoot.localPosition;
            _defaultAvatarLocalRotation = _defaultAvatarVisualRoot.localRotation;
            _defaultAvatarLocalScale = _defaultAvatarVisualRoot.localScale;

            Animator animator = _defaultAvatarVisualRoot.GetComponentInChildren<Animator>(true);
            if (animator != null)
                _defaultAvatarAnimatorController = animator.runtimeAnimatorController;
        }

        _avatarDefaultsInitialized = true;
    }

    private Transform FindDefaultAvatarVisualRoot()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            if (playerCamera != null && child == playerCamera.transform) continue;
            if (child.GetComponent<Camera>() != null) continue;
            if (child.GetComponent<Canvas>() != null) continue;
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;
            return child;
        }

        return null;
    }

    private void ApplySelectedAvatarPresentation()
    {
        InitializeAvatarDefaults();

        AvatarCatalog catalog = ResolveAvatarCatalog();
        AvatarCatalog.AvatarEntry entry = default;
        bool hasEntry = catalog != null && catalog.TryGet(_selectedAvatarId.Value, out entry);

        GameObject desiredPrefab = null;
        if (hasEntry)
            desiredPrefab = entry.gameplayPrefab != null ? entry.gameplayPrefab : entry.previewPrefab;

        bool useDefaultAvatar = _selectedAvatarId.Value <= 0 || desiredPrefab == null;
        if (useDefaultAvatar)
        {
            if (_runtimeAvatarInstance != null)
            {
                Destroy(_runtimeAvatarInstance);
                _runtimeAvatarInstance = null;
                _runtimeAvatarSourcePrefab = null;
            }

            if (_defaultAvatarVisualRoot != null)
            {
                _defaultAvatarVisualRoot.gameObject.SetActive(true);
                ApplyAvatarTransform(_defaultAvatarVisualRoot, hasEntry ? entry : default);
                ApplyAnimatorController(_defaultAvatarVisualRoot, hasEntry ? entry.gameplayAnimatorController : null);
                EnsureAnimationStateController(_defaultAvatarVisualRoot);
                avatarVisualRoot = _defaultAvatarVisualRoot;
            }
        }
        else
        {
            if (_defaultAvatarVisualRoot != null)
                _defaultAvatarVisualRoot.gameObject.SetActive(false);

            if (_runtimeAvatarInstance == null || _runtimeAvatarSourcePrefab != desiredPrefab)
            {
                if (_runtimeAvatarInstance != null)
                    Destroy(_runtimeAvatarInstance);

                _runtimeAvatarInstance = Instantiate(desiredPrefab, transform);
                _runtimeAvatarInstance.name = desiredPrefab.name + "_Avatar";
                _runtimeAvatarSourcePrefab = desiredPrefab;
            }

            Transform runtimeVisual = _runtimeAvatarInstance.transform;
            ApplyAvatarTransform(runtimeVisual, entry);

            RuntimeAnimatorController controller = entry.gameplayAnimatorController != null
                ? entry.gameplayAnimatorController
                : _defaultAvatarAnimatorController;
            ApplyAnimatorController(runtimeVisual, controller);
            EnsureAnimationStateController(runtimeVisual);
            avatarVisualRoot = runtimeVisual;
        }

        ApplyAvatarCameraAndController(hasEntry ? entry : default);

        CacheOwnerHiddenRenderers();
        CacheOwnerHeadTransform();

        bool ownerBodyShouldBeVisible = !IsOwner || !hideOwnerBodyInFirstPerson;
        SetOwnerBodyVisible(_inGameScene && ownerBodyShouldBeVisible);
        ApplyOwnerHeadVisibility(_inGameScene && IsOwner && hideOwnerHeadInFirstPerson);
    }

    private void ApplyAvatarTransform(Transform visualRoot, AvatarCatalog.AvatarEntry entry)
    {
        if (visualRoot == null || _defaultAvatarVisualRoot == null) return;

        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = _defaultAvatarLocalPosition + entry.gameplayPositionOffset;
        visualRoot.localRotation = _defaultAvatarLocalRotation * Quaternion.Euler(entry.gameplayEulerOffset);
        visualRoot.localScale = Vector3.Scale(_defaultAvatarLocalScale, ResolveScaleMultiplier(entry.gameplayScaleMultiplier));
    }

    private static Vector3 ResolveScaleMultiplier(Vector3 value)
    {
        bool allZero = Mathf.Approximately(value.x, 0f)
                       && Mathf.Approximately(value.y, 0f)
                       && Mathf.Approximately(value.z, 0f);
        if (allZero) return Vector3.one;

        return new Vector3(
            Mathf.Approximately(value.x, 0f) ? 1f : value.x,
            Mathf.Approximately(value.y, 0f) ? 1f : value.y,
            Mathf.Approximately(value.z, 0f) ? 1f : value.z);
    }

    private void ApplyAvatarCameraAndController(AvatarCatalog.AvatarEntry entry)
    {
        _cameraBaseLocalPos = _defaultCameraLocalPos + entry.cameraLocalOffset;
        if (playerCamera != null)
            playerCamera.transform.localPosition = _cameraBaseLocalPos + _currentSprintCameraOffset + new Vector3(0f, _currentBobOffset, 0f);

        if (_cc == null) return;

        if (entry.overrideController)
        {
            _cc.height = Mathf.Max(0.2f, entry.controllerHeight);
            _cc.radius = Mathf.Max(0.05f, entry.controllerRadius);
            _cc.center = entry.controllerCenter;
            return;
        }

        _cc.height = _defaultControllerHeight;
        _cc.radius = _defaultControllerRadius;
        _cc.center = _defaultControllerCenter;
    }

    private static void ApplyAnimatorController(Transform visualRoot, RuntimeAnimatorController controller)
    {
        if (visualRoot == null || controller == null) return;

        Animator animator = visualRoot.GetComponent<Animator>();
        if (animator == null)
            animator = visualRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visualRoot.gameObject.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
    }

    private static void EnsureAnimationStateController(Transform visualRoot)
    {
        if (visualRoot == null) return;

        Animator animator = visualRoot.GetComponent<Animator>();
        if (animator == null)
            animator = visualRoot.GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        if (animator.GetComponent<AnimationStateController>() == null)
            animator.gameObject.AddComponent<AnimationStateController>();
    }

    private void CacheOwnerHiddenRenderers()
    {
        if (avatarVisualRoot != null)
        {
            ownerHiddenVisualRoot = avatarVisualRoot;
        }
        else if (ownerHiddenVisualRoot == null || !ownerHiddenVisualRoot.IsChildOf(transform))
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
                ownerHiddenVisualRoot = animator.transform;
        }

        if (ownerHiddenVisualRoot != null)
            _ownerHiddenRenderers = ownerHiddenVisualRoot.GetComponentsInChildren<Renderer>(true);
        else
            _ownerHiddenRenderers = System.Array.Empty<Renderer>();
    }

    private void SetOwnerBodyVisible(bool visible)
    {
        for (int i = 0; i < _ownerHiddenRenderers.Length; i++)
        {
            var r = _ownerHiddenRenderers[i];
            if (r != null)
                r.enabled = visible;
        }
    }

    private void CacheOwnerHeadTransform()
    {
        _ownerHeadTransform = null;
        _hasOwnerHeadScale = false;

        Animator animator = null;
        if (avatarVisualRoot != null)
            animator = avatarVisualRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        if (animator.isHuman)
            _ownerHeadTransform = animator.GetBoneTransform(HumanBodyBones.Head);

        if (_ownerHeadTransform == null)
        {
            var all = animator.transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _ownerHeadTransform = all[i];
                    break;
                }
            }
        }

        if (_ownerHeadTransform != null)
        {
            _ownerHeadOriginalScale = _ownerHeadTransform.localScale;
            _hasOwnerHeadScale = true;
        }
    }

    private void ApplyOwnerHeadVisibility(bool hideHead)
    {
        if (_ownerHeadTransform == null || !_hasOwnerHeadScale) return;

        if (hideHead)
        {
            float s = Mathf.Clamp(ownerHeadScaleMultiplier, 0.001f, 1f);
            _ownerHeadTransform.localScale = _ownerHeadOriginalScale * s;
        }
        else
        {
            _ownerHeadTransform.localScale = _ownerHeadOriginalScale;
        }
    }

    private void SyncLookPitch(bool force = false)
    {
        if (!IsSpawned || !IsOwner) return;
        if (NetworkManager == null || !NetworkManager.IsListening) return;
        if (OwnerClientId != NetworkManager.LocalClientId) return;

        float now = Time.unscaledTime;
        bool intervalDue = now >= _nextPitchSyncTime;
        bool changed = float.IsNaN(_lastSyncedPitch) || Mathf.Abs(_pitch - _lastSyncedPitch) >= 0.2f;

        if (!force && !intervalDue && !changed) return;

        _lastSyncedPitch = _pitch;
        _nextPitchSyncTime = now + Mathf.Max(0.01f, lookPitchSyncInterval);
        LookPitch.Value = _pitch;
    }

    public AvatarCatalog ResolveAvatarCatalog()
    {
        return AvatarCatalogUtility.ResolveCatalog(avatarCatalog);
    }

    public void RequestSetSelectedAvatar(int avatarId)
    {
        if (!IsOwner) return;
        RequestSetSelectedAvatarServerRpc(avatarId);
    }

    public void RequestSetReadyInLobby(bool ready)
    {
        if (!IsOwner) return;
        RequestSetReadyInLobbyServerRpc(ready);
    }

    private void OnSelectedAvatarChanged(int previousValue, int newValue)
    {
        ApplySelectedAvatarPresentation();
        NotifyLobbyStateChanged();
    }

    private void OnReadyStateChanged(bool previousValue, bool newValue)
    {
        NotifyLobbyStateChanged();
    }

    private void NotifyLobbyStateChanged()
    {
        LobbyStateChanged?.Invoke(this);
        AnyLobbyStateChanged?.Invoke(this);
    }

    [ServerRpc]
    private void RequestSetSelectedAvatarServerRpc(int requestedAvatarId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != OwnerClientId) return;

        int count = AvatarCatalogUtility.GetAvatarCount(avatarCatalog);
        int max = Mathf.Max(0, count - 1);
        int validated = Mathf.Clamp(requestedAvatarId, 0, max);

        _selectedAvatarId.Value = validated;
        _readyInLobby.Value = false;
    }

    [ServerRpc]
    private void RequestSetReadyInLobbyServerRpc(bool ready, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != OwnerClientId) return;
        _readyInLobby.Value = ready;
    }
}