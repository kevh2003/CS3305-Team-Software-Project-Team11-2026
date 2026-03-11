using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public sealed class LobbyAvatarPodiumRoster : MonoBehaviour
{
    [Header("Podium Slots")]
    [SerializeField] private Transform[] slots;
    [SerializeField] private TMP_Text[] slotLabels;

    [Header("Model Placement")]
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 modelLocalScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.25f;

    private GameObject[] _activeModels = System.Array.Empty<GameObject>();
    private ulong[] _slotOwnerIds = System.Array.Empty<ulong>();
    private int[] _slotAvatarIds = System.Array.Empty<int>();
    private int[] _slotPrefabInstanceIds = System.Array.Empty<int>();
    private int[] _slotAnimationClipInstanceIds = System.Array.Empty<int>();
    private int[] _slotControllerInstanceIds = System.Array.Empty<int>();

    private void OnEnable()
    {
        if (slots == null || slots.Length == 0)
            Debug.LogWarning("[LobbyAvatarPodiumRoster] No slots assigned; avatar previews will not be shown.");

        EnsureSlotArrays();

        NetworkPlayer.AnyLobbyStateChanged += OnAnyLobbyStateChanged;
        InvokeRepeating(nameof(RefreshRoster), 0f, refreshInterval);
    }

    private void OnDisable()
    {
        NetworkPlayer.AnyLobbyStateChanged -= OnAnyLobbyStateChanged;
        CancelInvoke(nameof(RefreshRoster));
        ClearAllSlots();
    }

    private void OnAnyLobbyStateChanged(NetworkPlayer _)
    {
        RefreshRoster();
    }

    private void RefreshRoster()
    {
        if (slots == null || slots.Length == 0) return;
        EnsureSlotArrays();

        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) =>
        {
            int ownerCompare = a.OwnerClientId.CompareTo(b.OwnerClientId);
            if (ownerCompare != 0) return ownerCompare;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        int writeIndex = 0;
        var seenOwners = new HashSet<ulong>();
        for (int i = 0; i < players.Length && writeIndex < slots.Length; i++)
        {
            var player = players[i];
            if (player == null || !player.IsSpawned) continue;
            HideLivePlayerRenderers(player);
            if (!seenOwners.Add(player.OwnerClientId)) continue;

            ApplyPlayerToSlot(writeIndex, player);
            writeIndex++;
        }

        for (int slotIndex = writeIndex; slotIndex < slots.Length; slotIndex++)
            ClearSlot(slotIndex);
    }

    private void ApplyPlayerToSlot(int slotIndex, NetworkPlayer player)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        AvatarCatalog catalog = player.ResolveAvatarCatalog();
        int avatarId = player.SelectedAvatarId;
        GameObject prefab = null;
        AnimationClip previewClip = null;
        RuntimeAnimatorController previewController = null;
        Vector3 previewScaleMultiplier = Vector3.one;

        if (catalog != null && catalog.TryGet(avatarId, out var entry))
        {
            prefab = entry.previewPrefab;
            previewClip = entry.previewAnimationClip;
            previewController = entry.previewAnimatorController;
            previewScaleMultiplier = ResolveScaleMultiplier(entry.gameplayScaleMultiplier);
        }

        int prefabId = prefab != null ? prefab.GetInstanceID() : 0;
        int clipId = previewClip != null ? previewClip.GetInstanceID() : 0;
        int controllerId = previewController != null ? previewController.GetInstanceID() : 0;
        bool changed =
            _slotOwnerIds[slotIndex] != player.OwnerClientId ||
            _slotAvatarIds[slotIndex] != avatarId ||
            _slotPrefabInstanceIds[slotIndex] != prefabId ||
            _slotAnimationClipInstanceIds[slotIndex] != clipId ||
            _slotControllerInstanceIds[slotIndex] != controllerId;

        if (changed)
        {
            ReplaceSlotModel(slotIndex, prefab, previewClip, previewController, previewScaleMultiplier);
            _slotOwnerIds[slotIndex] = player.OwnerClientId;
            _slotAvatarIds[slotIndex] = avatarId;
            _slotPrefabInstanceIds[slotIndex] = prefabId;
            _slotAnimationClipInstanceIds[slotIndex] = clipId;
            _slotControllerInstanceIds[slotIndex] = controllerId;
        }

        if (slotLabels != null && slotIndex < slotLabels.Length && slotLabels[slotIndex] != null)
        {
            string ready = player.IsReadyInLobby ? "Ready" : "Not Ready";
            slotLabels[slotIndex].text = $"Player {slotIndex + 1} : {ready}";
        }
    }

    private void ReplaceSlotModel(
        int slotIndex,
        GameObject prefab,
        AnimationClip previewClip,
        RuntimeAnimatorController previewController,
        Vector3 previewScaleMultiplier)
    {
        ClearSlotModel(slotIndex);
        if (prefab == null || slots[slotIndex] == null) return;

        var model = Instantiate(prefab, slots[slotIndex]);
        model.transform.localPosition = modelLocalPosition;
        model.transform.localRotation = Quaternion.Euler(modelLocalEuler);
        model.transform.localScale = Vector3.Scale(modelLocalScale, previewScaleMultiplier);

        // Podium previews are local visuals only.
        foreach (var netObj in model.GetComponentsInChildren<NetworkObject>(true))
            Destroy(netObj);

        foreach (var netBehaviour in model.GetComponentsInChildren<NetworkBehaviour>(true))
            netBehaviour.enabled = false;

        foreach (var cam in model.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (var listener in model.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (var cc in model.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = false;

        AttachPreviewAnimationPlayer(model, previewClip, previewController);
        _activeModels[slotIndex] = model;
    }

    private static Vector3 ResolveScaleMultiplier(Vector3 value)
    {
        bool allZero = Mathf.Approximately(value.x, 0f)
                       && Mathf.Approximately(value.y, 0f)
                       && Mathf.Approximately(value.z, 0f);
        if (allZero) return Vector3.one;

        return new Vector3(
            Mathf.Approximately(value.x, 0f) ? 1f : value.x,
            Mathf.Approximately(value.y, 0f) ? 1f : value.y,
            Mathf.Approximately(value.z, 0f) ? 1f : value.z);
    }

    private static void AttachPreviewAnimationPlayer(
        GameObject model,
        AnimationClip previewClip,
        RuntimeAnimatorController previewController)
    {
        if (model == null || (previewClip == null && previewController == null)) return;

        foreach (var stateController in model.GetComponentsInChildren<AnimationStateController>(true))
            stateController.enabled = false;

        var fullBody = model.transform.Find("Full Body")?.gameObject;

        if (previewClip != null)
        {
            var hostAnimator = model.GetComponent<Animator>();
            if (hostAnimator == null)
                hostAnimator = model.AddComponent<Animator>();

            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null) continue;
                animator.enabled = false;
            }

            hostAnimator.enabled = false;
            hostAnimator.runtimeAnimatorController = null;

            var clipSampler = model.GetComponent<PreviewAnimationPlayer>() ?? model.AddComponent<PreviewAnimationPlayer>();
            clipSampler.enabled = true;
            clipSampler.Configure(previewClip, hostAnimator, model, fullBody);
            return;
        }

        if (previewController == null)
            return;

        var targetRoot = model.transform.Find("Full Body");
        if (targetRoot == null)
            targetRoot = model.transform;

        var animators = model.GetComponentsInChildren<Animator>(true);
        Avatar inheritedAvatar = null;
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            if (animator == null) continue;
            if (inheritedAvatar == null && animator.avatar != null)
                inheritedAvatar = animator.avatar;
            animator.enabled = false;
        }

        var targetAnimator = targetRoot.GetComponent<Animator>();
        if (targetAnimator == null)
            targetAnimator = targetRoot.gameObject.AddComponent<Animator>();

        if (targetAnimator.avatar == null && inheritedAvatar != null)
            targetAnimator.avatar = inheritedAvatar;

        var previewPlayer = targetRoot.GetComponent<PreviewAnimationPlayer>();
        if (previewPlayer != null)
            previewPlayer.enabled = false;

        targetAnimator.runtimeAnimatorController = previewController;
        targetAnimator.applyRootMotion = false;
        targetAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.enabled = true;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);
    }

    private static void HideLivePlayerRenderers(NetworkPlayer player)
    {
        if (player == null) return;

        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            renderer.enabled = false;
        }
    }

    private void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        ClearSlotModel(slotIndex);
        if (slotIndex < _slotOwnerIds.Length) _slotOwnerIds[slotIndex] = ulong.MaxValue;
        if (slotIndex < _slotAvatarIds.Length) _slotAvatarIds[slotIndex] = int.MinValue;
        if (slotIndex < _slotPrefabInstanceIds.Length) _slotPrefabInstanceIds[slotIndex] = int.MinValue;
        if (slotIndex < _slotAnimationClipInstanceIds.Length) _slotAnimationClipInstanceIds[slotIndex] = int.MinValue;
        if (slotIndex < _slotControllerInstanceIds.Length) _slotControllerInstanceIds[slotIndex] = int.MinValue;

        if (slotLabels != null && slotIndex < slotLabels.Length && slotLabels[slotIndex] != null)
            slotLabels[slotIndex].text = "Player...";
    }

    private void ClearSlotModel(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _activeModels.Length) return;
        if (_activeModels[slotIndex] != null)
        {
            Destroy(_activeModels[slotIndex]);
            _activeModels[slotIndex] = null;
        }
    }

    private void ClearAllSlots()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
            ClearSlot(i);
    }

    private void EnsureSlotArrays()
    {
        int required = slots != null ? slots.Length : 0;
        if (required < 0) required = 0;
        if (_activeModels.Length == required) return;

        _activeModels = new GameObject[required];
        _slotOwnerIds = new ulong[required];
        _slotAvatarIds = new int[required];
        _slotPrefabInstanceIds = new int[required];
        _slotAnimationClipInstanceIds = new int[required];
        _slotControllerInstanceIds = new int[required];

        for (int i = 0; i < required; i++)
        {
            _slotOwnerIds[i] = ulong.MaxValue;
            _slotAvatarIds[i] = int.MinValue;
            _slotPrefabInstanceIds[i] = int.MinValue;
            _slotAnimationClipInstanceIds[i] = int.MinValue;
            _slotControllerInstanceIds[i] = int.MinValue;
        }
    }
}