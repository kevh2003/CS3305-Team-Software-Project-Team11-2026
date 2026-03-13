using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
// Coordinates CCTV ownership and server-side lure pings for enemies.
public class CctvSystemManager : NetworkBehaviour
{
    private const ulong NoCctvUser = ulong.MaxValue;

    public static CctvSystemManager Instance { get; private set; }

    public NetworkVariable<ulong> InUseByClientId = new(
        NoCctvUser,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Lure")]
    [SerializeField] private LayerMask lureEnemyMask = ~0;
    [SerializeField] private float maxLureRadius = 30f;
    [SerializeField] private float lureCooldownSeconds = 10f;

    [Header("Ping Marker")]
    [SerializeField] private float lurePingMarkerLifetime = 5f;
    [SerializeField] private float lurePingMarkerRadius = 0.22f;
    [SerializeField] private float lurePingHeightOffset = 0.12f;
    [SerializeField] private Color lurePingMarkerColor = new Color(1f, 0.1f, 0.1f, 1f);
    [SerializeField] private AudioClip lurePingSfx;
    [SerializeField, Range(0f, 1f)] private float lurePingSfxVolume = 1f;
    [SerializeField] private float lurePingSfxMaxDistance = 30f;

    private readonly Dictionary<ulong, float> _nextLureAllowedAtByClient = new();
    private Material _lurePingSharedMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            InUseByClientId.Value = NoCctvUser;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_lurePingSharedMaterial != null)
            Destroy(_lurePingSharedMaterial);
    }

    public bool IsInUseByClient(ulong clientId)
    {
        return InUseByClientId.Value == clientId;
    }

    public bool IsInUseByAnotherClient(ulong clientId)
    {
        ulong current = InUseByClientId.Value;
        return current != NoCctvUser && current != clientId;
    }

    public void ServerResetForNewMatch()
    {
        if (!IsServer) return;

        InUseByClientId.Value = NoCctvUser;
        _nextLureAllowedAtByClient.Clear();
    }

    public void ServerReleaseIfOwner(ulong clientId)
    {
        if (!IsServer) return;
        if (InUseByClientId.Value == clientId)
            InUseByClientId.Value = NoCctvUser;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterCctvServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!TryNormalizeLockOwner(senderId)) return;

        if (InUseByClientId.Value == NoCctvUser || InUseByClientId.Value == senderId)
            InUseByClientId.Value = senderId;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReleaseCctvServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (InUseByClientId.Value == senderId)
            InUseByClientId.Value = NoCctvUser;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLureAtPointServerRpc(Vector3 point, float radius, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!TryNormalizeLockOwner(senderId)) return;
        if (InUseByClientId.Value != senderId) return;

        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out var sender)) return;
        if (sender.PlayerObject == null) return;

        var ph = sender.PlayerObject.GetComponent<PlayerHealth>();
        if (ph != null && ph.IsDead.Value) return;

        float now = Time.time;
        if (_nextLureAllowedAtByClient.TryGetValue(senderId, out float nextAllowed) && now < nextAllowed)
            return;

        _nextLureAllowedAtByClient[senderId] = now + Mathf.Max(0.1f, lureCooldownSeconds);
        ServerLureEnemiesAtPoint(point, radius);
    }

    public void ServerLureEnemiesAtPoint(Vector3 point, float radius)
    {
        if (!IsServer) return;

        float safeRadius = Mathf.Clamp(radius, 0.5f, maxLureRadius);
        Collider[] hits = Physics.OverlapSphere(point, safeRadius, lureEnemyMask, QueryTriggerInteraction.Ignore);
        var seen = new HashSet<EnemyAI>();

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            var enemy = c.GetComponentInParent<EnemyAI>();
            if (enemy == null) continue;
            if (!seen.Add(enemy)) continue;
            if (!enemy.CanSeeWorldPoint(point, safeRadius, requireFacing: false)) continue;

            enemy.Lure(point);
        }

        SpawnLurePingClientRpc(point + Vector3.up * lurePingHeightOffset);
    }

    private bool TryNormalizeLockOwner(ulong requestingClientId)
    {
        if (!IsServer) return false;

        ulong current = InUseByClientId.Value;
        if (current == NoCctvUser)
            return true;

        if (!IsClientAlive(current))
            InUseByClientId.Value = NoCctvUser;

        current = InUseByClientId.Value;
        return current == NoCctvUser || current == requestingClientId;
    }

    private bool IsClientAlive(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return false;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        var ph = client.PlayerObject.GetComponent<PlayerHealth>();
        if (ph != null && ph.IsDead.Value) return false;

        return true;
    }

    [ClientRpc]
    private void SpawnLurePingClientRpc(Vector3 point)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "CctvPingMarker";
        marker.transform.position = point;
        marker.transform.localScale = Vector3.one * Mathf.Max(0.05f, lurePingMarkerRadius);

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
            marker.layer = ignoreRaycastLayer;

        var collider = marker.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetOrCreateLurePingMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (lurePingSfx != null)
        {
            var source = marker.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.maxDistance = Mathf.Max(1f, lurePingSfxMaxDistance);
            source.minDistance = 1f;
            source.playOnAwake = false;
            source.clip = lurePingSfx;
            source.volume = lurePingSfxVolume;
            source.Play();
        }

        Destroy(marker, Mathf.Max(0.1f, lurePingMarkerLifetime));
    }

    private Material GetOrCreateLurePingMaterial()
    {
        if (_lurePingSharedMaterial != null)
            return _lurePingSharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        _lurePingSharedMaterial = new Material(shader);

        if (_lurePingSharedMaterial.HasProperty("_BaseColor"))
            _lurePingSharedMaterial.SetColor("_BaseColor", lurePingMarkerColor);
        if (_lurePingSharedMaterial.HasProperty("_Color"))
            _lurePingSharedMaterial.SetColor("_Color", lurePingMarkerColor);
        if (_lurePingSharedMaterial.HasProperty("_EmissionColor"))
        {
            _lurePingSharedMaterial.EnableKeyword("_EMISSION");
            _lurePingSharedMaterial.SetColor("_EmissionColor", lurePingMarkerColor * 2f);
        }

        return _lurePingSharedMaterial;
    }
}