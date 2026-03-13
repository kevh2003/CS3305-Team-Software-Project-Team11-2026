using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Local-only UI helper. Creates an overlay canvas and shows a single bottom-center prompt.
/// Supports three reasons: Inventory (Press Q to drop), CCTV (Press Q to exit), and WiFi (Press Q to exit).
/// CCTV takes priority, then WiFi, then inventory.
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

    public static DropPromptUI Existing => _instance;

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 5000;

    [Header("Bottom Center Layout")]
    [SerializeField] private Vector2 bottomCenterOffset = new Vector2(0f, 70f);
    [SerializeField] private int fontSize = 32;
    [Header("CCTV Crosshair")]
    [SerializeField] private float cctvCrosshairSize = 10f;
    [SerializeField] private Color cctvCrosshairColor = Color.white;

    private Canvas _canvas;
    private Text _text;
    private Image _cctvCrosshair;

    // Three channels that can request the prompt.
    private bool _inventoryVisible;
    private bool _cameraVisible;
    private bool _wifiVisible;

    private string _inventoryMessage = "Press Q to drop";
    private string _cameraMessage = "Press Q to exit";
    private string _wifiMessage = "Press Q to exit";

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
        rt.sizeDelta = new Vector2(900f, 150f);

        _text.gameObject.SetActive(false);

        var crosshairGo = new GameObject("CctvCrosshair", typeof(RectTransform));
        crosshairGo.transform.SetParent(transform, false);

        _cctvCrosshair = crosshairGo.AddComponent<Image>();
        _cctvCrosshair.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        _cctvCrosshair.color = cctvCrosshairColor;
        _cctvCrosshair.type = Image.Type.Simple;
        _cctvCrosshair.raycastTarget = false;

        var crosshairRect = _cctvCrosshair.rectTransform;
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = Vector2.zero;
        crosshairRect.sizeDelta = Vector2.one * Mathf.Max(2f, cctvCrosshairSize);

        _cctvCrosshair.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        // Hide in lobby/menus no matter what anyone requests
        if (!ShouldShowInThisScene())
        {
            _inventoryVisible = false;
            _cameraVisible = false;
            _wifiVisible = false;
            if (_text != null) _text.gameObject.SetActive(false);
            if (_cctvCrosshair != null) _cctvCrosshair.gameObject.SetActive(false);
            return;
        }

        EnsureBuilt();
        if (_text == null) return; // safety

        // Priority: CCTV > WiFi > Inventory
        if (_cameraVisible)
        {
            _text.text = _cameraMessage;
            _text.gameObject.SetActive(true);
            if (_cctvCrosshair != null) _cctvCrosshair.gameObject.SetActive(true);
            return;
        }

        if (_wifiVisible)
        {
            _text.text = _wifiMessage;
            _text.gameObject.SetActive(true);
            if (_cctvCrosshair != null) _cctvCrosshair.gameObject.SetActive(false);
            return;
        }

        if (_inventoryVisible)
        {
            _text.text = _inventoryMessage;
            _text.gameObject.SetActive(true);
            if (_cctvCrosshair != null) _cctvCrosshair.gameObject.SetActive(false);
            return;
        }

        _text.text = "";
        _text.gameObject.SetActive(false);
        if (_cctvCrosshair != null) _cctvCrosshair.gameObject.SetActive(false);
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

    public void SetWifiVisible(bool visible, string message = "Press Q to exit")
    {
        _wifiVisible = visible;
        if (!string.IsNullOrWhiteSpace(message))
            _wifiMessage = message;
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