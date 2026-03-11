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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressClip;
    [SerializeField, Range(0f, 1f)] private float pressVolume = 1f;

    private NetworkVariable<int> state = new(
        (int)ButtonState.RedDisabled,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    private NetworkVariable<int>.OnValueChangedDelegate _onStateChanged;

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

        PlayPressAudioClientRpc();
        controller.ServerOnButtonPressed(buttonIndex, senderId);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _onStateChanged ??= OnStateChanged;
        state.OnValueChanged += _onStateChanged;

        EnsureAudioSource();
        ApplyVisual();
    }

    public override void OnNetworkDespawn()
    {
        if (_onStateChanged != null)
            state.OnValueChanged -= _onStateChanged;

        base.OnNetworkDespawn();
    }

    private void OnStateChanged(int previousValue, int newValue)
    {
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

    [ClientRpc]
    private void PlayPressAudioClientRpc()
    {
        if (pressClip == null) return;
        EnsureAudioSource();
        if (audioSource == null) return;
        audioSource.PlayOneShot(pressClip, pressVolume);
    }

    private bool IsSenderInRange(ulong senderId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        if (!nm.ConnectedClients.TryGetValue(senderId, out var client)) return false;
        if (client.PlayerObject == null) return false;

        return Vector3.Distance(client.PlayerObject.transform.position, transform.position) <= serverInteractRange;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null) return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 15f;
    }
}