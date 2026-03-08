using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyAvatarSelectorUI : MonoBehaviour
{
    [Header("Selector Controls")]
    [SerializeField] private Button previousAvatarButton;
    [SerializeField] private Button nextAvatarButton;
    [SerializeField] private TMP_Text avatarNameText;

    [Header("Ready Controls")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text readyStateText;

    private NetworkPlayer _localPlayer;

    private void OnEnable()
    {
        EnsureReferences();

        if (previousAvatarButton != null) previousAvatarButton.onClick.AddListener(SelectPreviousAvatar);
        if (nextAvatarButton != null) nextAvatarButton.onClick.AddListener(SelectNextAvatar);
        if (readyButton != null) readyButton.onClick.AddListener(ToggleReady);

        NetworkPlayer.AnyLobbyStateChanged += OnAnyLobbyStateChanged;
        InvokeRepeating(nameof(RefreshUi), 0f, 0.25f);
    }

    private void OnDisable()
    {
        if (previousAvatarButton != null) previousAvatarButton.onClick.RemoveListener(SelectPreviousAvatar);
        if (nextAvatarButton != null) nextAvatarButton.onClick.RemoveListener(SelectNextAvatar);
        if (readyButton != null) readyButton.onClick.RemoveListener(ToggleReady);

        NetworkPlayer.AnyLobbyStateChanged -= OnAnyLobbyStateChanged;
        CancelInvoke(nameof(RefreshUi));
    }

    private void OnAnyLobbyStateChanged(NetworkPlayer _)
    {
        RefreshUi();
    }

    private void SelectPreviousAvatar()
    {
        if (!TryGetLocalPlayer(out var player)) return;

        int count = AvatarCatalogUtility.GetAvatarCount(player.ResolveAvatarCatalog());
        if (count <= 0) return;

        int next = player.SelectedAvatarId - 1;
        if (next < 0) next = count - 1;
        player.RequestSetSelectedAvatar(next);
    }

    private void SelectNextAvatar()
    {
        if (!TryGetLocalPlayer(out var player)) return;

        int count = AvatarCatalogUtility.GetAvatarCount(player.ResolveAvatarCatalog());
        if (count <= 0) return;

        int next = (player.SelectedAvatarId + 1) % count;
        player.RequestSetSelectedAvatar(next);
    }

    private void ToggleReady()
    {
        if (!TryGetLocalPlayer(out var player)) return;
        player.RequestSetReadyInLobby(!player.IsReadyInLobby);
    }

    private bool TryGetLocalPlayer(out NetworkPlayer player)
    {
        if (_localPlayer != null && _localPlayer.IsSpawned && _localPlayer.IsOwner)
        {
            player = _localPlayer;
            return true;
        }

        _localPlayer = null;

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.IsSpawned || !p.IsOwner) continue;
            _localPlayer = p;
            break;
        }

        player = _localPlayer;
        return player != null;
    }

    private void RefreshUi()
    {
        bool hasPlayer = TryGetLocalPlayer(out var player);
        bool hasCatalog = hasPlayer && AvatarCatalogUtility.GetAvatarCount(player.ResolveAvatarCatalog()) > 0;

        if (previousAvatarButton != null) previousAvatarButton.interactable = hasCatalog;
        if (nextAvatarButton != null) nextAvatarButton.interactable = hasCatalog;
        if (readyButton != null) readyButton.interactable = hasPlayer;

        if (avatarNameText != null)
        {
            if (!hasPlayer)
            {
                avatarNameText.text = "No Player";
            }
            else
            {
                var catalog = player.ResolveAvatarCatalog();
                avatarNameText.text = catalog != null
                    ? catalog.GetDisplayNameOrFallback(player.SelectedAvatarId)
                    : $"Avatar {player.SelectedAvatarId + 1}";
            }
        }

        if (readyButtonText != null)
            readyButtonText.text = hasPlayer && player.IsReadyInLobby ? "Not Ready" : "Ready";

        if (readyStateText != null)
            readyStateText.text = hasPlayer && player.IsReadyInLobby ? "READY" : "NOT READY";
    }

    private void EnsureReferences()
    {
        if (previousAvatarButton != null &&
            nextAvatarButton != null &&
            readyButton != null &&
            avatarNameText != null &&
            readyStateText != null)
            return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        TMP_FontAsset sharedFont = null;
        var sampleText = canvas.GetComponentInChildren<TMP_Text>(true);
        if (sampleText != null) sharedFont = sampleText.font;

        Transform root = canvas.transform.Find("AvatarSelector_Auto");
        if (root == null)
        {
            var go = new GameObject("AvatarSelector_Auto", typeof(RectTransform), typeof(CanvasGroup));
            root = go.transform;
            root.SetParent(canvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            rt.sizeDelta = new Vector2(460f, 210f);
        }

        if (avatarNameText == null)
            avatarNameText = CreateLabel(root, "AvatarNameText_Auto", new Vector2(0f, 78f), 340f, 36, "Avatar 1", sharedFont);

        if (readyStateText == null)
            readyStateText = CreateLabel(root, "ReadyStateText_Auto", new Vector2(0f, -4f), 340f, 22, "NOT READY", sharedFont);

        if (readyButtonText == null)
            readyButtonText = null;

        if (previousAvatarButton == null)
            previousAvatarButton = CreateButton(root, "AvatarLeftButton_Auto", new Vector2(-150f, 78f), new Vector2(80f, 52f), "<", sharedFont);

        if (nextAvatarButton == null)
            nextAvatarButton = CreateButton(root, "AvatarRightButton_Auto", new Vector2(150f, 78f), new Vector2(80f, 52f), ">", sharedFont);

        if (readyButton == null)
            readyButton = CreateButton(root, "ReadyButton_Auto", new Vector2(0f, -56f), new Vector2(240f, 58f), "Ready", sharedFont);

        if (readyButton != null && readyButtonText == null)
        {
            readyButtonText = readyButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        float labelWidth,
        int fontSize,
        string text,
        TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMP_Text));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(labelWidth, 44f);

        var label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.color = Color.white;
        label.raycastTarget = false;
        if (font != null) label.font = font;
        return label;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string text,
        TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.98f, 0.98f, 0.98f, 0.96f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMP_Text));
        textGo.transform.SetParent(go.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var label = textGo.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.color = new Color(0.13f, 0.13f, 0.13f, 1f);
        label.raycastTarget = false;
        if (font != null) label.font = font;

        return button;
    }
}