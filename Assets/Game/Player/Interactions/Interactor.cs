using UnityEngine;

public class Interactor : MonoBehaviour
{
    public Transform InteractSource;  
    public float InteractRange = 3f;
    public KeyCode InteractKey = KeyCode.E;

    void Start()
    {
        // Auto-find camera if not assigned
        if (InteractSource == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                InteractSource = cam.transform;
                Debug.Log("Auto-assigned camera as InteractSource");
            }
            else
            {
                Debug.LogError("No InteractSource assigned and no camera found!");
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(InteractKey))
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (InteractSource == null)
        {
            Debug.LogError("InteractSource is null!");
            return;
        }
        
        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * InteractRange, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, InteractRange))
        {
            Debug.Log($"Hit: {hit.collider.name}");
            
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null)
            {
                Debug.Log("Found IInteractable!");
                if (interactable.CanInteract())
                {
                    Debug.Log("Can interact, calling Interact()");
                    interactable.Interact(this);
                }
                else
                {
                    Debug.Log("Cannot interact (CanInteract returned false)");
                }
            }
            else
            {
                Debug.Log($"No IInteractable on {hit.collider.name}");
            }
        }
        else
        {
            Debug.Log("Raycast hit nothing");
        }
    }
}