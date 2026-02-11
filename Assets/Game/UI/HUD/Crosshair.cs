using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class Crosshair : NetworkBehaviour
{
    private GameObject crosshairObject;

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

        // Create crosshair as a small circle
        crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = crosshairObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(8, 8); // Small circle

        Image image = crosshairObject.AddComponent<Image>();
        
        // Use a circle sprite (Unity's default UI sprite works)
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        image.color = Color.white;
        image.type = Image.Type.Simple;

        UpdateVisibility(SceneManager.GetActiveScene().name);
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