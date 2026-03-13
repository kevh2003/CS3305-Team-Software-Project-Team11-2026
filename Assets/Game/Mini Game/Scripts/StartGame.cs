using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;

public class StartGame : NetworkBehaviour, IWifiInteractable, IInteractable
{
    private const ulong NoWifiUser = ulong.MaxValue;
    private const int NumberOfParts = 4; // the number of wifi components that need to be locked

    public static bool IsAnyLocalWifiMinigameActive { get; private set; }
    public static int LastExitFrame { get; private set; } = -1;
    public static bool WasExitedThisFrame => Time.frameCount == LastExitFrame;
    private static StartGame ActiveLocalInstance;

    [Header("Player Vars")]
    private CharacterController _player;
    private Canvas _miniGameCanvas;
    private float _lookSensitivity;
    private PlayerSoundFX _soundFX;
    private PlayerHealth _playerHealth;

    [Header("Wifi Game Status")]
    public bool completed = false;
    private bool _completionRequestSent;

    [Header("Server Validation")]
    [SerializeField] private float serverInteractRange = 6f;

    [Header("Locking")]
    [SerializeField] private float lockAcquireTimeoutSeconds = 0.5f;

    [Header("Status Light")]
    [SerializeField] private Light statusLight;
    [SerializeField] private bool autoFindOrCreateStatusLight = true;
    [SerializeField] private Vector3 autoLightLocalOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float statusLightRange = 3.5f;
    [SerializeField] private float statusLightIntensity = 2f;
    [SerializeField] private Color incompleteLightColor = new Color(0.2f, 0.55f, 1f, 1f);
    [SerializeField] private Color completeLightColor = new Color(0.2f, 1f, 0.35f, 1f);

    private NetworkVariable<bool> wifiCompleted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<ulong> inUseByClientId = new(
        NoWifiUser,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Collider _interactionCollider;

    [Header("Interactable")]
    private string text = "Press E to fix WiFi";
    public string InteractText => text;

    private bool _waitingForLock;
    private Coroutine _lockAcquireRoutine;

    private void Awake()
    {
        _interactionCollider = GetComponent<Collider>();
        EnsureStatusLight();
        ApplyCompletionState(wifiCompleted.Value);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        wifiCompleted.OnValueChanged += OnWifiCompletedChanged;
        inUseByClientId.OnValueChanged += OnInUseByClientChanged;

        if (IsServer)
        {
            if (wifiCompleted.Value)
                inUseByClientId.Value = NoWifiUser;
            else if (!IsClientAlive(inUseByClientId.Value))
                inUseByClientId.Value = NoWifiUser;
        }

        ApplyCompletionState(wifiCompleted.Value);
        RefreshInteractText();
        ResetLocalWifiInteractionState();
    }

    public override void OnNetworkDespawn()
    {
        CancelLockAcquire();

        if (_miniGameCanvas != null)
            CloseMiniGame(resetCanvas: true);
        else
            ResetLocalWifiInteractionState();

        wifiCompleted.OnValueChanged -= OnWifiCompletedChanged;
        inUseByClientId.OnValueChanged -= OnInUseByClientChanged;

        base.OnNetworkDespawn();
    }

    private void OnDisable()
    {
        CancelLockAcquire();

        if (_miniGameCanvas != null)
            CloseMiniGame(resetCanvas: true);
        else if (ActiveLocalInstance == this && IsAnyLocalWifiMinigameActive)
            ResetLocalWifiInteractionState();
    }

    public bool CanInteract()
    {
        if (wifiCompleted.Value) return false;
        if (_miniGameCanvas != null) return false;
        if (IsAnyLocalWifiMinigameActive) return false;
        return true;
    }

    public bool Interact(Interactor interactor)
    {
        if (interactor == null || !interactor.IsOwner)
            return false;

        if (wifiCompleted.Value)
            return false;

        if (_miniGameCanvas != null || IsAnyLocalWifiMinigameActive || _waitingForLock)
            return false;

        CharacterController controller = interactor.GetComponent<CharacterController>();
        if (controller == null)
            return false;

        if (!TryGetLocalClientId(out ulong localClientId))
        {
            // Fallback path (single-player/editor setups without net runtime).
            Interact(controller);
            return _miniGameCanvas != null;
        }

        if (IsInUseByAnotherClient(localClientId))
        {
            RefreshInteractText();
            return false;
        }

        CancelLockAcquire();
        _waitingForLock = true;
        RefreshInteractText();
        _lockAcquireRoutine = StartCoroutine(TryEnterWhenLockAcquired(controller, localClientId));
        return false;
    }

    public void Interact(CharacterController player)
    {
        if (player == null) return;
        if (wifiCompleted.Value) return;
        if (IsAnyLocalWifiMinigameActive) return;
        if (_miniGameCanvas != null) return;

        // In networked play, only the lock owner can open this station locally.
        if (TryGetLocalClientId(out ulong localClientId) && inUseByClientId.Value != localClientId)
            return;

        OpenMiniGameLocal(player);
    }

    private void OpenMiniGameLocal(CharacterController player)
    {
        // getting the mini game canvas from the player prefab
        Canvas[] canvas = player.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvas.Length; i++)
        {
            Canvas can = canvas[i];
            if (can != null && can.name == "Mini Game Canvas")
            {
                _miniGameCanvas = can;
                break;
            }
        }

        if (_miniGameCanvas == null)
            return;

        _miniGameCanvas.gameObject.SetActive(true);
        ClampAllDragPiecesToBounds();

        // show the cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // lock the player camera
        NetworkPlayer nPlayer = player.gameObject.GetComponent<NetworkPlayer>();
        if (nPlayer != null)
        {
            _lookSensitivity = nPlayer.LookSensitivity;
            nPlayer.SetLookSensitivity(0f);
        }

        _completionRequestSent = false;
        _player = player;
        _soundFX = player.GetComponent<PlayerSoundFX>();
        _playerHealth = player.GetComponent<PlayerHealth>();
        _soundFX?.StartWifiFixLoop();
        IsAnyLocalWifiMinigameActive = true;
        ActiveLocalInstance = this;
        DropPromptUI.Instance?.SetWifiVisible(true, "Press Q to exit");

        _waitingForLock = false;
        RefreshInteractText();
    }

