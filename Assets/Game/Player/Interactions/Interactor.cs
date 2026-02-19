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

    void Awake()
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

    void Update()
    {
        if (!IsOwner) return;

        CheckForInteractable();
    }

    void CheckForInteractable()
    {
        if (InteractSource == null) return;

        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                if (currentInteractable != interactable)
                {
                    currentInteractable = interactable;

                    if (crosshair != null)
                    {
                        crosshair.ShowInteractPrompt();
                    }
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
        if (currentInteractable != null)
        {
            currentInteractable = null;

            if (crosshair != null)
            {
                crosshair.HideInteractPrompt();
            }
        }
    }

    public void OnInteract(InputValue value)
    {
        if (!IsOwner) return;

        Debug.Log($"[Interactor] OnInteract fired. isPressed={value.isPressed}, currentInteractable={(currentInteractable != null)}");

        if (value.isPressed && currentInteractable != null)
        {
            Debug.Log($"[Interactor] Trying Interact() on {currentInteractable}");
            if (currentInteractable.CanInteract())
            {
                bool success = currentInteractable.Interact(this);
                Debug.Log($"[Interactor] Interact() returned {success}");

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