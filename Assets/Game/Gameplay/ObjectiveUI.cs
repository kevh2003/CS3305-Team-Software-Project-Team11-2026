using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ObjectiveUI : MonoBehaviour
{
    [Header("Ducks Objective")]
    [SerializeField] private Toggle ducksToggle;
    [SerializeField] private TMP_Text ducksLabel;
    [SerializeField] private TMP_Text ducksCountText;

    [Header("Assignment Objective")]
    [SerializeField] private Toggle assignmentToggle;
    [SerializeField] private TMP_Text assignmentLabel;


    [Header("Ducks Settings")]
    [SerializeField] private int ducksTotal = 6;

    private int ducksFound = 0;
    private bool completed = false;
    private bool assignmentComplete = false;

    [SerializeField] private GameObject ducksObjectiveRoot;
    [SerializeField] private GameObject assignmentObjectiveRoot;


    private void Awake()
    {
        if (ducksToggle) ducksToggle.interactable = false;
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";

        if (assignmentToggle) assignmentToggle.interactable = false;
        if (assignmentLabel) assignmentLabel.text = "Submit an Assignment in the correct lab";


        Refresh();
    }

    private void Refresh()
    {
        bool ducksComplete = ducksFound >= ducksTotal;

        if (ducksToggle)
            ducksToggle.isOn = ducksComplete;

        if (ducksCountText)
            ducksCountText.text = ducksComplete ? $"{ducksTotal}/{ducksTotal}" : $"{ducksFound}/{ducksTotal}";

        if (assignmentToggle)
            assignmentToggle.isOn = assignmentComplete;

    }

    public void AddDuck()
    {
        if (completed) return;

        ducksFound++;
        Refresh();

        if (ducksFound >= ducksTotal)
        {
            completed = true;
            StartCoroutine(HideAfterDelay());
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (ducksObjectiveRoot != null)
            ducksObjectiveRoot.SetActive(false);

        CheckHidePanel();
    }

    public void CompleteAssignment()
    {
        assignmentComplete = true;
        Refresh();
        StartCoroutine(HideAssignmentRoutine());
    }

    private IEnumerator HideAssignmentRoutine()
    {
        yield return new WaitForSeconds(2f);

        if (assignmentObjectiveRoot != null)
            assignmentObjectiveRoot.SetActive(false);

        CheckHidePanel();

    }

    private void CheckHidePanel()
    {
        bool ducksHidden = ducksObjectiveRoot == null || !ducksObjectiveRoot.activeSelf;
        bool assignmentHidden = assignmentObjectiveRoot == null || !assignmentObjectiveRoot.activeSelf;

        if (ducksHidden && assignmentHidden)
        {
            gameObject.SetActive(false);
        }
    }



}
