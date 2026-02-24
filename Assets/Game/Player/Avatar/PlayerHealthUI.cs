using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PlayerHealthUI : NetworkBehaviour
{
    private PlayerHealth playerHealth;
    private Canvas localCanvas;

    private GameObject healthPanel;
    private Image fillImage;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("HealthUI: No PlayerHealth found on player.");
            return;
        }

        CreateLocalUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateUIVisibility(SceneManager.GetActiveScene().name);
    }

    void CreateLocalUI()
    {
        GameObject canvasObj = new GameObject($"PlayerHealth_{OwnerClientId}");
        localCanvas = canvasObj.AddComponent<Canvas>();
        localCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        localCanvas.sortingOrder = 101; // above hotbar if needed

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        CreateHealthBar();
    }

    void CreateHealthBar()
    {
        // Panel (background)
        healthPanel = new GameObject("HealthPanel");
        healthPanel.transform.SetParent(localCanvas.transform, false);

        RectTransform panelRect = healthPanel.AddComponent<RectTransform>();

        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20, 20);
        panelRect.sizeDelta = new Vector2(200, 25);

        Image bg = healthPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.5f);

        GameObject fillObj = new GameObject("HealthFill");
        fillObj.transform.SetParent(healthPanel.transform, false);

        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);

        fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 1f, 0.2f, 0.9f);

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (fillImage == null || playerHealth == null) return;

        // 3-hit system => bar drops by 1/3 each hit automatically
        float percent = Mathf.Clamp01(playerHealth.currentHealth / playerHealth.maxHealth);
        fillImage.fillAmount = percent;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateUIVisibility(scene.name);
    }

    void UpdateUIVisibility(string sceneName)
    {
        bool showUI = (sceneName == "03_Game");
        if (healthPanel != null)
            healthPanel.SetActive(showUI);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (localCanvas != null)
            Destroy(localCanvas.gameObject);
    }
}