using UnityEngine;
using UnityEngine.InputSystem;

public class TorchHeld : MonoBehaviour
{
    [SerializeField] private Light torchLight;
    [SerializeField] private bool startOn = true;

    private bool _isOn;
    private PlayerSoundFX _soundFX;
    public bool IsOn => _isOn;

    private void Awake()
    {
        if (torchLight == null)
            torchLight = GetComponentInChildren<Light>(true);

        ResolveSoundFx();
        Set(startOn, false);
    }

    private void OnEnable()
    {
        ResolveSoundFx();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            Toggle();
    }

    public void Toggle() => Set(!_isOn, true);

    private void Set(bool on, bool playSound)
    {
        bool changed = _isOn != on;
        _isOn = on;
        if (torchLight != null)
            torchLight.enabled = on;

        if (_soundFX == null)
            ResolveSoundFx();

        if (playSound && changed)
            _soundFX?.PlayTorchToggleSound();
    }

    private void ResolveSoundFx()
    {
        if (_soundFX != null) return;
        _soundFX = GetComponentInParent<PlayerSoundFX>(true);
    }
}