using Unity.Netcode;
using UnityEngine;

public class SecurityButton : NetworkBehaviour, IInteractable
{
    public enum ButtonState { RedDisabled = 0, YellowReady = 1, GreenDone = 2 }

    [Header("Visuals")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color red = Color.red;
    [SerializeField] private Color yellow = Color.yellow;
    [SerializeField] private Color green = Color.green;

    [Header("Wiring")]
    [SerializeField] private SecurityRoomController controller;
    [SerializeField] private int buttonIndex = 1; // 1,2,3
    [SerializeField] private float serverInteractRange = 4f;

    private NetworkVariable<int> state = new(
        (int)ButtonState.RedDisabled,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool CanInteract() => state.Value == (int)ButtonState.YellowReady;

    public bool Interact(Interactor interactor)
    {
        if (interactor == null || !interactor.IsOwner) return false;
        PressServerRpc();
        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void PressServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!IsSenderInRange(senderId)) return;
        if (controller == null) return;
        if (state.Value != (int)ButtonState.YellowReady) return;

        controller.ServerOnButtonPressed(buttonIndex, senderId);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        state.OnValueChanged += (_, __) => ApplyVisual();
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (buttonRenderer == null) return;

        var s = (ButtonState)state.Value;
        var c = (s == ButtonState.GreenDone) ? green : (s == ButtonState.YellowReady ? yellow : red);
        buttonRenderer.material.color = c;
    }

    public void ServerSetState(ButtonState newState)
    {
        if (!IsServer) return;
        state.Value = (int)newState;
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= serverInteractRange;
    }
}