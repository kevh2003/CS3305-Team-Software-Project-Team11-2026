using UnityEngine;
using UnityEngine.UI;

public class WinScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private int sortingOrder = 2000;
    [SerializeField] private int fontSize = 64;

    private ObjectiveState state;

    private Canvas canvas;
    private GameObject root;
    private Text titleText;

    private void Start()
    {
        state = ObjectiveState.Instance != null ? ObjectiveState.Instance : FindFirstObjectByType<ObjectiveState>();
        BuildUI();

        if (state != null)
        {
            state.GradesChanged.OnValueChanged += OnGradesChanged;
            OnGradesChanged(false, state.GradesChanged.Value);
        }
        else
        {
            Hide();
        }
    }

    private void OnDestroy()
    {
        if (state != null)
            state.GradesChanged.OnValueChanged -= OnGradesChanged;
    }

    private void OnGradesChanged(bool oldValue, bool newValue)
    {
        if (newValue) Show();
        else Hide();
    }

    private void BuildUI()
    {
        root = new GameObject("WinScreenRoot");
        DontDestroyOnLoad(root);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // Dark background
        var bgGO = new GameObject("BG", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        var bgRT = (RectTransform)bgGO.transform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Text
        var textGO = new GameObject("WinText", typeof(RectTransform));
        textGO.transform.SetParent(bgGO.transform, false);

        titleText = textGO.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = fontSize;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "YOU WIN!\n\nGrades changed.";

        var rt = (RectTransform)textGO.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1200, 400);

        Hide();
    }

    private void Show()
    {
        if (root != null) root.SetActive(true);
    }

    private void Hide()
    {
        if (root != null) root.SetActive(false);
    }
}