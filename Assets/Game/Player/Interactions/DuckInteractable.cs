using UnityEngine;

public class DuckInteractable : MonoBehaviour, IInteractable
{
    private bool collected = false;
    private ObjectiveUI objectiveUI;

    private void Start()
    {
        objectiveUI = FindObjectOfType<ObjectiveUI>();
    }

    public bool CanInteract()
    {
        return !collected;
    }

    public bool Interact(Interactor interactor)
    {
        if (collected) return false;

        collected = true;

        if (objectiveUI != null)
            objectiveUI.AddDuck();

        Destroy(gameObject);

        return true;
    }
}
