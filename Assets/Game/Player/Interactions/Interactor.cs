using UnityEngine;

public class Interactor : MonoBehaviour
{
    public Transform InteractSource;  
    public float InteractRange = 3f;
    public KeyCode InteractKey = KeyCode.E;

    void Update()
    {
        if (Input.GetKeyDown(InteractKey))
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        Ray ray = new Ray(InteractSource.position, InteractSource.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractRange))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();

            if (interactable != null && interactable.CanInteract())
            {
                interactable.Interact(this);
            }
        }
    }
}

