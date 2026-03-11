using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class GetPlayerData : MonoBehaviour
{
    [Header("Legacy Mode")]
    [SerializeField] private bool _useLegacyWifiPromptAndInput = false;

    [Header("Interactable")]
    [SerializeField] private float _interactDistance = 10f;
    [SerializeField] private Vector2 _promptAnchoredPosition = new Vector2(0f, -430f);
    [SerializeField] private Vector2 _promptSize = new Vector2(280f, 50f);
    private TextMeshProUGUI _interactText;
    private IWifiInteractable interactable;

    [Header("Player")]
    private Camera _playerCamera;
    private CharacterController _playerController;

    private void Awake()
    {
        // WiFi interaction now runs through Interactor + StartGame (center prompt path).
        if (!_useLegacyWifiPromptAndInput)
            enabled = false;
    }

    private void Update()
    {
        if (!_useLegacyWifiPromptAndInput)
            return;

        if (_playerCamera == null)
            return;

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
        interactable = hit.collider?.GetComponentInParent<IWifiInteractable>();
    }

    private void UpdateInteractionText()
    {
        if (_interactText == null) return;

        bool hasInteractable = interactable != null;
        _interactText.gameObject.SetActive(hasInteractable);

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
        if (Keyboard.current != null
            && Keyboard.current.eKey.wasPressedThisFrame
            && interactable != null
            && _playerController != null)
        {
            interactable.Interact(_playerController);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_useLegacyWifiPromptAndInput)
            return;

        // function that gets a reference to the player
        if (other.CompareTag("Player"))
        {
            GameObject playerObject = other.gameObject;
            _playerCamera = playerObject.GetComponentInChildren<Camera>(true);
            _playerController = playerObject.GetComponent<CharacterController>();

            // Getting the textbox from the player prefab
            TextMeshProUGUI[] texts = playerObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (text.name == "Interact")
                {
                    _interactText = text;
                    break;
                }
            }

            // Older/newer prefab variants may not include an "Interact" TMP object.
            // Create a dedicated WiFi prompt under an active game canvas in that case.
            if (_interactText == null)
                _interactText = CreateFallbackPrompt();

            if (_interactText != null)
                _interactText.gameObject.SetActive(false);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_useLegacyWifiPromptAndInput)
            return;

        if (!other.CompareTag("Player"))
            return;

        interactable = null;
        _playerCamera = null;
        _playerController = null;

        if (_interactText != null)
        {
            _interactText.text = string.Empty;
            _interactText.gameObject.SetActive(false);
        }
    }

    private TextMeshProUGUI CreateFallbackPrompt()
    {
        Canvas canvas = FindPromptCanvas();
        if (canvas == null)
            return null;

        Transform existing = canvas.transform.Find("WifiInteractPrompt");
        if (existing != null)
        {
            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
                return existingText;
        }

        GameObject prompt = new GameObject("WifiInteractPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        prompt.transform.SetParent(canvas.transform, false);

        RectTransform rect = prompt.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = _promptAnchoredPosition;
        rect.sizeDelta = _promptSize;

        TextMeshProUGUI text = prompt.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.gameObject.SetActive(false);

        return text;
    }

    private static Canvas FindPromptCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy)
                continue;

            if (canvas.name == "GameCanvas" || canvas.GetComponent<PersistentCanvas>() != null)
                return canvas;
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.activeInHierarchy)
                return canvas;
        }

        return null;
    }
}