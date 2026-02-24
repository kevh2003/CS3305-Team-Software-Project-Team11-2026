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

        BuildUI();

        // Scene show/hide
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateVisibility(SceneManager.GetActiveScene().name);

        // Update now + whenever networked health changes
        UpdateBlocks(health.CurrentHealth.Value);
        health.CurrentHealth.OnValueChanged += OnHealthChanged;
    }

    private void OnDestroy()
    {
        if (!IsOwner) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (health != null)
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;

        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        UpdateBlocks(newValue);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);
        if (health != null) UpdateBlocks(health.CurrentHealth.Value);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool show = sceneName == gameSceneName;
        if (panel != null) panel.SetActive(show);
    }

    private void UpdateBlocks(int hp)
    {
        // Right-to-left loss: block3 disappears first, then block2, then block1.
        blocks[0].color = (hp >= 1) ? green : darkGrey;
        blocks[1].color = (hp >= 2) ? green : darkGrey;
        blocks[2].color = (hp >= 3) ? green : darkGrey;
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
    }
}