    private void Update()
    {
        if (ActiveLocalInstance == this
            && IsAnyLocalWifiMinigameActive
            && (_player == null || (_playerHealth != null && _playerHealth.IsDead.Value) || _miniGameCanvas == null))
        {
            if (_miniGameCanvas != null)
            {
                CloseMiniGame(resetCanvas: true);
            }
            else
            {
                if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
                    ReleaseWifiServerRpc();

                ResetLocalWifiInteractionState();
                _player = null;
                _soundFX = null;
                _playerHealth = null;
                _waitingForLock = false;
                RefreshInteractText();
            }
            return;
        }

        // checking if the player quits
        if (_miniGameCanvas != null
            && Keyboard.current != null
            && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Quit();
            return;
        }

        // checking if it is complete
        if (_miniGameCanvas != null && !wifiCompleted.Value && !_completionRequestSent)
        {
            if (HasAllPartsLocked())
            {
                _completionRequestSent = true;
                MarkCompletedServerRpc();
            }
        }
    }

    private IEnumerator TryEnterWhenLockAcquired(CharacterController player, ulong localClientId)
    {
        RequestEnterWifiServerRpc();

        float timeout = Mathf.Max(0.1f, lockAcquireTimeoutSeconds);
        float deadline = Time.time + timeout;

        while (Time.time < deadline)
        {
            if (this == null || !isActiveAndEnabled)
                yield break;

            if (wifiCompleted.Value)
                break;

            if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                break;

            if (IsInUseByClient(localClientId))
            {
                _waitingForLock = false;
                _lockAcquireRoutine = null;
                RefreshInteractText();
                Interact(player);
                yield break;
            }

            if (IsInUseByAnotherClient(localClientId))
                break;

            yield return null;
        }

        _waitingForLock = false;
        _lockAcquireRoutine = null;
        RefreshInteractText();
    }

