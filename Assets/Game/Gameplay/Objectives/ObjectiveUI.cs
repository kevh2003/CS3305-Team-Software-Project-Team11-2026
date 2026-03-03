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
    [SerializeField] private TMP_Text assignmentCountText;

    [SerializeField] private GameObject ducksObjectiveRoot;
    [SerializeField] private GameObject assignmentObjectiveRoot;

    private ObjectiveState state;
    private bool ducksCompleted;
    private bool assignmentCompleted;

    private void Awake()
    {
        if (ducksToggle) ducksToggle.interactable = false;
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";

        if (assignmentToggle) assignmentToggle.interactable = false;
        if (assignmentLabel) assignmentLabel.text = "Submit an assignment in Room 1.10";
    }

    private void Start()
    {
        // Find the ObjectiveState in scene (NetworkObject scene-spawned)
        state = ObjectiveState.Instance != null ? ObjectiveState.Instance : FindFirstObjectByType<ObjectiveState>();
        ducksCompleted = false;
        assignmentCompleted = false;

        if (ducksObjectiveRoot != null) ducksObjectiveRoot.SetActive(true);
        if (assignmentObjectiveRoot != null) assignmentObjectiveRoot.SetActive(true);
        gameObject.SetActive(true);

        if (state != null)
        {
            // Ducks
            state.DucksFound.OnValueChanged += OnDucksChanged;
            OnDucksChanged(0, state.DucksFound.Value);

            // Assignment
            state.CurrentSubmitCount.OnValueChanged += OnAssignmentChanged;
            state.RequiredSubmitCount.OnValueChanged += OnAssignmentChanged;
            RefreshAssignmentUI();
        }
        else
        {
            Debug.LogWarning("ObjectiveUI: Could not find ObjectiveState in scene.");
        }
    }

    private void OnDestroy()
    {
        if (state == null) return;

        state.DucksFound.OnValueChanged -= OnDucksChanged;

        // Assignment
        state.CurrentSubmitCount.OnValueChanged -= OnAssignmentChanged;
        state.RequiredSubmitCount.OnValueChanged -= OnAssignmentChanged;
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

    // Assignment handler: supports both CurrentSubmitCount and RequiredSubmitCount changes
    private void OnAssignmentChanged(int oldValue, int newValue)
    {
        RefreshAssignmentUI();
    }

    private void RefreshAssignmentUI()
    {
        if (state == null) return;

        int submitted = state.CurrentSubmitCount.Value;
        int required = state.RequiredSubmitCount.Value;

        bool complete = (required > 0 && submitted >= required);

        if (assignmentToggle) assignmentToggle.isOn = complete;

        if (assignmentLabel)
        {
            if (required <= 0)
            {
                assignmentLabel.text = "Submit an assignment in Room 1.10";
                if (assignmentCountText != null) assignmentCountText.text = "";
                return;
            }
            else {
                assignmentLabel.text = $"Submit an assignment in Room 1.10 (everyone must submit once)";
            }
        }

        if (assignmentCountText != null && required > 0)
            assignmentCountText.text = complete ? $"{required}/{required}" : $"{submitted}/{required}";

        if (!assignmentCompleted && complete)
        {
            assignmentCompleted = true;
            StartCoroutine(HideAssignmentAfterDelay());
        }
    }

    private IEnumerator HideAssignmentAfterDelay()
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