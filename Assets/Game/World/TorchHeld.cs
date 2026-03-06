using UnityEngine;
using UnityEngine.InputSystem;

public class TorchHeld : MonoBehaviour
{
    [SerializeField] private Light torchLight;
    [SerializeField] private bool startOn = true;

    private bool _isOn;
    public bool IsOn => _isOn;

    private void Awake()
    {
        if (torchLight == null)
            torchLight = GetComponentInChildren<Light>(true);

        Set(startOn);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            Toggle();
    }

    public void Toggle() => Set(!_isOn);

    private void Set(bool on)
    {
        _isOn = on;
        if (torchLight != null)
            torchLight.enabled = on;
    }
}