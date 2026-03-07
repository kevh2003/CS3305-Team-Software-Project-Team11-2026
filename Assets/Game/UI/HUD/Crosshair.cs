using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class Crosshair : NetworkBehaviour
{
    private GameObject crosshairObject;
    private Text interactText;
    private const string DefaultPromptText = "Press E";
    private bool promptOverridden = false;
    private bool showInteractUi = false;

    private Coroutine createRoutine;

    private PlayerHealth health;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.IsDead.OnValueChanged += OnDeadChanged;
        }

        StartCreateRoutine();
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    private void StartCreateRoutine()
    {
        if (createRoutine != null)
            StopCoroutine(createRoutine);

        createRoutine = StartCoroutine(EnsureCrosshairExistsRoutine());
    }

    private IEnumerator EnsureCrosshairExistsRoutine()
    {
        // Retry for a few seconds
        for (int i = 0; i < 60; i++) // ~6 seconds
        {
            CleanupBrokenReferences();

            if (crosshairObject == null || interactText == null)
            {
                Canvas canvas = FindCanvas();
                if (canvas != null)
                {
                    CreateCrosshair(canvas);
                }
            }

            if (crosshairObject != null && interactText != null)
            {
                UpdateVisibility(SceneManager.GetActiveScene().name);
                createRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogWarning("Crosshair: Could not create crosshair after retries.");
        createRoutine = null;
    }

    private void CleanupBrokenReferences()
    {
        if (crosshairObject == null)
            crosshairObject = null;

        if (interactText == null)
            interactText = null;
    }

    private void CreateCrosshair(Canvas canvas)
    {
        var existingCrosshair = canvas.transform.Find("Crosshair");
        if (existingCrosshair != null)
        {
            crosshairObject = existingCrosshair.gameObject;
        }
        else
        {
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
        }

        // Prompt
        var existingPrompt = canvas.transform.Find("InteractPrompt");
        if (existingPrompt != null)
        {
            interactText = existingPrompt.GetComponent<Text>();
        }
        else
        {
            CreateInteractPrompt(canvas);
        }

        Debug.Log("Crosshair created / reattached");
        UpdateVisibility(SceneManager.GetActiveScene().name);
    }

    private void CreateInteractPrompt(Canvas canvas)
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
        interactText.text = DefaultPromptText;
        interactText.fontSize = 16;
        interactText.color = Color.white;
        interactText.alignment = TextAnchor.MiddleCenter;
        interactText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        interactText.enabled = false;

        Debug.Log("Interact prompt created");
    }

    public void ShowInteractPrompt()
    {
        CleanupBrokenReferences();

        if (health != null && health.IsDead.Value) return;
        if (SceneManager.GetActiveScene().name != "03_Game") return;

        showInteractUi = true;

        if (crosshairObject != null)
            crosshairObject.SetActive(true);

        if (interactText != null)
        {
            // If no custom prompt set, force the default
            if (!promptOverridden)
                interactText.text = DefaultPromptText;

            interactText.enabled = true;
        }
    }

    public void HideInteractPrompt()
    {
        CleanupBrokenReferences();
        showInteractUi = false;

        if (interactText != null)
        {
            interactText.enabled = false;

            // When hide, clear any custom prompt
            promptOverridden = false;
            interactText.text = DefaultPromptText;
        }

        if (crosshairObject != null)
            crosshairObject.SetActive(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCreateRoutine();
        UpdateVisibility(scene.name);
    }

    private void UpdateVisibility(string sceneName)
    {
        bool inGame = (sceneName == "03_Game");
        bool isDead = (health != null && health.IsDead.Value);

        if (!inGame || isDead)
            showInteractUi = false;

        bool canShowInteractUi = inGame && !isDead && showInteractUi;

        CleanupBrokenReferences();

        if (crosshairObject != null)
            crosshairObject.SetActive(canShowInteractUi);

        if (interactText != null)
        {
            interactText.gameObject.SetActive(inGame && !isDead);
            if (!canShowInteractUi) interactText.enabled = false;
        }
    }

    public void Show()
    {
        CleanupBrokenReferences();

        if (crosshairObject != null && SceneManager.GetActiveScene().name == "03_Game")
            crosshairObject.SetActive(true);
    }

    public void Hide()
    {
        CleanupBrokenReferences();

        if (crosshairObject != null)
            crosshairObject.SetActive(false);

        HideInteractPrompt();
    }

    private Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        // Prefer persistent/game canvas if present
        foreach (Canvas c in canvases)
        {
            if (c == null) continue;
            if (!c.gameObject.activeInHierarchy) continue;

            if (c.name == "GameCanvas" || c.GetComponent<PersistentCanvas>() != null)
                return c;
        }

        // Fallback to any active canvas
        foreach (Canvas c in canvases)
        {
            if (c != null && c.gameObject.activeInHierarchy)
                return c;
        }

        return null;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (createRoutine != null)
        {
            StopCoroutine(createRoutine);
            createRoutine = null;
        }
    }

    public override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (health != null)
            health.IsDead.OnValueChanged -= OnDeadChanged;

        base.OnDestroy();
    }

    public void SetPromptText(string t)
    {
        CleanupBrokenReferences();
        if (interactText == null) return;

        promptOverridden = true;
        interactText.text = t;
    }
}