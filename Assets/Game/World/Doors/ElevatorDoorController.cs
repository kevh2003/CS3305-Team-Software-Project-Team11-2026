using Unity.Netcode;
using UnityEngine;

public class ElevatorDoorController : NetworkBehaviour
{
    [Header("Door Panels")]
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;

    [Header("Motion")]
    [Tooltip("How far each panel moves outward from its closed position (local units).")]
    [SerializeField] private float openDistance = 0.75f;

    [Tooltip("Higher = faster movement.")]
    [SerializeField] private float speed = 6f;

    [Tooltip("Which local axis the panels should slide along.")]
    [SerializeField] private Vector3 slideAxis = Vector3.right;

    [Header("State")]
    [SerializeField] private bool startsOpen = false;

    private NetworkVariable<bool> isOpen = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Vector3 _leftClosedLocal;
    private Vector3 _rightClosedLocal;
    private Vector3 _leftOpenLocal;
    private Vector3 _rightOpenLocal;

    private void Awake()
    {
        if (leftPanel == null || rightPanel == null)
            Debug.LogError($"{name}: ElevatorDoorController missing leftPanel/rightPanel references.");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CacheLocalPositions();

        if (IsServer)
            isOpen.Value = startsOpen;

        // apply instantly when spawned
        ApplyImmediate(isOpen.Value);

        isOpen.OnValueChanged += (_, newValue) =>
        {
            // snap target immediately (animation still happens in Update)
            if (newValue) { /* opened */ } else { /* closed */ }
        };
    }

    private void CacheLocalPositions()
    {
        if (leftPanel == null || rightPanel == null) return;

        _leftClosedLocal = leftPanel.localPosition;
        _rightClosedLocal = rightPanel.localPosition;

        // Normalize axis
        Vector3 axis = slideAxis.sqrMagnitude > 0.0001f ? slideAxis.normalized : Vector3.right;

        // Left -axis, right +axis
        _leftOpenLocal = _leftClosedLocal - axis * openDistance;
        _rightOpenLocal = _rightClosedLocal + axis * openDistance;
    }

    private void Update()
    {
        if (leftPanel == null || rightPanel == null) return;

        Vector3 leftTarget = isOpen.Value ? _leftOpenLocal : _leftClosedLocal;
        Vector3 rightTarget = isOpen.Value ? _rightOpenLocal : _rightClosedLocal;

        leftPanel.localPosition = Vector3.Lerp(leftPanel.localPosition, leftTarget, Time.deltaTime * speed);
        rightPanel.localPosition = Vector3.Lerp(rightPanel.localPosition, rightTarget, Time.deltaTime * speed);
    }

    private void ApplyImmediate(bool open)
    {
        if (leftPanel == null || rightPanel == null) return;

        leftPanel.localPosition = open ? _leftOpenLocal : _leftClosedLocal;
        rightPanel.localPosition = open ? _rightOpenLocal : _rightClosedLocal;
    }

    // Called by button on SERVER
    public void ServerOpenPermanently()
    {
        if (!IsServer) return;
        if (isOpen.Value) return; // already open

        isOpen.Value = true;
        if (ObjectiveState.Instance != null)
            ObjectiveState.Instance.ElevatorOpened.Value = true;
    }

    // Match reset hook
    public void ServerResetForNewMatch()
    {
        if (!IsServer) return;
        isOpen.Value = startsOpen;
        ApplyImmediate(isOpen.Value);
    }
}