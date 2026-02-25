using Unity.Netcode;
using UnityEngine;

public class ObjectiveState : NetworkBehaviour
{
    public static ObjectiveState Instance { get; private set; }

    [SerializeField] private int ducksTotal = 6;

    public NetworkVariable<int> DucksFound = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public int DucksTotal => ducksTotal;

    public override void OnNetworkSpawn()
    {
        // Ensures objectives reset each time the host starts
        if (IsServer)
            DucksFound.Value = 0;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterDuckServerRpc()
    {
        if (DucksFound.Value >= ducksTotal) return;
        DucksFound.Value++;
    }
}