using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerStaminaUI : NetworkBehaviour
{
    [Header("Scene names")]
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("Layout (bottom-left, above health)")]
    [SerializeField] private Vector2 offset = new Vector2(20, 54);
    [SerializeField] private Vector2 panelSize = new Vector2(240, 16);
    [SerializeField] private int sortingOrder = 510;

    [Header("Blocks")]
    [SerializeField] private int blockCount = 8;   // 8 blocks (0.5s each if max stamina is 4s)
    [SerializeField] private float gap = 6f;

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color blockOnColor = Color.white;
    [SerializeField] private Color blockOffColor = new Color(1f, 1f, 1f, 0.12f);

    private NetworkPlayer player;
    private PlayerHealth health;

    private Canvas canvas;
    private GameObject panel;

    private Image[] blocks;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        player = GetComponentInParent<NetworkPlayer>();
        if (player == null)
        {
            Debug.LogError("PlayerStaminaUI: NetworkPlayer not found on this player.");
            enabled = false;
            return;
        }

        health = GetComponentInParent<PlayerHealth>();
        if (health != null)
            health.IsDead.OnValueChanged += OnDeadChanged;

        SceneManager.sceneLoaded += OnSceneLoaded;

        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    public override void OnDestroy()
    {
        if (IsOwner)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (health != null)
                health.IsDead.OnValueChanged -= OnDeadChanged;

            if (canvas != null)
                Destroy(canvas.gameObject);
        }

        base.OnDestroy();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool inGame = sceneName == gameSceneName;
        bool isDead = (health != null && health.IsDead.Value);

        if (inGame && canvas == null)
            BuildUI();

        if (canvas == null) return;

        canvas.gameObject.SetActive(inGame);
        panel.SetActive(inGame && !isDead);
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (blocks == null || blocks.Length == 0) return;

        float stamina01 = Mathf.Clamp01(player.Stamina01);

        // 8 blocks => each block is 12.5% of bar
        // With max stamina 4 seconds: 4s * 12.5% = 0.5s per block
        int visibleBlocks = Mathf.Clamp(Mathf.CeilToInt(stamina01 * blockCount), 0, blockCount);

        for (int i = 0; i < blockCount; i++)
        {
            bool on = i < visibleBlocks;
            blocks[i].color = on ? blockOnColor : blockOffColor;

            // If you prefer them to fully disappear:
            // blocks[i].enabled = on;
        }
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject($"StaminaBlocksCanvas_{OwnerClientId}");
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

        panel = new GameObject("StaminaPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0);
        panelRT.anchorMax = new Vector2(0, 0);
        panelRT.pivot = new Vector2(0, 0);
        panelRT.anchoredPosition = offset;
        panelRT.sizeDelta = panelSize;

        var bg = panel.AddComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        blocks = new Image[blockCount];

        float totalGap = gap * (blockCount - 1);
        float blockWidth = (panelSize.x - totalGap) / blockCount;
        float blockHeight = panelSize.y;

        for (int i = 0; i < blockCount; i++)
        {
            var blockGO = new GameObject($"Block_{i}", typeof(RectTransform));
            blockGO.transform.SetParent(panel.transform, false);

            var rt = blockGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);

            float x = i * (blockWidth + gap);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(blockWidth, blockHeight);

            var img = blockGO.AddComponent<Image>();
            img.color = blockOnColor;
            img.raycastTarget = false;

            blocks[i] = img;
        }
    }
}