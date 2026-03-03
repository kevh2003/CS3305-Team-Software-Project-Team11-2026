using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Local-only UI helper. Creates an overlay canvas and shows a single bottom-center prompt.
/// Supports two reasons: Inventory (Press Q to drop) and CCTV (Press Q to exit).
/// CCTV takes priority when active.
/// Hides automatically outside the game scene.
/// </summary>
public class DropPromptUI : MonoBehaviour
{
    private static DropPromptUI _instance;
    private static bool _isQuitting;

    // Change this if your gameplay scene name differs
    private const string GAME_SCENE_NAME = "03_Game";

    public static DropPromptUI Instance
    {
        get
        {
            if (_isQuitting) return null;

            if (_instance == null)
            {
                var go = new GameObject("DropPromptUI");
                _instance = go.AddComponent<DropPromptUI>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 5000;

    [Header("Bottom Center Layout")]
    [SerializeField] private Vector2 bottomCenterOffset = new Vector2(0f, 70f);
    [SerializeField] private int fontSize = 32;

    private Canvas _canvas;
    private Text _text;

    // Two “channels” that can request the prompt
    private bool _inventoryVisible;
    private bool _cameraVisible;

    private string _inventoryMessage = "Press Q to drop";
    private string _cameraMessage = "Press Q to exit";

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // whenever scenes change, enforce correct visibility
        Refresh();
    }

    private bool ShouldShowInThisScene()
    {
        return SceneManager.GetActiveScene().name == GAME_SCENE_NAME;
    }

    private void EnsureBuilt()
    {
        if (_canvas != null) return;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Unity 6+ change: Arial.ttf is not valid built-in font
        // Use LegacyRuntime.ttf instead
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var go = new GameObject("BottomCenterPrompt", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        _text = go.AddComponent<Text>();
        _text.font = font;
        _text.fontSize = fontSize;
        _text.color = Color.white;
        _text.alignment = TextAnchor.LowerCenter;
        _text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        var rt = _text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = bottomCenterOffset;
        rt.sizeDelta = new Vector2(900f, 40f);

        _text.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        // Hide in lobby/menus no matter what anyone requests
        if (!ShouldShowInThisScene())
        {
            if (_text != null) _text.gameObject.SetActive(false);
            return;
        }

        EnsureBuilt();
        if (_text == null) return; // safety

        // Priority: CCTV > Inventory
        if (_cameraVisible)
        {
            _text.text = _cameraMessage;
            _text.gameObject.SetActive(true);
            return;
        }

        if (_inventoryVisible)
        {
            _text.text = _inventoryMessage;
            _text.gameObject.SetActive(true);
            return;
        }

        _text.text = "";
        _text.gameObject.SetActive(false);
    }

    public void SetInventoryVisible(bool visible, string message = "Press Q to drop")
    {
        _inventoryVisible = visible;
        if (!string.IsNullOrWhiteSpace(message))
            _inventoryMessage = message;
        Refresh();
    }

    public void SetCameraVisible(bool visible, string message = "Press Q to exit")
    {
        _cameraVisible = visible;
        if (!string.IsNullOrWhiteSpace(message))
            _cameraMessage = message;
        Refresh();
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }
}