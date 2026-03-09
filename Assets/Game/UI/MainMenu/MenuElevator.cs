using UnityEngine;

public sealed class MenuElevatorMover : MonoBehaviour
{
    [SerializeField] Transform bottomStop;
    [SerializeField] Transform topStop;
    [SerializeField] float speed = 1f;
    [SerializeField] float waitAtStops = 5f;
    [SerializeField] bool startMovingUp = true;

    Vector3 a, b;
    bool toB;
    float wait;

    void Awake()
    {
        if (bottomStop) a = bottomStop.position;
        if (topStop) b = topStop.position;
        toB = startMovingUp;
        wait = waitAtStops;
    }

    void FixedUpdate()
    {
        if (!bottomStop || !topStop) return;
        if (wait > 0f) { wait -= Time.fixedDeltaTime; return; }

        var target = toB ? b : a;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);

        if ((transform.position - target).sqrMagnitude <= 0.0001f)
        {
            toB = !toB;
            wait = waitAtStops;
        }
    }
}