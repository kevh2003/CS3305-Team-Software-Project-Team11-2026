using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class CrosshairUI : NetworkBehaviour
{
    [Header("Crosshair Settings")]
    public Color crosshairColor = Color.white;
    public float crosshairSize = 10f;
    public float crosshairThickness = 2f;
    
    private Canvas canvas;
    private GameObject crosshairContainer;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only create crosshair for local player
        if (IsOwner)
        {
            Invoke(nameof(CreateCrosshair), 0.1f); // Small delay to ensure canvas exists
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Clean up crosshair when player despawns
        CleanupCrosshair();
    }
    
    void CreateCrosshair()
    {
        // Find canvas by name (matches your InventoryCanvas)
        GameObject canvasObj = GameObject.Find("InventoryCanvas");
        if (canvasObj != null)
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }
        
        if (canvas == null)
        {
            Debug.LogError("InventoryCanvas not found for crosshair!");
            return;
        }
        
        // Create container
        crosshairContainer = new GameObject("Crosshair");
        crosshairContainer.transform.SetParent(canvas.transform);
        crosshairContainer.transform.SetAsFirstSibling(); // Put behind other UI
        
        RectTransform containerRect = crosshairContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(crosshairSize * 3, crosshairSize * 3);
        
        // Create horizontal line
        CreateCrosshairLine("Horizontal", new Vector2(crosshairSize, crosshairThickness));
        
        // Create vertical line
        CreateCrosshairLine("Vertical", new Vector2(crosshairThickness, crosshairSize));
        
        Debug.Log("Crosshair created for local player");
    }
    
    void CreateCrosshairLine(string name, Vector2 size)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(crosshairContainer.transform);
        
        RectTransform rect = line.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        
        Image img = line.AddComponent<Image>();
        img.color = crosshairColor;
    }
    
    public void SetVisible(bool visible)
    {
        if (crosshairContainer != null)
            crosshairContainer.SetActive(visible);
    }
    
    void CleanupCrosshair()
    {
        if (crosshairContainer != null)
        {
            Destroy(crosshairContainer);
            crosshairContainer = null;
        }
    }
}