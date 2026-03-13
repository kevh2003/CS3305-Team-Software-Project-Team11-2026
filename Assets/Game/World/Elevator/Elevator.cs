using Unity.Netcode;
using UnityEngine;

// Server-driven elevator platform movement between two configured stops.
public class ElevatorMover : NetworkBehaviour
{
    [Header("Stops (world space)")]
    [SerializeField] private Transform bottomStop;
    [SerializeField] private Transform topStop;

    [Header("Motion")]
    [SerializeField] private float speed = 1.0f;          // units per second
    [SerializeField] private float waitAtStops = 5.0f;     // seconds
    [SerializeField] private bool startMovingUp = true;

    private Vector3 _a;
    private Vector3 _b;
    private bool _towardsB;
    private float _waitTimer;

    private void Awake()
    {
        if (bottomStop != null) _a = bottomStop.position;
        if (topStop != null) _b = topStop.position;
        _towardsB = startMovingUp;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Re-cache in case stops moved in editor
        if (bottomStop != null) _a = bottomStop.position;
        if (topStop != null) _b = topStop.position;

        _waitTimer = waitAtStops;
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (bottomStop == null || topStop == null) return;

        // Wait at ends
        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 target = _towardsB ? _b : _a;

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);

        // Arrived?
        if ((transform.position - target).sqrMagnitude <= 0.0001f)
        {
            _towardsB = !_towardsB;
            _waitTimer = waitAtStops;
        }
    }
}