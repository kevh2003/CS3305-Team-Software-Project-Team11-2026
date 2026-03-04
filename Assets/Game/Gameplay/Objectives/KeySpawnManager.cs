using Unity.Netcode;
using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem; // editor-only debug hotkey (Input System)
#endif

public class KeySpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObjectiveState objectiveState;

    [Header("Key Prefab (MUST have NetworkObject on root)")]
    [SerializeField] private NetworkObject keyWorldPrefab;

    [Header("Possible spawn locations")]
    [SerializeField] private Transform[] keySpawnPoints;

    private bool _subscribed;

#if UNITY_EDITOR
    [Header("Editor Debug")]
    [SerializeField] private bool enableEditorDebugHotkey = true; // toggle in inspector
    private int _debugIndex = 0;
    private NetworkObject _lastDebugSpawnedKey;
#endif

    private void Awake()
    {
        if (objectiveState == null)
            objectiveState = ObjectiveState.Instance != null ? ObjectiveState.Instance : FindFirstObjectByType<ObjectiveState>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (objectiveState == null) return;

        // Only server should drive spawning
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        objectiveState.DucksFound.OnValueChanged += OnAnyObjectiveChanged;
        objectiveState.CurrentSubmitCount.OnValueChanged += OnAnyObjectiveChanged;
        objectiveState.RequiredSubmitCount.OnValueChanged += OnAnyObjectiveChanged;
        objectiveState.PreKeyExtraCompleted.OnValueChanged += OnAnyObjectiveChanged;
        objectiveState.PreKeyExtraRequired.OnValueChanged += OnAnyObjectiveChanged;

        _subscribed = true;

        // Evaluate once at start
        EvaluateAndSpawnIfReady();
    }

    private void Unsubscribe()
    {
        if (!_subscribed || objectiveState == null) return;

        objectiveState.DucksFound.OnValueChanged -= OnAnyObjectiveChanged;
        objectiveState.CurrentSubmitCount.OnValueChanged -= OnAnyObjectiveChanged;
        objectiveState.RequiredSubmitCount.OnValueChanged -= OnAnyObjectiveChanged;
        objectiveState.PreKeyExtraCompleted.OnValueChanged -= OnAnyObjectiveChanged;
        objectiveState.PreKeyExtraRequired.OnValueChanged -= OnAnyObjectiveChanged;

        _subscribed = false;
    }

    private void OnAnyObjectiveChanged(int oldValue, int newValue) => EvaluateAndSpawnIfReady();
    private void OnAnyObjectiveChanged(bool oldValue, bool newValue) => EvaluateAndSpawnIfReady();

    private void EvaluateAndSpawnIfReady()
    {
        if (objectiveState == null) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // Already spawned this round?
        if (objectiveState.KeySpawned.Value) return;

        // Ready?
        if (!objectiveState.ServerArePreKeyObjectivesComplete()) return;

        // Validate
        if (keyWorldPrefab == null)
        {
            Debug.LogError("[KeySpawnManager] keyWorldPrefab not assigned.");
            return;
        }

        if (keySpawnPoints == null || keySpawnPoints.Length == 0)
        {
            Debug.LogError("[KeySpawnManager] No keySpawnPoints assigned.");
            return;
        }

        // Pick a random spawn point (server-authoritative)
        int idx = Random.Range(0, keySpawnPoints.Length);
        Transform sp = keySpawnPoints[idx];

        SpawnKeyAt(sp.position, sp.rotation, markAsSpawned: true);

        Debug.Log($"[KeySpawnManager] Spawned key at spawn point index {idx}: {sp.name}");
    }

    private void SpawnKeyAt(Vector3 pos, Quaternion rot, bool markAsSpawned)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var key = Instantiate(keyWorldPrefab, pos, rot);
        key.Spawn(true);

        if (markAsSpawned && objectiveState != null)
            objectiveState.KeySpawned.Value = true;

#if UNITY_EDITOR
        // track last debug spawn so it can despawn on next debug press
        _lastDebugSpawnedKey = key;
#endif
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!enableEditorDebugHotkey) return;

        // Debug hotkey is server-only.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            DebugSpawnNextPoint();
    }

    private void DebugSpawnNextPoint()
    {
        if (keyWorldPrefab == null)
        {
            Debug.LogError("[KeySpawnManager] keyWorldPrefab not assigned.");
            return;
        }

        if (keySpawnPoints == null || keySpawnPoints.Length == 0)
        {
            Debug.LogError("[KeySpawnManager] No keySpawnPoints assigned.");
            return;
        }

        // Optional: remove previous debug key
        if (_lastDebugSpawnedKey != null && _lastDebugSpawnedKey.IsSpawned)
            _lastDebugSpawnedKey.Despawn(true);

        int idx = _debugIndex % keySpawnPoints.Length;
        _debugIndex++;

        var sp = keySpawnPoints[idx];

        // Debug spawn should not mark KeySpawned, so it doesn't interfere with real objective gate.
        SpawnKeyAt(sp.position, sp.rotation, markAsSpawned: false);

        Debug.Log($"[KeySpawnManager][EDITOR] Debug spawned key at index {idx}: {sp.name}");
    }
#endif
}