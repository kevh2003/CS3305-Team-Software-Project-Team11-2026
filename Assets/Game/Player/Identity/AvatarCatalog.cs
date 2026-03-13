using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvatarCatalog", menuName = "Game/Player/Avatar Catalog")]
public sealed class AvatarCatalog : ScriptableObject
{
    [Serializable]
    public struct AvatarEntry
    {
        public string displayName;
        public GameObject previewPrefab;
        public AnimationClip previewAnimationClip;
        public RuntimeAnimatorController previewAnimatorController;
        [Header("Lobby Preview")]
        public Vector3 previewPositionOffset;
        public Vector3 previewEulerOffset;
        public Vector3 previewScaleMultiplier;
        [Header("Gameplay")]
        public GameObject gameplayPrefab;
        public RuntimeAnimatorController gameplayAnimatorController;
        public Vector3 gameplayPositionOffset;
        public Vector3 gameplayEulerOffset;
        public Vector3 gameplayScaleMultiplier;
        public Vector3 cameraLocalOffset;
        public bool overrideController;
        public float controllerHeight;
        public float controllerRadius;
        public Vector3 controllerCenter;
        public Sprite icon;
    }

    [SerializeField] private List<AvatarEntry> avatars = new();

    public int Count => avatars != null ? avatars.Count : 0;

    public bool TryGet(int index, out AvatarEntry entry)
    {
        if (avatars == null || index < 0 || index >= avatars.Count)
        {
            entry = default;
            return false;
        }

        entry = avatars[index];
        return true;
    }

    public string GetDisplayNameOrFallback(int index)
    {
        if (!TryGet(index, out var entry))
            return $"Avatar {index + 1}";

        return string.IsNullOrWhiteSpace(entry.displayName)
            ? $"Avatar {index + 1}"
            : entry.displayName;
    }
}