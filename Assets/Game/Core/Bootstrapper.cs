using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;

/*
 * Bootstrapper
 * 
 * App entry point (00_Bootstrap scene)
 * - Ensures a single persistent NetRig exists
 * - Initializes Unity Services
 * - Loads MainMenu scene
 */

public sealed class Bootstrapper : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject netRigPrefab;

    [Header("Startup")]
    [SerializeField] private string firstSceneName = "01_MainMenu";

    private static bool _booted;

    private async void Awake()
    {
        Debug.Log("[Bootstrap] Awake() running");

        // Prevent duplicate bootstraps across scenes
        if (_booted)
        {
            Destroy(gameObject);
            return;
        }
        _booted = true;

        DontDestroyOnLoad(gameObject);

        EnsureNetRig();

        Debug.Log("[Bootstrap] Initializing services...");
        await InitializeServicesSafe();

        Debug.Log("[Bootstrap] Loading first scene: " + firstSceneName);
        SceneManager.LoadScene(firstSceneName);
    }

    private void EnsureNetRig()
    {
        // Prevent duplicate NetworkManagers across scenes
        if (FindFirstObjectByType<Unity.Netcode.NetworkManager>() != null)
            return;

        if (netRigPrefab == null)
        {
            Debug.LogError("[Bootstrap] NetRig prefab not assigned!");
            return;
        }

        var rig = Instantiate(netRigPrefab);
        rig.name = "NetRig";
        DontDestroyOnLoad(rig);
    }

    private static async Task InitializeServicesSafe()
    {
        // LAN should still work even if services fail
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[Bootstrap] Services ready. PlayerID={AuthenticationService.Instance.PlayerId}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Bootstrap] Unity Services init failed (LAN OK): {e.Message}");
        }
    }
}