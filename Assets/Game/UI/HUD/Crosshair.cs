using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class Crosshair : NetworkBehaviour
{
    private GameObject crosshairObject;
    private Text interactText;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Invoke(nameof(CreateCrosshair), 0.2f);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void CreateCrosshair()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogError("Crosshair: No canvas found");
            return;
        }

        // Create crosshair circle
        crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = crosshairObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8, 8);

        Image image = crosshairObject.AddComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        image.color = Color.white;
        image.type = Image.Type.Simple;

        // Create "Press E" text
        CreateInteractPrompt(canvas);

        Debug.Log("Crosshair created");
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    void CreateInteractPrompt(Canvas canvas)
    {
        GameObject textObj = new GameObject("InteractPrompt");
        textObj.transform.SetParent(canvas.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0, -15);
        textRect.sizeDelta = new Vector2(200, 30);

        interactText = textObj.AddComponent<Text>();
        interactText.text = "Press E";
        interactText.fontSize = 16;
        interactText.color = Color.white;
        interactText.alignment = TextAnchor.MiddleCenter;
        interactText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        interactText.enabled = false;

        Debug.Log("Interact prompt created");
    }

    public void ShowInteractPrompt()
    {
        if (interactText != null)
        {
            interactText.enabled = true;
        }
    }

    public void HideInteractPrompt()
    {
        if (interactText != null)
        {
            interactText.enabled = false;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateVisibility(scene.name);
    }

    void UpdateVisibility(string sceneName)
    {
        bool showCrosshair = (sceneName == "03_Game");
        
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(showCrosshair);
        }
        
        if (interactText != null)
        {
            interactText.gameObject.SetActive(showCrosshair);
            interactText.enabled = false;
        }
    }

    public void Show()
    {
        if (crosshairObject != null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "03_Game")
            {
                crosshairObject.SetActive(true);
            }
        }
    }

    public void Hide()
    {
        if (crosshairObject != null)
        {
            crosshairObject.SetActive(false);
        }
        
        HideInteractPrompt();
    }

    Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        
        foreach (Canvas c in canvases)
        {
            if (c.name == "GameCanvas" || c.GetComponent<PersistentCanvas>() != null)
            {
                return c;
            }
        }
        
        if (canvases.Length > 0)
        {
            return canvases[0];
        }
        
        return null;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}