using Unity.Netcode;
using UnityEngine;

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

    // Defaults for resetting each round
    private bool _defaultOpen = false;   // doors start CLOSED by default
    private bool _defaultLocked;

    private readonly NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isLocked = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float _current;
    private float _target;

    private void Awake()
    {
        if (hingePivot == null)
            hingePivot = transform;
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

        isOpen.OnValueChanged += (_, __) => UpdateTarget();
        UpdateTarget();
        ApplyImmediate();
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
        // Locked door logic
        if (isLocked.Value)
        {
            if (!requireKey)
                return;

            // Require key in inventory
            ulong senderId = rpcParams.Receive.SenderClientId;

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
    }
}