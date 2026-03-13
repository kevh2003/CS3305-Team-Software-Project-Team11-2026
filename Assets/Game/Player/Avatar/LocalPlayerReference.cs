using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Improved singleton helper that provides access to the local player's components.
/// Handles timing issues with NetworkPlayer.IsOwner by checking in multiple lifecycle methods.
/// </summary>
public class LocalPlayerReference : MonoBehaviour
{
    public static LocalPlayerReference Instance { get; private set; }
    
    public Camera PlayerCamera { get; private set; }
    public NetworkPlayer NetworkPlayer { get; private set; }
    public UnityEngine.InputSystem.PlayerInput PlayerInput { get; private set; }
    public Interactor Interactor { get; private set; }

    private bool _hasRegistered = false;
    private Coroutine _registerRoutine;

    private void Awake()
    {
        NetworkPlayer = GetComponent<NetworkPlayer>();
        PlayerCamera = GetComponentInChildren<Camera>(true);
        PlayerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        Interactor = GetComponent<Interactor>();

        Debug.Log($"LocalPlayerReference.Awake() on {gameObject.name}");
    }

    private void OnEnable()
    {
        if (_registerRoutine == null)
            _registerRoutine = StartCoroutine(RegisterWhenReady());
    }

    private void Start()
    {
        // Preserve eager registration attempt for immediate owner-ready cases.
        TryRegisterAsInstance();
    }

    private System.Collections.IEnumerator RegisterWhenReady()
    {
        while (!_hasRegistered)
        {
            // Remote player copies should never claim the singleton.
            if (NetworkPlayer != null && NetworkPlayer.IsSpawned && !NetworkPlayer.IsOwner)
                break;

            TryRegisterAsInstance();
            if (_hasRegistered)
                break;
            yield return null;
        }

        _registerRoutine = null;
    }

    private void TryRegisterAsInstance()
    {
        if (_hasRegistered) return;
        
        // Only set as instance if this is the local player
        if (NetworkPlayer != null && NetworkPlayer.IsOwner)
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"LocalPlayerReference: Multiple instances detected! Replacing {Instance.gameObject.name} with {gameObject.name}");
            }
            
            Instance = this;
            _hasRegistered = true;
            
            Debug.Log($"LocalPlayerReference: Registered as Instance for local player (ClientId: {NetworkPlayer.OwnerClientId})");
            Debug.Log($"  - Camera found: {PlayerCamera != null}");
            Debug.Log($"  - PlayerInput found: {PlayerInput != null}");
            Debug.Log($"  - Interactor found: {Interactor != null}");
        }
        else if (NetworkPlayer == null)
        {
            Debug.LogWarning("LocalPlayerReference: NetworkPlayer component not found!");
        }
        // Remote copies are expected and intentionally do not register.
    }

    private void OnDisable()
    {
        if (_registerRoutine != null)
        {
            StopCoroutine(_registerRoutine);
            _registerRoutine = null;
        }

        if (Instance == this)
        {
            Instance = null;
            _hasRegistered = false;
            Debug.Log("LocalPlayerReference: Unregistered Instance");
        }
    }
}