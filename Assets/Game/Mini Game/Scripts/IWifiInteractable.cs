using UnityEngine;

public interface IWifiInteractable
{
    public string InteractText { get; }
    public void Interact(CharacterController player);
}