    private void CancelLockAcquire()
    {
        if (_lockAcquireRoutine != null)
        {
            StopCoroutine(_lockAcquireRoutine);
            _lockAcquireRoutine = null;
        }

        _waitingForLock = false;
        RefreshInteractText();
    }

    public bool IsInUseByClient(ulong clientId)
    {
        return inUseByClientId.Value == clientId;
    }

    public bool IsInUseByAnotherClient(ulong clientId)
    {
        ulong current = inUseByClientId.Value;
        return current != NoWifiUser && current != clientId;
    }

    private void Quit()
    {
        if (_miniGameCanvas == null)
            return;

        CloseMiniGame(resetCanvas: true);
    }

    private void ResetCanvas()
    {
        // method to reset the player canvas
        if (_miniGameCanvas == null) return;

        DraggingBehaviour[] draggables = _miniGameCanvas.GetComponentsInChildren<DraggingBehaviour>(true);
        for (int i = 0; i < draggables.Length; i++)
        {
            DraggingBehaviour drag = draggables[i];
            if (drag == null) continue;

            RectTransform pieceRect = drag.GetComponent<RectTransform>();
            if (pieceRect == null) continue;

            bool wasLocked = drag.locked;
            if (wasLocked)
                drag.locked = false;

            pieceRect.anchoredPosition = wasLocked
                ? GetRandomAnchoredPositionWithinParent(pieceRect)
                : ClampAnchoredPositionWithinParent(pieceRect, pieceRect.anchoredPosition);

            CanvasGroup canvasGroup = drag.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }

    private void ClampAllDragPiecesToBounds()
    {
        if (_miniGameCanvas == null) return;

        DraggingBehaviour[] draggables = _miniGameCanvas.GetComponentsInChildren<DraggingBehaviour>(true);
        for (int i = 0; i < draggables.Length; i++)
        {
            DraggingBehaviour drag = draggables[i];
            if (drag == null) continue;

            RectTransform pieceRect = drag.GetComponent<RectTransform>();
            if (pieceRect == null) continue;

            pieceRect.anchoredPosition = ClampAnchoredPositionWithinParent(pieceRect, pieceRect.anchoredPosition);
        }
    }

    private static Vector2 GetRandomAnchoredPositionWithinParent(RectTransform pieceRect)
    {
        if (pieceRect == null || pieceRect.parent is not RectTransform parentRect)
            return pieceRect != null ? pieceRect.anchoredPosition : Vector2.zero;

        GetAnchoredPositionBounds(pieceRect, parentRect, out float minX, out float maxX, out float minY, out float maxY);

        float x = minX <= maxX ? Random.Range(minX, maxX) : 0.5f * (minX + maxX);
        float y = minY <= maxY ? Random.Range(minY, maxY) : 0.5f * (minY + maxY);
        return new Vector2(x, y);
    }

    private static Vector2 ClampAnchoredPositionWithinParent(RectTransform pieceRect, Vector2 desiredPosition)
    {
        if (pieceRect == null || pieceRect.parent is not RectTransform parentRect)
            return desiredPosition;

        GetAnchoredPositionBounds(pieceRect, parentRect, out float minX, out float maxX, out float minY, out float maxY);

        float x = minX <= maxX ? Mathf.Clamp(desiredPosition.x, minX, maxX) : 0.5f * (minX + maxX);
        float y = minY <= maxY ? Mathf.Clamp(desiredPosition.y, minY, maxY) : 0.5f * (minY + maxY);
        return new Vector2(x, y);
    }

    private static void GetAnchoredPositionBounds(
        RectTransform pieceRect,
        RectTransform parentRect,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        Rect parentBounds = parentRect.rect;

        Vector2 size = pieceRect.rect.size;
        Vector3 localScale = pieceRect.localScale;
        float width = Mathf.Abs(size.x * localScale.x);
        float height = Mathf.Abs(size.y * localScale.y);

        float leftPadding = width * pieceRect.pivot.x;
        float rightPadding = width * (1f - pieceRect.pivot.x);
        float bottomPadding = height * pieceRect.pivot.y;
        float topPadding = height * (1f - pieceRect.pivot.y);

        float anchorCenterX = (pieceRect.anchorMin.x + pieceRect.anchorMax.x) * 0.5f;
        float anchorCenterY = (pieceRect.anchorMin.y + pieceRect.anchorMax.y) * 0.5f;
        float anchorReferenceX = Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, anchorCenterX);
        float anchorReferenceY = Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, anchorCenterY);

