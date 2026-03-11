using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class GetPlayerData : MonoBehaviour
{

    [Header("Interactable")]
    [SerializeField] float _interactDistance = 10f;
    TextMeshProUGUI _interactText;
    IWifiInteractable interactable;

    [Header("Player")]
    private Camera _playerCamera;

    private void Update()
    {
        // Interaction logic
        UpdateCurrentInteractable();
        UpdateInteractionText();
        CheckForInteractionInput();

    }

    private void UpdateCurrentInteractable()
    {
        if (_playerCamera == null) return;
        Ray ray = _playerCamera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
        Physics.Raycast(ray, out RaycastHit hit, _interactDistance);
        interactable = hit.collider?.GetComponent<IWifiInteractable>();
    }

    private void UpdateInteractionText()
    {
        if (_interactText == null) return;

        if (interactable != null)
        {
            _interactText.text = interactable.InteractText;
        }
        else
        {
            _interactText.text = "";
        }
    }

    private void CheckForInteractionInput()
    {
        // checking for interaction input - passes in a refrence to the player
        if (Keyboard.current.eKey.wasPressedThisFrame && interactable != null)
        {
            interactable.Interact(_playerCamera.GetComponentInParent<CharacterController>());
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        // function that gets a reference to the player 
        if (other.CompareTag("Player"))
        {
            _playerCamera = other.gameObject.GetComponentInChildren<Camera>();

            // Getting the textbox from the player prefab
            TextMeshProUGUI[] texts = other.gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {

                if (text.name == "Interact") _interactText = text;
            }
        }
    }
}
