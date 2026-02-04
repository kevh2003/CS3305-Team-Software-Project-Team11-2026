using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Interactor that uses the PlayerInput component and your existing Input Actions.
/// Add an "Interact" action to your Input Actions asset, then this will use it automatically.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class Interactor : MonoBehaviour
{
    [Header("Raycast Settings")]
    public Transform InteractSource;  
    public float InteractRange = 3f;

    private PlayerInput _playerInput;
    private InputAction _interactAction;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        // Get the "Interact" action from the PlayerInput component
        // Make sure you have an "Interact" action in your Input Actions asset!
        if (_playerInput != null)
        {
            _interactAction = _playerInput.actions["Interact"];
            
            if (_interactAction == null)
            {
                Debug.LogError("Interactor: No 'Interact' action found in Input Actions! Please add one.");
            }
        }
    }

    void Update()
    {
        // Check if the interact action was pressed this frame
        if (_interactAction != null && _interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        if (InteractSource == null)
        {
            Debug.LogWarning("Interactor: InteractSource is null!");
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
                    Debug.Log($"Interacted with: {hit.collider.gameObject.name}");
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

