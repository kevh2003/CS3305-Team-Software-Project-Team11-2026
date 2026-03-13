using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Displays the network match timer for the local owner.
public class TimerUI : NetworkBehaviour
{
    [Header("Scene names")]
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("UI (Top Right)")]
    [SerializeField] private Vector2 offset = new Vector2(20, 20);
    [SerializeField] private int fontSize = 36;
    [SerializeField] private int sortingOrder = 600;

    private Canvas canvas;
    private Text timerText;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        BuildUI();

        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    public override void OnDestroy()
    {
        if (IsOwner)
            SceneManager.sceneLoaded -= OnSceneLoaded;

        if (canvas != null)
            Destroy(canvas.gameObject);

        base.OnDestroy();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool show = sceneName == gameSceneName;
        if (canvas != null) canvas.gameObject.SetActive(show);
    }

    private void Update()
    {
        var timer = TimerNetwork.Instance;
        if (timer == null || timerText == null) return;

        int secs = timer.RemainingSeconds.Value;
        if (secs < 0) secs = 0;

        int m = secs / 60;
        int s = secs % 60;
        timerText.text = $"{m:00}:{s:00}";
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("MatchTimerCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        var txtGO = new GameObject("TimerText", typeof(RectTransform));
        txtGO.transform.SetParent(canvasGO.transform, false);

        timerText = txtGO.AddComponent<Text>();
        timerText.alignment = TextAnchor.UpperRight;
        timerText.fontSize = fontSize;
        timerText.fontStyle = FontStyle.Bold;
        timerText.color = Color.white;
        timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText.text = "10:00";

        var rt = (RectTransform)txtGO.transform;
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-offset.x, -offset.y);
        rt.sizeDelta = new Vector2(260, 60);
    }
}