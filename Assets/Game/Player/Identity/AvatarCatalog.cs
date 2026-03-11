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