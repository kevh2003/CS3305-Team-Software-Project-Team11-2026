using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Interactor that uses Unity's PlayerInput component (matches your team's input setup).
/// Add this script to your player prefab and create an "Interact" action in your Input Actions asset.
/// The OnInteract method will be called automatically when the player presses the interact button.
/// </summary>
public class Interactor : NetworkBehaviour
{
    [Header("Raycast Settings")]
    public Transform InteractSource;
    public float InteractRange = 3f;

    // Reference to NetworkPlayer so inventory system knows which player is interacting
    public NetworkPlayer Player { get; private set; }

    private bool _interactPressed = false;

    void Awake()
    {
        // Get the NetworkPlayer component for inventory system compatibility
        Player = GetComponent<NetworkPlayer>();
        
        if (Player == null)
        {
            Debug.LogError("Interactor: NetworkPlayer component not found on this GameObject!");
        }
        
        Debug.Log("Interactor: Initialized in Awake");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Auto-find camera if not assigned
        if (InteractSource == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                InteractSource = cam.transform;
                Debug.Log("Interactor: Auto-assigned camera");
            }
            else
            {
                Debug.LogError("Interactor: No camera found!");
            }
        }

        Debug.Log("Interactor fully initialized");
    }

    // This method is called by Unity's PlayerInput component when the "Interact" action is triggered
    // Make sure you have an "Interact" action in your Input Actions asset!
    public void OnInteract(InputValue value)
    {
        if (!IsOwner) return;
        
        _interactPressed = value.isPressed;
        
        if (_interactPressed)
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (InteractSource == null)
        {
            Debug.LogError("Interactor: InteractSource is null!");
            return;
        }

        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                bool success = interactable.Interact(this);
                if (success)
                {
                    Debug.Log($"Interacted with: {hit.collider.name}");
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

