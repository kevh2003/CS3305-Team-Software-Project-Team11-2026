using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerInventory))]
// Builds the owning player's hotbar UI and keeps it synced with inventory state.
public class InventoryUI : NetworkBehaviour
{
    [Header("Scene gating")]
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("Hotbar Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(150, 70);
    [SerializeField] private Vector2 panelOffsetFromBottomRight = new Vector2(-20, 20);
    [SerializeField] private Vector2 slotSize = new Vector2(60, 60);
    [SerializeField] private float slotSpacing = 70f;

    [Header("Hand/Drop anchors (local offsets)")]
    [SerializeField] private Vector3 handLocalPos = new Vector3(0.25f, -0.25f, 0.5f);
    [SerializeField] private Vector3 handLocalEuler = Vector3.zero;

    [SerializeField] private Vector3 dropLocalPos = new Vector3(0f, -0.2f, 1.5f);
    [SerializeField] private Vector3 dropLocalEuler = Vector3.zero;

    private PlayerInventory inventory;
    private PlayerHealth health;

    private Canvas localCanvas;
    private GameObject canvasObj;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the owning client builds UI / anchors
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        inventory = GetComponent<PlayerInventory>();
        health = GetComponent<PlayerHealth>();

        if (health != null)
            health.IsDead.OnValueChanged += OnDeadChanged;

        CreateLocalUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateUIVisibility(SceneManager.GetActiveScene().name);
    }

    private void CreateLocalUI()
    {
        // Avoid duplicate canvases if something respawns
        string canvasName = $"PlayerHotbar_{OwnerClientId}";
        var existing = GameObject.Find(canvasName);
        if (existing != null)
        {
            canvasObj = existing;
            localCanvas = existing.GetComponent<Canvas>();
        }
        else
        {
            canvasObj = new GameObject(canvasName);
            localCanvas = canvasObj.AddComponent<Canvas>();
            localCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            localCanvas.sortingOrder = 100;

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);
        }

        CreateHotbarPanel();
        StartCoroutine(SetupHandAndDropPositions());

        // Ensure slot highlight / hand display starts sane
        inventory.SelectSlot(0);
        SetSelectedSlot(0);
    }

    private void CreateHotbarPanel()
    {
        // If we already have one, don't recreate
        if (inventory.hotbarPanel != null)
            return;

        GameObject hotbarPanel = new GameObject("HotbarPanel");
        hotbarPanel.transform.SetParent(localCanvas.transform, false);

        RectTransform hotbarRect = hotbarPanel.AddComponent<RectTransform>();

        // Anchor to bottom-right
        hotbarRect.anchorMin = new Vector2(1f, 0f);
        hotbarRect.anchorMax = new Vector2(1f, 0f);
        hotbarRect.pivot = new Vector2(1f, 0f);

        // Offset inward from the corner
        hotbarRect.anchoredPosition = panelOffsetFromBottomRight;
        hotbarRect.sizeDelta = panelSize;

        Image hotbarImage = hotbarPanel.AddComponent<Image>();
        hotbarImage.color = new Color(0, 0, 0, 0.5f);

        inventory.hotbarPanel = hotbarPanel;

        int slots = Mathf.Max(1, inventory.hotbarSlots);
        inventory.hotbarSlotImages = new Image[slots];

        for (int i = 0; i < slots; i++)
        {
            GameObject slot = new GameObject($"HotbarSlot_{i}");
            slot.transform.SetParent(hotbarPanel.transform, false);

            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0f, 0f);
            slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0f, 0f);

            slotRect.sizeDelta = slotSize;
            slotRect.anchoredPosition = new Vector2(10 + (i * slotSpacing), 5);

            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(1, 1, 1, 0.3f);

            inventory.hotbarSlotImages[i] = slotImage;
        }
    }

    private IEnumerator SetupHandAndDropPositions()
    {
        // IMPORTANT:
        // Do NOT use Camera.main (unreliable with multiple players / ParrelSync / builds).
        // Always use THIS player's camera.
        Camera cam = null;

        float timeout = 2f;
        while (timeout > 0f && (cam == null || !cam.gameObject.activeInHierarchy))
        {
            cam = GetComponentInChildren<Camera>(true);
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (cam == null)
        {
            Debug.LogError("InventoryUI: Could not find this player's Camera. Hand/Drop anchors won't be created.");
            yield break;
        }

        // Hand anchor under this player's camera
        Transform hp = cam.transform.Find("HandPosition");
        if (hp == null)
        {
            var go = new GameObject("HandPosition");
            hp = go.transform;
            hp.SetParent(cam.transform, false);
        }
        hp.localPosition = handLocalPos;
        hp.localRotation = Quaternion.Euler(handLocalEuler);

        // Drop anchor under this player's camera too (keeps drop direction consistent)
        Transform dp = cam.transform.Find("DropPosition");
        if (dp == null)
        {
            var go = new GameObject("DropPosition");
            dp = go.transform;
            dp.SetParent(cam.transform, false);
        }
        dp.localPosition = dropLocalPos;
        dp.localRotation = Quaternion.Euler(dropLocalEuler);

        // Push anchors into PlayerInventory
        inventory.SetAnchors(hp, dp);
    }

    public void SetSelectedSlot(int index)
    {
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateUIVisibility(scene.name);
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        UpdateUIVisibility(SceneManager.GetActiveScene().name);
    }

    private void UpdateUIVisibility(string sceneName)
    {
        bool inGame = (sceneName == gameSceneName);
        bool isDead = (health != null && health.IsDead.Value);

        bool showUI = inGame && !isDead;

        if (inventory != null && inventory.hotbarPanel != null)
            inventory.hotbarPanel.SetActive(showUI);

        if (inventory != null && inventory.handPosition != null)
        {
            foreach (Transform child in inventory.handPosition)
                child.gameObject.SetActive(showUI);
        }
    }

    private void OnDestroy()
    {
        if (!IsOwner) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (health != null)
            health.IsDead.OnValueChanged -= OnDeadChanged;

        if (localCanvas != null)
            Destroy(localCanvas.gameObject);
    }
}