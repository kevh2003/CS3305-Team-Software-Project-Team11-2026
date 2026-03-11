using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class Interactor : NetworkBehaviour
{
    private const int MaxProbeHits = 16;

    [Header("Raycast Settings")]
    public Transform InteractSource;
    public float InteractRange = 3.6f;
    [Tooltip("Adds thickness to the interaction probe so close/low objects are easier to target.")]
    public float InteractProbeRadius = 0.2f;

    public NetworkPlayer Player { get; private set; }
    private Crosshair crosshair;
    private IInteractable currentInteractable;

    [Header("References")]
    private PlayerSoundFX soundFX;

    [Header("Hold to interact")]
    private Coroutine holdRoutine;
    private PCInteractable holdingPc;
    private GradesRackInteractable holdingGrades;
    private readonly RaycastHit[] _sphereHits = new RaycastHit[MaxProbeHits];
    private readonly RaycastHit[] _rayHits = new RaycastHit[MaxProbeHits];

    void Awake()
    {
        soundFX = GetComponent<PlayerSoundFX>();
        Player = GetComponent<NetworkPlayer>();

        if (Player == null)
            Debug.LogError("Interactor: NetworkPlayer component not found");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        crosshair = GetComponent<Crosshair>();

        if (InteractSource == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null) InteractSource = cam.transform;
            else Debug.LogError("Interactor: No camera found");
        }

        // Interact UI should only appear while aiming at a valid target.
        crosshair?.HideInteractPrompt();
    }

    void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();

        // If currently holding a PC or Grades interaction, cancel if E is released
        if (holdingPc != null || holdingGrades != null)
        {
            bool eHeld = (Keyboard.current != null && Keyboard.current.eKey.isPressed);
            if (!eHeld)
            {
                CancelHold();
                return;
            }
        }
    }

    void CheckForInteractable()
    {
        if (InteractSource == null) return;

        Ray ray = new Ray(InteractSource.position, InteractSource.forward);

        if (TryGetValidHit(ray, out RaycastHit hit))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                if (currentInteractable != interactable)
                {
                    // If swapped targets mid-hold, cancel the hold instantly
                    if (holdingPc != null || holdingGrades != null)
                        CancelHold();

                    currentInteractable = interactable;

                }

                if (crosshair != null)
                    crosshair.ShowInteractPrompt();
            }
            else
            {
                ClearInteractable();
            }
        }
        else
        {
            ClearInteractable();
        }
    }

    private bool TryGetValidHit(Ray ray, out RaycastHit hit)
    {
        if (TryGetClosestNonSelfHit(ray, useSphereProbe: true, out hit))
            return true;

        return TryGetClosestNonSelfHit(ray, useSphereProbe: false, out hit);
    }

    private bool TryGetClosestNonSelfHit(Ray ray, bool useSphereProbe, out RaycastHit closestHit)
    {
        closestHit = default;

        int hitCount;
        RaycastHit[] hits;

        if (useSphereProbe && InteractProbeRadius > 0f)
        {
            hitCount = Physics.SphereCastNonAlloc(
                ray,
                InteractProbeRadius,
                _sphereHits,
                InteractRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            hits = _sphereHits;
        }
        else
        {
            hitCount = Physics.RaycastNonAlloc(
                ray,
                _rayHits,
                InteractRange,
                ~0,
                QueryTriggerInteraction.Ignore);
            hits = _rayHits;
        }

        if (hitCount <= 0) return false;

        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var candidate = hits[i];
            var col = candidate.collider;
            if (col == null) continue;

            if (col.transform.IsChildOf(transform)) continue;

            if (candidate.distance < bestDistance)
            {
                bestDistance = candidate.distance;
                closestHit = candidate;
                found = true;
            }
        }

        return found;
    }

    void ClearInteractable()
    {
        if (holdingPc != null || holdingGrades != null)
            CancelHold();

        currentInteractable = null;

        if (crosshair != null)
            crosshair.HideInteractPrompt();
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) return;

        bool pressed = value.isPressed;

        // release: cancel hold immediately
        if (!pressed)
        {
            CancelHold();
            return;
        }

        if (currentInteractable == null) return;
        if (!currentInteractable.CanInteract()) return;

        // If this is a PC assignment, start holding logic instead of instant interact.
        var pc = (currentInteractable as Component)?.GetComponentInParent<PCInteractable>();
        if (pc != null)
        {
            StartHold(pc);
            return;
        }

        // If this is the grades rack, hold-to-complete
        var grades = (currentInteractable as Component)?.GetComponentInParent<GradesRackInteractable>();
        if (grades != null)
        {
            StartHoldGrades(grades);
            return;
        }

        // Normal interactables (ducks, pickups, doors, etc.)
        bool success = currentInteractable.Interact(this);
        if (success)
        {
            soundFX?.PlayInteractSound();
            ClearInteractable();
        }
    }

    private void StartHold(PCInteractable pc)
    {
        CancelHold();

        // If already submitted this round, don't allow starting again
        if (ObjectiveState.Instance != null &&
            NetworkManager.Singleton != null &&
            ObjectiveState.Instance.HasSubmittedClient(NetworkManager.Singleton.LocalClientId))
        {
            TrySetInteractPrompt("Already submitted.");
            return;
        }

        holdingPc = pc;

        // show starting prompt immediately
        TrySetInteractPrompt("Submitting... 0% (hold E)");
        soundFX?.PlayInteractSound();
        soundFX?.StartAssignmentTypingLoop();

        holdRoutine = StartCoroutine(HoldToSubmitRoutine(pc));
    }

    private void CancelHold()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        soundFX?.StopHoldLoopSound();

        holdingPc = null;
        holdingGrades = null;

        if (crosshair != null)
        {
            if (currentInteractable != null)
            {
                var pc = (currentInteractable as Component)?.GetComponentInParent<PCInteractable>();
                if (pc != null)
                {
                    crosshair.SetPromptText("Hold E to submit");
                }
                else
                {
                    var grades = (currentInteractable as Component)?.GetComponentInParent<GradesRackInteractable>();
                    if (grades != null)
                        crosshair.SetPromptText("Hold E to change grades");
                    else
                        crosshair.ShowInteractPrompt();
                }
            }
            else
            {
                crosshair.HideInteractPrompt();
            }
        }
    }

    private IEnumerator HoldToSubmitRoutine(PCInteractable pc)
    {
        if (pc == null || !pc.gameObject.activeInHierarchy)
        {
            CancelHold();
            yield break;
        }

        float duration = Mathf.Max(0.1f, pc.holdSeconds);
        float t = 0f;

        while (t < duration)
        {
            // Cancel if player looked away / lost the interactable / PC disabled
            if (currentInteractable == null || pc == null || !pc.gameObject.activeInHierarchy)
            {
                CancelHold();
                yield break;
            }

            // Cancel if E released
            if (Keyboard.current != null && !Keyboard.current.eKey.isPressed)
            {
                CancelHold();
                yield break;
            }

            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / duration);

            TrySetInteractPrompt($"Submitting... {Mathf.RoundToInt(pct * 100f)}% (hold E)");
            yield return null;
        }

        TrySetInteractPrompt("Submitted!");

        pc.SubmitAssignmentServerRpc();

        // Prevent immediately restarting
        ClearInteractable();
        CancelHold();
    }

    private void TrySetInteractPrompt(string text)
    {
        if (crosshair != null)
            crosshair.SetPromptText(text);
    }

    private void OnDrawGizmosSelected()
    {
        if (InteractSource != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(InteractSource.position, InteractSource.forward * InteractRange);
            Gizmos.DrawWireSphere(InteractSource.position + (InteractSource.forward * InteractRange), InteractProbeRadius);
        }
    }

    private void OnDisable()
    {
        CancelHold();
    }

    private void StartHoldGrades(GradesRackInteractable g)
    {
        CancelHold();

        if (g == null) return;

        // If already done, don't allow starting again
        if (ObjectiveState.Instance != null && ObjectiveState.Instance.GradesChanged.Value)
        {
            TrySetInteractPrompt("Already changed.");
            return;
        }

        holdingGrades = g;

        // show starting prompt immediately
        TrySetInteractPrompt("Changing grades... 0%");
        soundFX?.PlayInteractSound();
        soundFX?.StartGradesChangeLoop();

        holdRoutine = StartCoroutine(HoldToChangeGradesRoutine(g));
    }

    private IEnumerator HoldToChangeGradesRoutine(GradesRackInteractable g)
    {
        if (g == null || !g.gameObject.activeInHierarchy)
        {
            CancelHold();
            yield break;
        }

        float duration = Mathf.Max(0.1f, g.holdSeconds);
        float t = 0f;

        while (t < duration)
        {
            if (currentInteractable == null || g == null || !g.gameObject.activeInHierarchy)
            {
                CancelHold();
                yield break;
            }

            if (Keyboard.current != null && !Keyboard.current.eKey.isPressed)
            {
                CancelHold();
                yield break;
            }

            t += Time.deltaTime;
            float pct = Mathf.Clamp01(t / duration);

            TrySetInteractPrompt($"Changing grades... {Mathf.RoundToInt(pct * 100f)}% (hold E)");
            yield return null;
        }

        TrySetInteractPrompt("Grades changed!");

        // Server flips ObjectiveState.GradesChanged (syncs to everyone)
        g.ChangeGradesServerRpc();

        ClearInteractable();
        CancelHold();
    }
}