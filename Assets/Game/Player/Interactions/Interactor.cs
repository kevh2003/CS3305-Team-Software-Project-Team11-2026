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

    private void Awake()
    {
        Player = GetComponent<NetworkPlayer>();
        if (Player == null)
        {
            Debug.LogError("Interactor: NetworkPlayer component not found");
        }
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
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                InteractSource = cam.transform;
            }
            else
            {
                Debug.LogError("Interactor: No camera found");
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();
    }

    private void CheckForInteractable()
    {
        if (InteractSource == null) return;

        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract())
            {
                if (currentInteractable != interactable)
                {
                    currentInteractable = interactable;
                    crosshair?.ShowInteractPrompt();
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

    private void ClearInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable = null;
            crosshair?.HideInteractPrompt();
        }
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) return;

        if (value.isPressed && currentInteractable != null)
        {
            if (currentInteractable.CanInteract())
            {
                bool success = currentInteractable.Interact(this);
                if (success)
                {
                    ClearInteractable();
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (InteractSource != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(InteractSource.position, InteractSource.forward * InteractRange);
        }
    }
}
