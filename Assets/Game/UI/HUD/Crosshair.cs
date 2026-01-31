using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Creates a simple crosshair in the center of the screen.
/// Only created for the local player.
/// </summary>
public class Crosshair : NetworkBehaviour
{
    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public float crosshairSize = 10f;
    public float crosshairThickness = 2f;

    private GameObject crosshairObject;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"🎯 Crosshair.OnNetworkSpawn() - IsOwner: {IsOwner}");

        if (!IsOwner)
        {
            Debug.Log("❌ Not owner, disabling Crosshair");
            enabled = false;
            return;
        }

        Debug.Log("✅ Is owner, creating crosshair in 0.1s...");
        Invoke(nameof(CreateCrosshair), 0.1f);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (crosshairObject != null)
            Destroy(crosshairObject);
    }

    void CreateCrosshair()
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogError("Crosshair: No Canvas found!");
            return;
        }

        crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(canvas.transform);

        RectTransform rect = crosshairObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(30, 30);

        CreateLine(crosshairObject, "Horizontal", new Vector2(crosshairSize, crosshairThickness));
        CreateLine(crosshairObject, "Vertical", new Vector2(crosshairThickness, crosshairSize));

        Debug.Log("✅ Crosshair created!");
    }

    void CreateLine(GameObject parent, string name, Vector2 size)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent.transform);

        RectTransform rect = line.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image img = line.AddComponent<Image>();
        img.color = crosshairColor;
    }

    Canvas FindCanvas()
    {
        // Try to find the persistent GameCanvas first
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        foreach (Canvas c in canvases)
        {
            if (c.name == "GameCanvas" || c.GetComponent<PersistentCanvas>() != null)
            {
                Debug.Log($"✅ Crosshair found GameCanvas: {c.name}");
                return c;
            }
        }

        // Fallback
        Debug.LogWarning("⚠️ Crosshair: GameCanvas not found, using first available canvas");
        return canvases.Length > 0 ? canvases[0] : null;
    }
}