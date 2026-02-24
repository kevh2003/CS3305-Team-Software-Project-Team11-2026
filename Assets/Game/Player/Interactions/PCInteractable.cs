using UnityEngine;

public class PCInteractable : MonoBehaviour, IInteractable
{
    private static bool assignmentSubmitted = false;

    public bool CanInteract()
    {
    
        return !assignmentSubmitted;
    }

    public bool Interact(Interactor interactor)
    {
        if (assignmentSubmitted) 
            return false;

        assignmentSubmitted = true;

        Debug.Log("Assignment submitted on correct PC!");

        ObjectiveUI ui = FindObjectOfType<ObjectiveUI>();
        if (ui != null)
        {
            ui.CompleteAssignment();
        }

        return true;
    }
}

