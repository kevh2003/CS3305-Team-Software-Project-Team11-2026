using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class InventoryUI : NetworkBehaviour
{
    private PlayerInventory inventory;
    private Canvas localCanvas;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("InventoryUI: No PlayerInventory found");
            return;
        }

        CreateLocalUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateUIVisibility(SceneManager.GetActiveScene().name);
    }

    void CreateLocalUI()
    {
        GameObject canvasObj = new GameObject($"PlayerHotbar_{OwnerClientId}");
        localCanvas = canvasObj.AddComponent<Canvas>();
        localCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        localCanvas.sortingOrder = 100;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        CreateHotbarPanel();
        StartCoroutine(SetupHandAndDropPositions());
        inventory.SelectSlot(0);
    }

    void CreateHotbarPanel()
    {
        GameObject hotbarPanel = new GameObject("HotbarPanel");
        hotbarPanel.transform.SetParent(localCanvas.transform, false);

        RectTransform hotbarRect = hotbarPanel.AddComponent<RectTransform>();

        // Anchor to bottom-right
        hotbarRect.anchorMin = new Vector2(1f, 0f);
        hotbarRect.anchorMax = new Vector2(1f, 0f);
        hotbarRect.pivot = new Vector2(1f, 0f);

        // Offset inward from the corner
        hotbarRect.anchoredPosition = new Vector2(-20, 20);
        hotbarRect.sizeDelta = new Vector2(150, 70);

        Image hotbarImage = hotbarPanel.AddComponent<Image>();
        hotbarImage.color = new Color(0, 0, 0, 0.5f);

        inventory.hotbarPanel = hotbarPanel;

        inventory.hotbarSlotImages = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject slot = new GameObject($"HotbarSlot_{i}");
            slot.transform.SetParent(hotbarPanel.transform, false);

            RectTransform slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0f, 0f);
            slotRect.anchorMax = new Vector2(0f, 0f);
            slotRect.pivot = new Vector2(0f, 0f);

            slotRect.sizeDelta = new Vector2(60, 60);
            slotRect.anchoredPosition = new Vector2(10 + (i * 70), 5);

            Image slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(1, 1, 1, 0.3f);

            inventory.hotbarSlotImages[i] = slotImage;
        }
    }


    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateUIVisibility(scene.name);
    }

    void UpdateUIVisibility(string sceneName)
    {
        bool showUI = (sceneName == "03_Game");

        if (inventory.hotbarPanel != null)
        {
            inventory.hotbarPanel.SetActive(showUI);
        }

        if (inventory.handPosition != null)
        {
            foreach (Transform child in inventory.handPosition)
            {
                child.gameObject.SetActive(showUI);
            }
        }
    }

    IEnumerator SetupHandAndDropPositions()
    {
        int attempts = 0;
        while (attempts < 50)
        {
            Camera cam = GetComponentInChildren<Camera>(true);

            if (cam != null && cam.gameObject.activeInHierarchy)
            {
                SetupHandPosition(cam);
                break;
            }

            attempts++;
            yield return new WaitForSeconds(0.1f);
        }

        SetupDropPosition();
    }

    void SetupHandPosition(Camera cam)
    {
        Transform hand = cam.transform.Find("HandPosition");
        if (hand == null)
        {
            hand = new GameObject("HandPosition").transform;
            hand.SetParent(cam.transform);
            hand.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
        }
        inventory.handPosition = hand;
    }

    void SetupDropPosition()
    {
        Transform drop = transform.Find("DropPosition");
        if (drop == null)
        {
            drop = new GameObject("DropPosition").transform;
            drop.SetParent(transform);
            drop.localPosition = new Vector3(0, 1, 1);
        }
        inventory.dropPosition = drop;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (localCanvas != null)
        {
            Destroy(localCanvas.gameObject);
        }
    }
}