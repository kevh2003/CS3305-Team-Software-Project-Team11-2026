using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ObjectiveUI : MonoBehaviour
{
    [Header("Elevator Objective")]
    [SerializeField] private Toggle elevatorToggle;
    [SerializeField] private TMP_Text elevatorLabel;

    [Header("Ducks Objective")]
    [SerializeField] private Toggle ducksToggle;
    [SerializeField] private TMP_Text ducksLabel;
    [SerializeField] private TMP_Text ducksCountText;

    [Header("Ducks Settings")]
    [SerializeField] private int ducksTotal = 6;

    private int ducksFound = 0;
    private bool elevatorComplete = false;

    private void Awake()
    {
        if (elevatorToggle) elevatorToggle.interactable = false;
        if (ducksToggle) ducksToggle.interactable = false;

        if (elevatorLabel) elevatorLabel.text = "Fix the elevator";
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";

        Refresh();
    }

    private void Refresh()
    {
        if (elevatorToggle)
            elevatorToggle.isOn = elevatorComplete;

        bool ducksComplete = ducksFound >= ducksTotal;

        if (ducksToggle)
            ducksToggle.isOn = ducksComplete;

        if (ducksCountText)
            ducksCountText.text = ducksComplete ? "✓" : $"{ducksFound}/{ducksTotal}";
    }

    public void SetElevatorComplete(bool complete)
    {
        elevatorComplete = complete;
        Refresh();
    }

    public void SetDucksFound(int found)
    {
        ducksFound = Mathf.Clamp(found, 0, ducksTotal);
        Refresh();
    }
}