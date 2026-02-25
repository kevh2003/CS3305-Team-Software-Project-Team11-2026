using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Unity.Netcode;

public class ObjectiveUI : MonoBehaviour
{
    [Header("Ducks Objective")]
    [SerializeField] private Toggle ducksToggle;
    [SerializeField] private TMP_Text ducksLabel;
    [SerializeField] private TMP_Text ducksCountText;

    [Header("Assignment Objective")]
    [SerializeField] private Toggle assignmentToggle;
    [SerializeField] private TMP_Text assignmentLabel;

    [SerializeField] private GameObject ducksObjectiveRoot;
    [SerializeField] private GameObject assignmentObjectiveRoot;

    private ObjectiveState state;
    private bool ducksCompleted;

    private void Awake()
    {
        if (ducksToggle) ducksToggle.interactable = false;
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";

        if (assignmentToggle) assignmentToggle.interactable = false;
        if (assignmentLabel) assignmentLabel.text = "Submit an Assignment in the correct lab";
    }

    private void Start()
    {
        // Find the ObjectiveState in scene (NetworkObject scene-spawned)
        state = ObjectiveState.Instance != null ? ObjectiveState.Instance : FindFirstObjectByType<ObjectiveState>();

        if (state != null)
        {
            state.DucksFound.OnValueChanged += OnDucksChanged;
            // force initial refresh
            OnDucksChanged(0, state.DucksFound.Value);
        }
    }

    private void OnDestroy()
    {
        if (state != null)
            state.DucksFound.OnValueChanged -= OnDucksChanged;
    }

    private void OnDucksChanged(int oldValue, int newValue)
    {
        int total = state != null ? state.DucksTotal : 6;

        bool complete = newValue >= total;

        if (ducksToggle) ducksToggle.isOn = complete;
        if (ducksCountText) ducksCountText.text = complete ? $"{total}/{total}" : $"{newValue}/{total}";

        if (!ducksCompleted && complete)
        {
            ducksCompleted = true;
            StartCoroutine(HideDucksAfterDelay());
        }
    }

    private IEnumerator HideDucksAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (ducksObjectiveRoot != null)
            ducksObjectiveRoot.SetActive(false);

        CheckHidePanel();
    }

    // Left assignment logic as is (this needs to be networked later) - kev
    public void CompleteAssignment()
    {
        if (assignmentToggle) assignmentToggle.isOn = true;
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
            gameObject.SetActive(false);
    }
}