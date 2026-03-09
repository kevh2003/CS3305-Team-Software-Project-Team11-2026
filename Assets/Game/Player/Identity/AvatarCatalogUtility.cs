using UnityEngine;

public static class AvatarCatalogUtility
{
    private const string DefaultResourcesPath = "AvatarCatalog";
    private static AvatarCatalog _cachedCatalog;

    public static AvatarCatalog ResolveCatalog(AvatarCatalog explicitCatalog = null)
    {
        if (explicitCatalog != null)
            return explicitCatalog;

        if (_cachedCatalog == null)
            _cachedCatalog = Resources.Load<AvatarCatalog>(DefaultResourcesPath);

        return _cachedCatalog;
    }

    public static int GetAvatarCount(AvatarCatalog explicitCatalog = null)
    {
        var catalog = ResolveCatalog(explicitCatalog);
        if (catalog == null) return 1;
        return Mathf.Max(1, catalog.Count);
    }
}