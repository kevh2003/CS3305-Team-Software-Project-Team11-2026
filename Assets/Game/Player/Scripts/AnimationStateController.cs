using UnityEngine;

// Drives walk/run animator booleans from smoothed movement speed.
public class AnimationStateController : MonoBehaviour
{
    private enum MoveAnimState
    {
        Idle,
        Walking,
        Running
    }

    private Animator animator;
    private Vector3 _lastWorldPos;
    private float _smoothedSpeed;
    private MoveAnimState _currentState = MoveAnimState.Idle;

    [Header("Speed Thresholds")]
    [SerializeField] private float walkStartSpeed = 0.08f;
    [SerializeField] private float runStartSpeed = 5.6f;
    [SerializeField] private float speedSmoothing = 12f;
    [SerializeField] private float transitionDuration = 0.12f;
    [SerializeField] private float teleportResetDistance = 3f;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.idle");
    private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.walking");
    private static readonly int RunStateHash = Animator.StringToHash("Base Layer.running");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        _lastWorldPos = transform.position;
    }

    private void OnEnable()
    {
        _lastWorldPos = transform.position;
        _smoothedSpeed = 0f;
        _currentState = MoveAnimState.Idle;
    }

    private void Update()
    {
        if (animator == null) return;

        Vector3 delta = transform.position - _lastWorldPos;
        _lastWorldPos = transform.position;

        float rawSpeed = (Time.deltaTime > 0f) ? (delta.magnitude / Time.deltaTime) : 0f;
        if (delta.sqrMagnitude >= teleportResetDistance * teleportResetDistance)
            rawSpeed = 0f;

        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, speedSmoothing * Time.deltaTime);

        MoveAnimState targetState;
        if (_smoothedSpeed >= runStartSpeed) targetState = MoveAnimState.Running;
        else if (_smoothedSpeed >= walkStartSpeed) targetState = MoveAnimState.Walking;
        else targetState = MoveAnimState.Idle;

        bool isWalking = targetState == MoveAnimState.Walking;
        bool isRunning = targetState == MoveAnimState.Running;

        animator.SetBool(IsWalkingHash, isWalking);
        animator.SetBool(IsRunningHash, isRunning);

        if (targetState != _currentState)
        {
            int stateHash = targetState switch
            {
                MoveAnimState.Running => RunStateHash,
                MoveAnimState.Walking => WalkStateHash,
                _ => IdleStateHash
            };

            animator.CrossFade(stateHash, transitionDuration, 0);
            _currentState = targetState;
        }
    }
}