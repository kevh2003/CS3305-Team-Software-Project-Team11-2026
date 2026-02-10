using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Improved singleton helper that provides access to the local player's components.
/// Handles timing issues with NetworkPlayer.IsOwner by checking in multiple lifecycle methods.
/// Add this script to your NetworkPlayer prefab.
/// Specifically designed for hunter's camera interaction system.
/// </summary>
public class LocalPlayerReference : MonoBehaviour
{
    public static LocalPlayerReference Instance { get; private set; }
    
    public Camera PlayerCamera { get; private set; }
    public NetworkPlayer NetworkPlayer { get; private set; }
    public UnityEngine.InputSystem.PlayerInput PlayerInput { get; private set; }
    public Interactor Interactor { get; private set; }  // Added for convenience

    private bool _hasRegistered = false;

    private void Awake()
    {
        NetworkPlayer = GetComponent<NetworkPlayer>();
        PlayerCamera = GetComponentInChildren<Camera>(true);
        PlayerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        Interactor = GetComponent<Interactor>();  // Cache the Interactor reference

        Debug.Log($"LocalPlayerReference.Awake() on {gameObject.name}");
    }

    private void OnEnable()
    {
        TryRegisterAsInstance();
    }

    private void Start()
    {
        // Try again in Start in case IsOwner wasn't ready in OnEnable
        TryRegisterAsInstance();
    }

    private void Update()
    {
        // Keep trying until we successfully register (for the first few frames)
        if (!_hasRegistered && Time.frameCount < 10)
        {
            TryRegisterAsInstance();
        }
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
            
            Debug.Log($"✅ LocalPlayerReference: Registered as Instance for local player (ClientId: {NetworkPlayer.OwnerClientId})");
            Debug.Log($"  - Camera found: {PlayerCamera != null}");
            Debug.Log($"  - PlayerInput found: {PlayerInput != null}");
            Debug.Log($"  - Interactor found: {Interactor != null}");
        }
        else if (NetworkPlayer == null)
        {
            Debug.LogWarning("LocalPlayerReference: NetworkPlayer component not found!");
        }
        else if (!NetworkPlayer.IsOwner)
        {
            // This is a remote player, not the local one
            Debug.Log($"LocalPlayerReference: Not owner (ClientId: {NetworkPlayer.OwnerClientId}), skipping registration");
        }
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
            _hasRegistered = false;
            Debug.Log("LocalPlayerReference: Unregistered Instance");
        }
    }
}
