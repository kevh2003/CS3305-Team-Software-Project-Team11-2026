using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reveals NPC dialogue text for the local owning player inside the trigger.
public class NPC : NetworkBehaviour
{
    [TextArea(2, 8)]
    [SerializeField] private string text;
    [SerializeField, Min(1f)] private float charactersPerSecond = 45f;
    [SerializeField] private string dialogueCanvasName = "Canvas";
    [SerializeField] private string dialogueTextName = "Text (TMP)";
    [SerializeField] private string dialogueBackgroundName = "Image";
    [SerializeField] private Vector2 dialoguePadding = new Vector2(40f, 20f);
    [SerializeField] private TextOverflowModes overflowMode = TextOverflowModes.Truncate;

    private int _messageIndex;
    private float _revealProgress;
    private bool _displayText;
    private Canvas _activeCanvas;
    private TextMeshProUGUI _textBox;

    private void Update()
    {
        if (!_displayText || _textBox == null || string.IsNullOrEmpty(text)) return;
        if (_messageIndex >= text.Length) return;

        _revealProgress += Time.deltaTime * Mathf.Max(1f, charactersPerSecond);
        int nextIndex = Mathf.Clamp(Mathf.FloorToInt(_revealProgress), 0, text.Length);
        if (nextIndex == _messageIndex) return;

        _messageIndex = nextIndex;
        _textBox.text = text[.._messageIndex];
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwningPlayer(other)) return;
        if (other.GetComponentInParent<CharacterController>() == null) return;
        if (!TryResolveDialogueUi(other, out Canvas canvas, out TextMeshProUGUI textBox)) return;

        _activeCanvas = canvas;
        _textBox = textBox;
        ConfigureTextBox(_textBox);

        _activeCanvas.gameObject.SetActive(true);
        _displayText = true;
        _messageIndex = 0;
        _revealProgress = 0f;
        _textBox.text = string.Empty;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwningPlayer(other)) return;
        if (other.GetComponentInParent<CharacterController>() == null) return;
        HideDialogue();
    }

    private void OnDisable()
    {
        HideDialogue();
    }

    private bool IsOwningPlayer(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        return netObj != null && netObj.IsOwner;
    }

    private bool TryResolveDialogueUi(Collider other, out Canvas canvas, out TextMeshProUGUI textBox)
    {
        canvas = null;
        textBox = null;

        Canvas[] canvases = other.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate != null && candidate.name == dialogueCanvasName)
            {
                canvas = candidate;
                break;
            }
        }

        if (canvas == null)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas candidate = canvases[i];
                if (candidate != null && candidate.GetComponentInChildren<TextMeshProUGUI>(true) != null)
                {
                    canvas = candidate;
                    break;
                }
            }
        }

        if (canvas == null)
        {
            Debug.LogWarning("[NPC] Could not find a dialogue canvas on the player.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dialogueTextName))
        {
            TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI candidate = texts[i];
                if (candidate != null && candidate.name == dialogueTextName)
                {
                    textBox = candidate;
                    break;
                }
            }
        }

        if (textBox == null)
            textBox = canvas.GetComponentInChildren<TextMeshProUGUI>(true);

        if (textBox == null)
        {
            Debug.LogWarning("[NPC] Dialogue canvas found, but no TMP text field was available.");
            return false;
        }

        return true;
    }

    private void ConfigureTextBox(TextMeshProUGUI textBox)
    {
        textBox.enableWordWrapping = true;
        textBox.overflowMode = overflowMode;
        textBox.text = string.Empty;
        FitTextToBackground(textBox);
    }

    private void FitTextToBackground(TextMeshProUGUI textBox)
    {
        RectTransform textRect = textBox.rectTransform;
        if (textRect == null) return;

        Transform parent = textRect.parent;
        if (parent == null) return;

        RectTransform backgroundRect = null;
        if (!string.IsNullOrWhiteSpace(dialogueBackgroundName))
            backgroundRect = parent.Find(dialogueBackgroundName) as RectTransform;

        if (backgroundRect == null)
        {
            Image[] images = parent.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image candidate = images[i];
                if (candidate == null || candidate.transform == textBox.transform) continue;
                if (candidate.transform.parent != parent) continue;

                backgroundRect = candidate.rectTransform;
                break;
            }
        }

        if (backgroundRect == null) return;

        Vector2 paddedSize = backgroundRect.sizeDelta - (dialoguePadding * 2f);
        paddedSize.x = Mathf.Max(0f, paddedSize.x);
        paddedSize.y = Mathf.Max(0f, paddedSize.y);

        textRect.anchorMin = backgroundRect.anchorMin;
        textRect.anchorMax = backgroundRect.anchorMax;
        textRect.pivot = backgroundRect.pivot;
        textRect.anchoredPosition = backgroundRect.anchoredPosition;
        textRect.sizeDelta = paddedSize;
    }

    private void HideDialogue()
    {
        _displayText = false;
        _messageIndex = 0;
        _revealProgress = 0f;

        if (_textBox != null)
            _textBox.text = string.Empty;

        if (_activeCanvas != null)
            _activeCanvas.gameObject.SetActive(false);

        _textBox = null;
        _activeCanvas = null;
    }
}