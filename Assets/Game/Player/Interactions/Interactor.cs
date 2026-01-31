using UnityEngine;
using Unity.Netcode;

public class Interactor : NetworkBehaviour
{
    public Transform InteractSource;  
    public float InteractRange = 3f;
    
    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        Debug.Log("✅ Interactor: PlayerInputActions created in Awake");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Safety check
        if (inputActions == null)
        {
            Debug.LogWarning("⚠️ inputActions was null, creating new instance");
            inputActions = new PlayerInputActions();
        }

        inputActions.Enable();
        inputActions.Player.Interact.performed += ctx => TryInteract();
        
        // Auto-find camera if not assigned
        if (InteractSource == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                InteractSource = cam.transform;
                Debug.Log("✅ Interactor: Auto-assigned camera");
            }
            else
            {
                Debug.LogError("❌ Interactor: No camera found!");
            }
        }
        
        Debug.Log("✅ Interactor fully initialized");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (inputActions != null)
            inputActions.Disable();
    }

    void TryInteract()
    {
        if (InteractSource == null)
        {
            Debug.LogError("❌ Interactor: InteractSource is null!");
            return;
        }
        
        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                interactable.Interact(this);
                Debug.Log($"✅ Interacted with: {hit.collider.name}");
            }
        }
    }
}