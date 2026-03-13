using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Networked hinged door interaction with lock, key, and AI path-block rules.
public class HingeDoorInteractable : NetworkBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private Transform hingePivot;

    [Header("Motion")]
    [SerializeField] private float openAngle = 90f;   // degrees yaw
    [SerializeField] private float speed = 6f;        // higher = faster

    [Header("Locking (optional)")]
    [SerializeField] private bool startsLocked = false;
    [SerializeField] private bool requireKey = false;
    [SerializeField] private int requiredKeyItemId = 1;
    [SerializeField] private bool isSecurityDoor = false;
    [SerializeField] private float serverInteractRange = 4f;

    [Header("Audio")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField, Range(0f, 1f)] private float doorMoveVolume = 0.6f;
    [SerializeField] private AudioClip lockedNoKeyClip;
    [SerializeField, Range(0f, 1f)] private float lockedNoKeyVolume = 0.1f;

    [Header("AI Blocking")]
    [SerializeField] private bool blockAiPathWhenClosed = true;
    [SerializeField] private bool autoCreateNavMeshObstacle = true;
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] private Collider aiBlockingCollider;
    [SerializeField] private float fallbackObstacleHeight = 2f;
    [SerializeField] private float fallbackObstacleThickness = 0.3f;
    [SerializeField] private bool forceEnemyRepathOnClose = true;
    [SerializeField] private float repathNotifyRadius = 40f;

    // Defaults for resetting each round
    private bool _defaultOpen = false;   // doors start CLOSED by default
    private bool _defaultLocked;

    private readonly NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isLocked = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool>.OnValueChangedDelegate _onIsOpenChanged;

    private float _current;
    private float _target;

    private void Awake()
    {
        if (hingePivot == null)
            hingePivot = transform;

        EnsureDoorAudioSource();
        EnsureNavMeshObstacle();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Set initial server state
            _defaultOpen = false;
            _defaultLocked = startsLocked;

            isLocked.Value = startsLocked;
            isOpen.Value = false;
        }

        _onIsOpenChanged ??= OnIsOpenChanged;
        isOpen.OnValueChanged += _onIsOpenChanged;

        UpdateTarget();
        ApplyImmediate();
        RefreshAiBlocker();
    }

    public override void OnNetworkDespawn()
    {
        if (_onIsOpenChanged != null)
            isOpen.OnValueChanged -= _onIsOpenChanged;

        base.OnNetworkDespawn();
    }

    public bool CanInteract() => true;

    public bool Interact(Interactor interactor)
    {
        if (interactor == null || !interactor.IsOwner) return false;
        ToggleDoorServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleDoorServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId)) return;

        // Locked door logic
        if (isLocked.Value)
        {
            if (!requireKey)
                return;

            // Require key in inventory
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var client) &&
                client.PlayerObject != null)
            {
                var inv = client.PlayerObject.GetComponent<PlayerInventory>();
                if (inv != null && inv.HasItemId(requiredKeyItemId))
                {
                    isLocked.Value = false;

                    if (isSecurityDoor && ObjectiveState.Instance != null)
                        ObjectiveState.Instance.SecurityDoorUnlocked.Value = true;
                }
                else
                {
                    if (isSecurityDoor && lockedNoKeyClip != null)
                    {
                        var denyParams = new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
                        };
                        PlayLockedNoKeyClientRpc(denyParams);
                    }
                    return; // no key
                }
            }
            else
            {
                return;
            }
        }

        isOpen.Value = !isOpen.Value;
    }

    private void Update()
    {
        // Animate locally on everyone
        _current = Mathf.Lerp(_current, _target, Time.deltaTime * speed);
        SetHinge(_current);
    }

    private void UpdateTarget()
    {
        _target = isOpen.Value ? openAngle : 0f;
    }

    private void OnIsOpenChanged(bool previousValue, bool newValue)
    {
        UpdateTarget();
        RefreshAiBlocker();
        PlayDoorMoveAudio(newValue);

        if (IsServer && !newValue && forceEnemyRepathOnClose)
            StartCoroutine(NotifyEnemiesToRepathAfterClose());
    }

    private void ApplyImmediate()
    {
        _current = isOpen.Value ? openAngle : 0f;
        _target = _current;
        SetHinge(_current);
    }

    private void SetHinge(float yaw)
    {
        if (hingePivot == null) return;
        hingePivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // Called by MatchStartResetter on the server
    public void ServerResetToDefaults()
    {
        if (!IsServer) return;

        isOpen.Value = _defaultOpen;
        isLocked.Value = _defaultLocked;

        UpdateTarget();
        ApplyImmediate();
        RefreshAiBlocker();
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= serverInteractRange;
    }

    private void EnsureNavMeshObstacle()
    {
        if (!blockAiPathWhenClosed)
            return;

        if (navMeshObstacle == null)
        {
            if (hingePivot != null)
                navMeshObstacle = hingePivot.GetComponent<NavMeshObstacle>();
            if (navMeshObstacle == null)
                navMeshObstacle = GetComponent<NavMeshObstacle>();
        }

        if (navMeshObstacle == null && autoCreateNavMeshObstacle)
        {
            var host = hingePivot != null ? hingePivot.gameObject : gameObject;
            navMeshObstacle = host.AddComponent<NavMeshObstacle>();
        }

        if (navMeshObstacle == null)
            return;

        navMeshObstacle.shape = NavMeshObstacleShape.Box;
        navMeshObstacle.carving = true;
        navMeshObstacle.carveOnlyStationary = false;
        navMeshObstacle.carvingMoveThreshold = 0.01f;
        navMeshObstacle.carvingTimeToStationary = 0f;

        ConfigureObstacleBoundsFromCollider();
    }

    private void ConfigureObstacleBoundsFromCollider()
    {
        if (navMeshObstacle == null)
            return;

        Transform host = navMeshObstacle.transform;
        if (!TryResolveBlockingBounds(host, out Bounds bounds))
        {
            navMeshObstacle.center = new Vector3(0f, fallbackObstacleHeight * 0.5f, 0f);
            navMeshObstacle.size = new Vector3(fallbackObstacleThickness, fallbackObstacleHeight, fallbackObstacleThickness);
            return;
        }

        Vector3 centerWs = bounds.center;
        Vector3 centerLocal = host.InverseTransformPoint(centerWs);
        Vector3 sizeWs = bounds.size;
        Vector3 hostLossy = host.lossyScale;

        float sizeX = Mathf.Abs(sizeWs.x / (Mathf.Approximately(hostLossy.x, 0f) ? 1f : hostLossy.x));
        float sizeY = Mathf.Abs(sizeWs.y / (Mathf.Approximately(hostLossy.y, 0f) ? 1f : hostLossy.y));
        float sizeZ = Mathf.Abs(sizeWs.z / (Mathf.Approximately(hostLossy.z, 0f) ? 1f : hostLossy.z));

        navMeshObstacle.center = centerLocal;
        navMeshObstacle.size = new Vector3(
            Mathf.Max(0.05f, sizeX),
            Mathf.Max(0.05f, sizeY),
            Mathf.Max(0.05f, sizeZ));
    }

    private bool TryResolveBlockingBounds(Transform host, out Bounds bounds)
    {
        if (aiBlockingCollider != null && !aiBlockingCollider.isTrigger)
        {
            bounds = aiBlockingCollider.bounds;
            return true;
        }

        Collider candidate = FindFirstSolidCollider(hingePivot);
        if (candidate != null)
        {
            bounds = candidate.bounds;
            return true;
        }

        Renderer visual = FindFirstRenderer(hingePivot);
        if (visual != null)
        {
            bounds = visual.bounds;
            return true;
        }

        if (host != null)
        {
            candidate = FindFirstSolidCollider(host);
            if (candidate != null)
            {
                bounds = candidate.bounds;
                return true;
            }
        }

        candidate = FindFirstSolidCollider(transform);
        if (candidate != null)
        {
            bounds = candidate.bounds;
            return true;
        }

        visual = FindFirstRenderer(transform);
        if (visual != null)
        {
            bounds = visual.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private static Collider FindFirstSolidCollider(Transform root)
    {
        if (root == null)
            return null;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || c.isTrigger)
                continue;
            return c;
        }

        return null;
    }

    private static Renderer FindFirstRenderer(Transform root)
    {
        if (root == null)
            return null;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            return r;
        }

        return null;
    }

    private void RefreshAiBlocker()
    {
        if (!blockAiPathWhenClosed)
        {
            if (navMeshObstacle != null)
                navMeshObstacle.enabled = false;
            return;
        }

        EnsureNavMeshObstacle();
        if (navMeshObstacle == null)
            return;

        if (!isOpen.Value)
            ConfigureObstacleBoundsFromCollider();

        navMeshObstacle.enabled = !isOpen.Value;
    }

    private System.Collections.IEnumerator NotifyEnemiesToRepathAfterClose()
    {
        // Let obstacle + carving settle at least one frame before forcing repath.
        yield return null;
        NotifyNearbyEnemiesToRepath();
        yield return null;
        NotifyNearbyEnemiesToRepath();
    }

    private void NotifyNearbyEnemiesToRepath()
    {
        if (!IsServer)
            return;

        float radius = Mathf.Max(1f, repathNotifyRadius);
        float radiusSqr = radius * radius;
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.IsSpawned)
                continue;

            if ((enemy.transform.position - transform.position).sqrMagnitude > radiusSqr)
                continue;

            enemy.ServerForceRepathNow();
        }
    }

    [ClientRpc]
    private void PlayLockedNoKeyClientRpc(ClientRpcParams clientRpcParams = default)
    {
        PlayDoorClip(lockedNoKeyClip, lockedNoKeyVolume);
    }

    private void PlayDoorMoveAudio(bool opened)
    {
        AudioClip clip = opened ? doorOpenClip : doorCloseClip;
        PlayDoorClip(clip, doorMoveVolume);
    }

    private void PlayDoorClip(AudioClip clip, float volume)
    {
        if (clip == null) return;
        EnsureDoorAudioSource();
        if (doorAudioSource == null) return;
        doorAudioSource.PlayOneShot(clip, volume);
    }

    private void EnsureDoorAudioSource()
    {
        if (doorAudioSource != null) return;

        Transform host = hingePivot != null ? hingePivot : transform;
        doorAudioSource = host.GetComponent<AudioSource>();
        if (doorAudioSource == null)
            doorAudioSource = host.gameObject.AddComponent<AudioSource>();

        doorAudioSource.playOnAwake = false;
        doorAudioSource.spatialBlend = 1f;
        doorAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        doorAudioSource.minDistance = 1f;
        doorAudioSource.maxDistance = 20f;
    }
}