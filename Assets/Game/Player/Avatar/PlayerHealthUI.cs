using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHealthUI : NetworkBehaviour
{
    [Header("Scene names")]
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("UI layout")]
    [SerializeField] private Vector2 offset = new Vector2(20, 20);
    [SerializeField] private Vector2 panelSize = new Vector2(240, 28);
    [SerializeField] private float blockGap = 6f;
    [SerializeField] private int sortingOrder = 500;

    [Header("Colors")]
    [SerializeField] private Color green = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color darkGrey = new Color(0.2f, 0.2f, 0.2f, 1f);

    private PlayerHealth health;

    private Canvas canvas;
    private GameObject panel;
    private Image[] blocks = new Image[3];

    [Header("Death message")]
    [SerializeField] private string caughtMessage = "You got Caught";
    [SerializeField] private int caughtFontSize = 72;

    private GameObject caughtRoot;
    private Text caughtLabel;
    private static PlayerHealthUI s_local;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        health = GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogError("PlayerHealthUI: PlayerHealth not found.");
            enabled = false;
            return;
        }

        s_local = this;

        SceneManager.sceneLoaded += OnSceneLoaded;

        health.CurrentHealth.OnValueChanged += OnHealthChanged;
        health.IsDead.OnValueChanged += OnDeadChanged;

        // Try to build/show if already in game scene
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (!IsOwner) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (health != null)
        {
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;
            health.IsDead.OnValueChanged -= OnDeadChanged;
        }

        if (canvas != null)
            Destroy(canvas.gameObject);

        if (s_local == this) s_local = null;
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        UpdateBlocks(newValue);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            ShowCaughtMessage(true);
        else
            ShowCaughtMessage(false);
    }

    private void ShowCaughtMessage(bool show)
    {
        if (caughtRoot == null) return;
        caughtRoot.SetActive(show);
        if (show && caughtLabel != null)
            caughtLabel.text = caughtMessage;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);

        // Only update blocks if the UI exists
        if (canvas != null && health != null)
            UpdateBlocks(health.CurrentHealth.Value);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool inGame = sceneName == gameSceneName;
        bool isDead = (health != null && health.IsDead.Value);

        // Build once when entering game scene
        if (inGame && canvas == null)
        {
            BuildUI();
            UpdateBlocks(health.CurrentHealth.Value);
        }

        if (canvas == null) return;

        // Health bar should be hidden when dead
        if (panel != null) panel.SetActive(inGame && !isDead);

        // "You got caught" only when dead, only in game
        if (caughtRoot != null)
            caughtRoot.SetActive(inGame && isDead);
    }

    private void UpdateBlocks(int hp)
    {
        if (blocks == null || blocks.Length < 3 || blocks[0] == null) return;

        blocks[0].color = (hp >= 1) ? green : darkGrey;
        blocks[1].color = (hp >= 2) ? green : darkGrey;
        blocks[2].color = (hp >= 3) ? green : darkGrey;
    }

    public static void ShowGameOver()
    {
        if (s_local == null) return;

        // Ensure message shows even if "caught" message was already visible
        if (s_local.caughtLabel != null)
            s_local.caughtLabel.text = "GAME OVER";

        if (s_local.caughtRoot != null)
            s_local.caughtRoot.SetActive(true);
    }

    private void BuildUI()
    {
        // Canvas that survives scene loads
        var canvasGO = new GameObject($"PlayerHealthCanvas_{OwnerClientId}");
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

        // Panel
        panel = new GameObject("HealthPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0);
        panelRT.anchorMax = new Vector2(0, 0);
        panelRT.pivot = new Vector2(0, 0);
        panelRT.anchoredPosition = offset;
        panelRT.sizeDelta = panelSize;

        float totalGap = blockGap * 2f;
        float blockWidth = (panelSize.x - totalGap) / 3f;
        float blockHeight = panelSize.y;

        for (int i = 0; i < 3; i++)
        {
            var blockGO = new GameObject($"Block{i + 1}", typeof(RectTransform));
            blockGO.transform.SetParent(panel.transform, false);

            var rt = blockGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(blockWidth, blockHeight);
            rt.anchoredPosition = new Vector2(i * (blockWidth + blockGap), 0);

            var img = blockGO.AddComponent<Image>();
            img.color = green; // start full
            img.raycastTarget = false;

            blocks[i] = img;
        }

        // "You got Caught" overlay (center screen)
        caughtRoot = new GameObject("CaughtMessageRoot", typeof(RectTransform));
        caughtRoot.transform.SetParent(canvasGO.transform, false);

        var rootRT = caughtRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = Vector2.zero;
        rootRT.sizeDelta = new Vector2(1000, 220);

        // Text
        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(caughtRoot.transform, false);
        caughtLabel = txtGO.AddComponent<Text>();
        caughtLabel.alignment = TextAnchor.MiddleCenter;
        caughtLabel.fontSize = caughtFontSize;
        caughtLabel.fontStyle = FontStyle.Bold;
        caughtLabel.color = Color.white;
        caughtLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caughtLabel.text = caughtMessage;

        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(30, 30);
        txtRT.offsetMax = new Vector2(-30, -30);

        caughtRoot.SetActive(false);
    }
}