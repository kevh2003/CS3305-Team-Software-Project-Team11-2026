using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentCanvas : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject hotbarPanel;
    [SerializeField] private GameObject inventoryPanel;

    void Awake()
    {
        // Check if there's already a persistent canvas
        PersistentCanvas[] canvases = FindObjectsByType<PersistentCanvas>(FindObjectsSortMode.None);
        if (canvases.Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        Debug.Log("✅ GameCanvas set to DontDestroyOnLoad");
        
        // Subscribe to scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Auto-find panels if not assigned
        if (hotbarPanel == null)
            hotbarPanel = transform.Find("HotbarPanel")?.gameObject;
        
        if (inventoryPanel == null)
            inventoryPanel = transform.Find("InventoryPanel")?.gameObject;
        
        // Set initial state based on current scene
        UpdateUIVisibility(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"🎬 Scene loaded: {scene.name}");
        UpdateUIVisibility(scene.name);
    }

    void UpdateUIVisibility(string sceneName)
    {
        bool showUI = sceneName == "03_Game";
        
        if (hotbarPanel != null)
        {
            hotbarPanel.SetActive(showUI);
            Debug.Log($"HotbarPanel: {(showUI ? "VISIBLE" : "HIDDEN")} in {sceneName}");
        }
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false); // Always start hidden (Tab opens it)
        }
        
        Debug.Log($"✅ UI visibility updated for scene: {sceneName}");
    }
}