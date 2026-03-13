using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int MaxHealth => maxHealth;

    [Header("Death Behaviour")]
    [SerializeField] private string aliveLayer = "WhatsIsPlayer";
    [SerializeField] private string deadLayer = "DeadPlayer";
    [SerializeField] private string gameSceneName = "03_Game";

    [Tooltip("Scripts to disable when dead (Interactor etc).")]
    [SerializeField] private Behaviour[] disableOnDeath;

    [Tooltip("Optional root object containing meshes.")]
    [SerializeField] private GameObject visualsRoot;

    [Header("Game Over Flow")]
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private float gameOverDelaySeconds = 3f;

    private static bool s_gameOverTriggered;

    // Round-scoped flag reset by MatchStartResetter when a new match begins.
    public static void ServerResetGlobalGameOverFlag()
    {
        s_gameOverTriggered = false;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDead.Value = false;
        }

        // React to death changes on ALL clients
        IsDead.OnValueChanged += OnDeadChanged;

        ApplyDeathState(IsDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDead.OnValueChanged -= OnDeadChanged;
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        ApplyDeathState(current);
    }

    private void ApplyDeathState(bool dead)
    {
        // Layer swap
        int alive = LayerMask.NameToLayer(aliveLayer);
        int deadL = LayerMask.NameToLayer(deadLayer);

        if (alive != -1 && deadL != -1)
            gameObject.layer = dead ? deadL : alive;

        // Disable interaction scripts
        if (disableOnDeath != null)
        {
            foreach (var script in disableOnDeath)
            {
                if (script != null)
                    script.enabled = !dead;
            }
        }

        // Avoid forcing lobby visibility; lobby uses dedicated avatar previews instead.
        bool isGameScene = SceneManager.GetActiveScene().name == gameSceneName;
        if (isGameScene)
        {
            if (visualsRoot != null)
            {
                visualsRoot.SetActive(!dead);
            }
            else
            {
                // fallback
                foreach (var r in GetComponentsInChildren<Renderer>())
                    r.enabled = !dead;
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;

        if (IsServer) ApplyDamage(amount);
        else TakeDamageServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        // Security: only the owning client can request damage on this player object.
        if (!IsAuthorizedRpcSender(rpcParams.Receive.SenderClientId)) return;
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        if (IsDead.Value) return;

        
        CurrentHealth.Value = Mathf.Clamp(CurrentHealth.Value - amount, 0, maxHealth);

        if (CurrentHealth.Value <= 0)
        {
            IsDead.Value = true;

            CctvSystemManager.Instance?.ServerReleaseIfOwner(OwnerClientId);
            StartGame.ServerReleaseAllIfOwner(OwnerClientId);

            // Drop all inventory items on death
            var inv = GetComponent<PlayerInventory>();
            if (inv != null)
            {
                inv.DropAllItemsOnDeathServer();
            }

            // Assignment requires every player, so a dead player that hasn't completed it shouldn't soft-lock it
            // This reduces RequiredSubmitCount by 1 IF this player was required and not submitted yet. - kev
            if (ObjectiveState.Instance != null)
            {
                ObjectiveState.Instance.ServerHandlePlayerDeath(OwnerClientId);
                var security = FindFirstObjectByType<SecurityRoomController>();
                if (security != null)
                    security.ServerOnRosterChanged();
            }

            // after a player dies, check if that was the last one
            TryTriggerGameOverServer();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetHealthServerRpc(ServerRpcParams rpcParams = default)
    {
        // Security: prevent clients from resetting other players.
        if (!IsAuthorizedRpcSender(rpcParams.Receive.SenderClientId)) return;
        CurrentHealth.Value = maxHealth;
        IsDead.Value = false;
    }

    private bool IsAuthorizedRpcSender(ulong senderId)
    {
        if (senderId == OwnerClientId) return true;
        return senderId == NetworkManager.ServerClientId;
    }

    private void TryTriggerGameOverServer()
    {
        if (!IsServer) return;
        if (s_gameOverTriggered) return;

        // Find all PlayerHealth in the scene (includes inactive)
        var all = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return;

        // If any spawned player is still alive, don't trigger
        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i].IsSpawned) continue;
            if (!all[i].IsDead.Value) return;
        }

        // Everyone is dead :O
        s_gameOverTriggered = true;

        ShowGameOverClientRpc();
        StartCoroutine(ReturnToLobbyAfterDelay());
    }

    public void KillPlayer()
    {
        // Route through the normal damage path so death side-effects stay consistent.
        if (IsServer) ApplyDamage(maxHealth);
        else TakeDamageServerRpc(maxHealth);
    }

    private System.Collections.IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSeconds(gameOverDelaySeconds);

        if (NetworkManager.Singleton == null) yield break;
        if (NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogError("[PlayerHealth] Network SceneManager not enabled on NetworkManager.");
            yield break;
        }

        // Host drives the scene change for everyone
        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    [ClientRpc]
    private void ShowGameOverClientRpc()
    {
        PlayerHealthUI.ShowGameOver();
    }
}