using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class Interactor : NetworkBehaviour
{
    [Header("Raycast Settings")]
    public Transform InteractSource;
    public float InteractRange = 3f;

    public NetworkPlayer Player { get; private set; }
    private Crosshair crosshair;
    private IInteractable currentInteractable;

    [Header("References")]
    private PlayerSoundFX soundFX;

    [Header("Hold to interact")]
    private Coroutine holdRoutine;
    private PCInteractable holdingPc;
    private GradeRackInteractable holdingRack;

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
    }

    void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();

        // If currently holding a PC or ServerRack interaction, cancel the moment E is released
        if (holdingPc != null || holdingRack != null)
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

        if (Physics.Raycast(ray, out RaycastHit hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                if (currentInteractable != interactable)
                {
                    // If swapped targets mid-hold, cancel the hold instantly
                    if (holdingPc != null)
                        CancelHold();

                    currentInteractable = interactable;

                    if (crosshair != null)
                        crosshair.ShowInteractPrompt();
                }
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

    void ClearInteractable()
    {
        if (holdingPc != null)
            CancelHold();

        if (currentInteractable != null)
        {
            currentInteractable = null;

            if (crosshair != null)
                crosshair.HideInteractPrompt();
        }
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

        // press:
        soundFX?.PlayInteractSound();

        if (currentInteractable == null) return;
        if (!currentInteractable.CanInteract()) return;

        // If this is a PC assignment, start holding logic instead of instant interact.
        var pc = (currentInteractable as Component)?.GetComponentInParent<PCInteractable>();
        if (pc != null)
        {
            StartHold(pc);
            return;
        }

        var rack = (currentInteractable as Component)?.GetComponentInParent<GradeRackInteractable>();
        if (rack != null)
        {
            StartHoldRack(rack);
            return;
        }

        // Normal interactables (ducks, pickups, doors, etc.)
        bool success = currentInteractable.Interact(this);
        if (success)
            ClearInteractable();
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

        holdRoutine = StartCoroutine(HoldToSubmitRoutine(pc));
    }

    private void CancelHold()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        holdingPc = null;
        holdingRack = null;

        if (crosshair != null)
        {
            // If still looking at something, show the default prompt again
            if (currentInteractable != null)
            {
                // If it's a PC, show a "hold" message rather than plain "Press E"
                var pc = (currentInteractable as Component)?.GetComponentInParent<PCInteractable>();
                if (pc != null)
                    crosshair.SetPromptText("Hold E to submit");
                else
                    crosshair.ShowInteractPrompt();
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
        }
    }

    private void StartHoldRack(GradeRackInteractable rack)
    {
        CancelHold();

        if (!rack.CanInteract())
        {
            TrySetInteractPrompt("Can't do that yet.");
            return;
        }

        holdingRack = rack;
        TrySetInteractPrompt("Changing grades... 0% (hold E)");
        holdRoutine = StartCoroutine(HoldToChangeGradesRoutine(rack));
    }

    private IEnumerator HoldToChangeGradesRoutine(GradeRackInteractable rack)
    {
        if (rack == null || !rack.gameObject.activeInHierarchy)
        {
            CancelHold();
            yield break;
        }

        float duration = Mathf.Max(0.1f, rack.holdSeconds);
        float t = 0f;

        while (t < duration)
        {
            if (currentInteractable == null || rack == null || !rack.gameObject.activeInHierarchy)
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

        TrySetInteractPrompt("Done!");
        rack.ChangeGradesServerRpc();

        ClearInteractable();
        CancelHold();
    }
}