        minX = (parentBounds.xMin + leftPadding) - anchorReferenceX;
        maxX = (parentBounds.xMax - rightPadding) - anchorReferenceX;
        minY = (parentBounds.yMin + bottomPadding) - anchorReferenceY;
        maxY = (parentBounds.yMax - topPadding) - anchorReferenceY;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestEnterWifiServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (wifiCompleted.Value) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
            return;

        if (!TryNormalizeLockOwner(senderId))
            return;

        if (inUseByClientId.Value == NoWifiUser || inUseByClientId.Value == senderId)
            inUseByClientId.Value = senderId;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReleaseWifiServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (inUseByClientId.Value == senderId)
            inUseByClientId.Value = NoWifiUser;
    }

    [ServerRpc(RequireOwnership = false)]
    private void MarkCompletedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId))
        {
            Debug.LogWarning($"[StartGame] Reject WiFi completion from {senderId}: out of range.");
            return;
        }

        if (!TryNormalizeLockOwner(senderId) || inUseByClientId.Value != senderId)
        {
            Debug.LogWarning($"[StartGame] Reject WiFi completion from {senderId}: lock not owned.");
            return;
        }

        ServerMarkCompleted();
    }

    private void ServerMarkCompleted()
    {
        if (!IsServer) return;
        if (wifiCompleted.Value) return;

        wifiCompleted.Value = true;
        inUseByClientId.Value = NoWifiUser;

        if (ObjectiveState.Instance != null)
            ObjectiveState.Instance.ServerRegisterWifiFix();
    }

    public void ServerResetForNewRound()
    {
        if (!IsServer) return;

        wifiCompleted.Value = false;
        inUseByClientId.Value = NoWifiUser;
        _completionRequestSent = false;
    }

    public void ServerReleaseIfOwner(ulong clientId)
    {
        if (!IsServer) return;
        if (inUseByClientId.Value == clientId)
            inUseByClientId.Value = NoWifiUser;
    }

    public static void ServerReleaseAllIfOwner(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        var wifiTasks = FindObjectsByType<StartGame>(FindObjectsSortMode.None);
        for (int i = 0; i < wifiTasks.Length; i++)
        {
            StartGame task = wifiTasks[i];
            if (task != null)
                task.ServerReleaseIfOwner(clientId);
        }
    }

    private void OnWifiCompletedChanged(bool oldValue, bool newValue)
    {
        ApplyCompletionState(newValue);

        if (_miniGameCanvas != null)
            CloseMiniGame(resetCanvas: true);
        else
            RefreshInteractText();
    }

    private void OnInUseByClientChanged(ulong oldValue, ulong newValue)
    {
        RefreshInteractText();

        if (_miniGameCanvas == null)
            return;

        ulong localClientId = GetLocalClientIdOrNone();
        if (localClientId == NoWifiUser)
            return;

        // Lock was lost (death/disconnect/reset/completion), close local minigame safely.
        if (newValue != localClientId)
            CloseMiniGame(resetCanvas: true);
    }

    private void ApplyCompletionState(bool isComplete)
    {
        completed = isComplete;
        _completionRequestSent = false;

        if (_interactionCollider == null)
            _interactionCollider = GetComponent<Collider>();

        if (_interactionCollider != null)
            _interactionCollider.enabled = !isComplete;

        ApplyStatusLight(isComplete);
        RefreshInteractText();
    }

    private void EnsureStatusLight()
    {
        if (statusLight == null && autoFindOrCreateStatusLight)
            statusLight = GetComponentInChildren<Light>(true);

        if (statusLight == null && autoFindOrCreateStatusLight)
        {
            GameObject lightObject = new GameObject("WifiStatusLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = autoLightLocalOffset;
            statusLight = lightObject.AddComponent<Light>();
            statusLight.type = LightType.Point;
        }

        if (statusLight == null) return;

        statusLight.type = LightType.Point;
        statusLight.range = statusLightRange;
        statusLight.intensity = statusLightIntensity;
    }

    private void ApplyStatusLight(bool isComplete)
    {
        EnsureStatusLight();
        if (statusLight == null) return;

        statusLight.enabled = true;
        statusLight.color = isComplete ? completeLightColor : incompleteLightColor;
    }

    private bool HasAllPartsLocked()
    {
        if (_miniGameCanvas == null) return false;

        int counter = 0;
        Image[] images = _miniGameCanvas.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null) continue;

            DraggingBehaviour drag = image.GetComponent<DraggingBehaviour>();
            if (drag != null && drag.locked)
                counter++;
        }

        return counter >= NumberOfParts;
    }

    private void CloseMiniGame(bool resetCanvas)
    {
        if (_miniGameCanvas == null)
        {
            CancelLockAcquire();
            return;
        }

        _soundFX?.StopHoldLoopSound();

        if (resetCanvas)
            ResetCanvas();

        _miniGameCanvas.gameObject.SetActive(false);
        _miniGameCanvas = null;
        IsAnyLocalWifiMinigameActive = false;
        LastExitFrame = Time.frameCount;
        if (ActiveLocalInstance == this)
            ActiveLocalInstance = null;
        DropPromptUI.Instance?.SetWifiVisible(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            ReleaseWifiServerRpc();

        if (_player != null)
        {
            NetworkPlayer networkPlayer = _player.gameObject.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
                networkPlayer.SetLookSensitivity(_lookSensitivity);
        }

        _player = null;
        _soundFX = null;
        _playerHealth = null;

        CancelLockAcquire();
        RefreshInteractText();
    }

    private static void ResetLocalWifiInteractionState()
    {
        IsAnyLocalWifiMinigameActive = false;
        LastExitFrame = -1;
        ActiveLocalInstance = null;
        DropPromptUI.Existing?.SetWifiVisible(false);
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        Vector3 playerPos = client.PlayerObject.transform.position;
        float maxSqr = serverInteractRange * serverInteractRange;

        if (_interactionCollider != null)
        {
            Vector3 closest = _interactionCollider.ClosestPoint(playerPos);
            return (closest - playerPos).sqrMagnitude <= maxSqr;
        }

        return (transform.position - playerPos).sqrMagnitude <= maxSqr;
    }

    private bool TryNormalizeLockOwner(ulong requestingClientId)
    {
        if (!IsServer)
            return false;

        ulong current = inUseByClientId.Value;
        if (current == NoWifiUser)
            return true;

        if (!IsClientAlive(current))
            inUseByClientId.Value = NoWifiUser;

        current = inUseByClientId.Value;
        return current == NoWifiUser || current == requestingClientId;
    }

    private bool IsClientAlive(ulong clientId)
    {
        if (clientId == NoWifiUser)
            return false;

        var nm = NetworkManager.Singleton;
        if (nm == null)
            return false;

        if (!nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return false;

        var health = client.PlayerObject.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead.Value)
            return false;

        return true;
    }

    private ulong GetLocalClientIdOrNone()
    {
        return TryGetLocalClientId(out ulong localClientId) ? localClientId : NoWifiUser;
    }

    private static bool TryGetLocalClientId(out ulong localClientId)
    {
        localClientId = NoWifiUser;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient)
            return false;

        localClientId = nm.LocalClientId;
        return true;
    }

    private void RefreshInteractText()
    {
        if (wifiCompleted.Value)
        {
            text = "WiFi fixed";
            return;
        }

        if (_waitingForLock)
        {
            text = "Connecting...";
            return;
        }

        ulong localClientId = GetLocalClientIdOrNone();
        if (localClientId != NoWifiUser && IsInUseByAnotherClient(localClientId))
        {
            text = "WiFi in use";
            return;
        }

        text = "Press E to fix WiFi";
    }
}