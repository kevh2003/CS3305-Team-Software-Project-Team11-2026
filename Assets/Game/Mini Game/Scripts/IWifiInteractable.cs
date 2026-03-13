using UnityEngine;

// Legacy interaction contract used by the WiFi minigame entry point.
public interface IWifiInteractable
{
    public string InteractText { get; }
    public void Interact(CharacterController player);
}