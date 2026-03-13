using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

// Binds objective toggles/counts to the replicated ObjectiveState values.
public class ObjectiveUI : MonoBehaviour
{
    private const string UnlockSecurityOfficeText = "Unlock the security office";
    private const string InvestigateSecurityOfficeText = "Investigate the security office";

    [Header("Ducks Objective")]
    [SerializeField] private Toggle ducksToggle;
    [SerializeField] private TMP_Text ducksLabel;
    [SerializeField] private TMP_Text ducksCountText;
    [SerializeField] private GameObject ducksObjectiveRoot;

    [Header("WiFi Objective")]
    [SerializeField] private Toggle wifiToggle;
    [SerializeField] private TMP_Text wifiLabel;
    [SerializeField] private TMP_Text wifiCountText;
    [SerializeField] private GameObject wifiObjectiveRoot;

    [Header("Assignment Objective")]
    [SerializeField] private Toggle assignmentToggle;
    [SerializeField] private TMP_Text assignmentLabel;
    [SerializeField] private TMP_Text assignmentCountText;
    [SerializeField] private GameObject assignmentObjectiveRoot;

    [Header("Assignment Count Layout")]
    [SerializeField] private bool assignmentCountAlignLeft = true;
    [SerializeField] private float assignmentCountPreferredWidth = 38f;

    [Header("Key Objective")]
    [SerializeField] private Toggle keyToggle;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private GameObject keyObjectiveRoot;

    [Header("Security Door Objective")]
    [SerializeField] private Toggle securityToggle;
    [SerializeField] private TMP_Text securityLabel;
    [SerializeField] private GameObject securityObjectiveRoot;

    [Header("Pressure Plates Objective")]
    [SerializeField] private Toggle platesToggle;
    [SerializeField] private TMP_Text platesLabel;
    [SerializeField] private TMP_Text platesCountText;
    [SerializeField] private GameObject platesObjectiveRoot;

    [Header("Button 2 Timer Objective")]
    [SerializeField] private Toggle timerToggle;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text timerCountText;
    [SerializeField] private GameObject timerObjectiveRoot;

    [Header("Elevator Objective")]
    [SerializeField] private Toggle elevatorToggle;
    [SerializeField] private TMP_Text elevatorLabel;
    [SerializeField] private GameObject elevatorObjectiveRoot;

    [Header("Grades Objective")]
    [SerializeField] private Toggle gradesToggle;
    [SerializeField] private TMP_Text gradesLabel;
    [SerializeField] private GameObject gradesObjectiveRoot;

    private ObjectiveState state;
    private SecurityRoomController security;

    private NetworkVariable<int>.OnValueChangedDelegate _onIntChanged;
    private NetworkVariable<bool>.OnValueChangedDelegate _onBoolChanged;

    private void Awake()
    {
        // toggles read-only
        SetToggleReadOnly(ducksToggle);
        SetToggleReadOnly(wifiToggle);
        SetToggleReadOnly(assignmentToggle);
        SetToggleReadOnly(keyToggle);
        SetToggleReadOnly(securityToggle);
        SetToggleReadOnly(platesToggle);
        SetToggleReadOnly(timerToggle);
        SetToggleReadOnly(elevatorToggle);
        SetToggleReadOnly(gradesToggle);

        // static labels
        if (ducksLabel) ducksLabel.text = "Find rubber ducks";
        if (wifiLabel) wifiLabel.text = "Fix WiFi routers";
        if (assignmentLabel) assignmentLabel.text = "Submit an assignment in Room 1.10";
        if (keyLabel) keyLabel.text = "Find the security office key";
        if (securityLabel) securityLabel.text = UnlockSecurityOfficeText;
        if (platesLabel) platesLabel.text = "Activate the pressure plates";
        if (timerLabel) timerLabel.text = "Press the yellow button";
        if (elevatorLabel) elevatorLabel.text = "Open the elevator doors";
        if (gradesLabel) gradesLabel.text = "Change your grades";

        ConfigureAssignmentCountLayout();
    }

    private void Start()
    {
        state = ObjectiveState.Instance != null ? ObjectiveState.Instance : FindFirstObjectByType<ObjectiveState>();
        security = FindFirstObjectByType<SecurityRoomController>();

        // default visibility
        SetActiveSafe(ducksObjectiveRoot, true);
        SetActiveSafe(wifiObjectiveRoot, true);
        SetActiveSafe(assignmentObjectiveRoot, false);

        SetActiveSafe(keyObjectiveRoot, false);
        SetActiveSafe(securityObjectiveRoot, false);
        SetActiveSafe(platesObjectiveRoot, false);
        SetActiveSafe(timerObjectiveRoot, false);
        SetActiveSafe(elevatorObjectiveRoot, false);
        SetActiveSafe(gradesObjectiveRoot, false);

        if (state == null)
        {
            Debug.LogWarning("ObjectiveUI: Could not find ObjectiveState in scene.");
            return;
        }

        // Create delegates ONCE and reuse them
        _onIntChanged ??= OnAnyIntChanged;
        _onBoolChanged ??= OnAnyBoolChanged;

        // subscribe to relevant ObjectiveState vars
        state.DucksFound.OnValueChanged += _onIntChanged;
        state.WifiFixedCount.OnValueChanged += _onIntChanged;
        state.CurrentSubmitCount.OnValueChanged += _onIntChanged;
        state.RequiredSubmitCount.OnValueChanged += _onIntChanged;

        state.KeySpawned.OnValueChanged += _onBoolChanged;
        state.KeyCollected.OnValueChanged += _onBoolChanged;
        state.SecurityDoorUnlocked.OnValueChanged += _onBoolChanged;
        state.ElevatorOpened.OnValueChanged += _onBoolChanged;
        state.GradesChanged.OnValueChanged += _onBoolChanged;

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (state == null) return;

        // unsubscribe with the SAME delegate instances
        if (_onIntChanged != null)
        {
            state.DucksFound.OnValueChanged -= _onIntChanged;
            state.WifiFixedCount.OnValueChanged -= _onIntChanged;
            state.CurrentSubmitCount.OnValueChanged -= _onIntChanged;
            state.RequiredSubmitCount.OnValueChanged -= _onIntChanged;
        }

        if (_onBoolChanged != null)
        {
            state.KeySpawned.OnValueChanged -= _onBoolChanged;
            state.KeyCollected.OnValueChanged -= _onBoolChanged;
            state.SecurityDoorUnlocked.OnValueChanged -= _onBoolChanged;
            state.ElevatorOpened.OnValueChanged -= _onBoolChanged;
            state.GradesChanged.OnValueChanged -= _onBoolChanged;
        }
    }

    private void Update()
    {
        if (state == null) return;

        bool shouldPoll =
            state.SecurityDoorUnlocked.Value || state.ElevatorOpened.Value || state.GradesChanged.Value ||
            (state.KeySpawned.Value && !state.KeyCollected.Value);

        if (shouldPoll)
        {
            if (security == null)
                security = FindFirstObjectByType<SecurityRoomController>();

            RefreshSecurityDoorUI();
            RefreshPlatesUI();
            RefreshTimerUI();
            RefreshElevatorUI();
        }
    }

    private void OnAnyIntChanged(int oldValue, int newValue) => RefreshAll();
    private void OnAnyBoolChanged(bool oldValue, bool newValue) => RefreshAll();

    private void RefreshAll()
    {
        if (state == null) return;

        if (security == null)
            security = FindFirstObjectByType<SecurityRoomController>();

        RefreshDucksUI();
        RefreshWifiUI();
        RefreshAssignmentUI();
        RefreshKeyUI();
        RefreshSecurityDoorUI();
        RefreshPlatesUI();
        RefreshTimerUI();
        RefreshElevatorUI();
        RefreshGradesUI();
    }

    // -------------------------
    // Pre-key
    // -------------------------

    private void RefreshDucksUI()
    {
        int total = state.DucksTotal;
        int found = state.DucksFound.Value;
        bool complete = found >= total;

        if (ducksToggle) ducksToggle.isOn = complete;
        if (ducksCountText) ducksCountText.text = complete ? $"{total}/{total}" : $"{found}/{total}";

        SetActiveSafe(ducksObjectiveRoot, !complete);
    }

    private void RefreshWifiUI()
    {
        int total = state.WifiTotal;
        int fixedCount = state.WifiFixedCount.Value;
        bool complete = total <= 0 || fixedCount >= total;

        if (wifiToggle) wifiToggle.isOn = complete;
        if (wifiCountText)
            wifiCountText.text = total > 0
                ? (complete ? $"{total}/{total}" : $"{fixedCount}/{total}")
                : "";

        SetActiveSafe(wifiObjectiveRoot, !complete);
    }

    private void RefreshAssignmentUI()
    {
        bool wifiComplete = state.IsWifiObjectiveCompleteClient();
        if (!wifiComplete)
        {
            if (assignmentToggle) assignmentToggle.isOn = false;
            if (assignmentCountText) assignmentCountText.text = "";
            SetActiveSafe(assignmentObjectiveRoot, false);
            return;
        }

        int submitted = state.CurrentSubmitCount.Value;
        int required = state.RequiredSubmitCount.Value;

        bool activeRound = required > 0;
        bool complete = activeRound && submitted >= required;

        if (assignmentToggle) assignmentToggle.isOn = complete;

        if (assignmentCountText)
        {
            assignmentCountText.text = activeRound
                ? (complete ? $"{required}/{required}" : $"{submitted}/{required}")
                : "";
        }

        bool show = activeRound && !complete;
        SetActiveSafe(assignmentObjectiveRoot, show);

        if (assignmentObjectiveRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(assignmentObjectiveRoot.GetComponent<RectTransform>());
    }

    // -------------------------
    // Post-key chain
    // -------------------------

    private void RefreshKeyUI()
    {
        // show only after pre-key is done and key has spawned, until collected
        bool preKeyDone = state.ArePreKeyObjectivesCompleteClient();

        bool show = preKeyDone && state.KeySpawned.Value && !state.KeyCollected.Value;
        SetActiveSafe(keyObjectiveRoot, show);

        if (keyToggle) keyToggle.isOn = state.KeyCollected.Value;
    }

    private void RefreshSecurityDoorUI()
    {
        // show only after key collected
        if (!state.KeyCollected.Value)
        {
            if (securityToggle) securityToggle.isOn = false;
            if (securityLabel) securityLabel.text = UnlockSecurityOfficeText;
            SetActiveSafe(securityObjectiveRoot, false);
            return;
        }

        // Step 1: unlock the door
        if (!state.SecurityDoorUnlocked.Value)
        {
            if (securityLabel) securityLabel.text = UnlockSecurityOfficeText;
            if (securityToggle) securityToggle.isOn = false;
            SetActiveSafe(securityObjectiveRoot, true);
            return;
        }

        // Step 2: investigate office (until button 1 starts plate phase)
        bool firstButtonPressed = security != null && security.Stage >= 1;
        if (!firstButtonPressed)
        {
            if (securityLabel) securityLabel.text = InvestigateSecurityOfficeText;
            if (securityToggle) securityToggle.isOn = false;
            SetActiveSafe(securityObjectiveRoot, true);
            return;
        }

        if (securityToggle) securityToggle.isOn = true;
        if (securityLabel) securityLabel.text = UnlockSecurityOfficeText;
        SetActiveSafe(securityObjectiveRoot, false);
    }

    private void RefreshPlatesUI()
    {
        // show after door unlocked, while puzzle is in plate stages
        if (!state.SecurityDoorUnlocked.Value || state.ElevatorOpened.Value)
        {
            SetActiveSafe(platesObjectiveRoot, false);
            return;
        }

        if (security == null)
        {
            SetActiveSafe(platesObjectiveRoot, true);
            if (platesCountText) platesCountText.text = "";
            if (platesToggle) platesToggle.isOn = false;
            return;
        }

        bool inPlatePhase = (security.Stage == 1 || security.Stage == 2);
        SetActiveSafe(platesObjectiveRoot, inPlatePhase);

        if (!inPlatePhase) return;

        int active = security.ActivePlates;
        int required = security.RequiredPlates;
        bool complete = required > 0 && active >= required;

        if (platesToggle) platesToggle.isOn = complete;
        if (platesCountText) platesCountText.text = $"{active}/{required}";
    }

    private void RefreshTimerUI()
    {
        // stage 2 = 1-minute window running
        if (!state.SecurityDoorUnlocked.Value || state.ElevatorOpened.Value || security == null)
        {
            SetActiveSafe(timerObjectiveRoot, false);
            return;
        }

        bool show = (security.Stage == 2);
        SetActiveSafe(timerObjectiveRoot, show);

        if (!show) return;

        float remain = security.GetSecondsRemaining();
        int secs = Mathf.CeilToInt(remain);

        if (timerToggle) timerToggle.isOn = false;

        if (timerCountText != null)
        {
            int m = secs / 60;
            int s = secs % 60;
            timerCountText.text = $"{m:00}:{s:00}";
        }
    }

    private void RefreshElevatorUI()
    {
        // stage 3 = button3 enabled, until elevator opened
        if (!state.SecurityDoorUnlocked.Value || state.ElevatorOpened.Value || security == null)
        {
            SetActiveSafe(elevatorObjectiveRoot, false);
            return;
        }

        bool show = (security.Stage == 3);
        SetActiveSafe(elevatorObjectiveRoot, show);

        if (elevatorToggle) elevatorToggle.isOn = state.ElevatorOpened.Value;
    }

    private void RefreshGradesUI()
    {
        // show after elevator opened, until grades changed
        if (!state.ElevatorOpened.Value)
        {
            SetActiveSafe(gradesObjectiveRoot, false);
            return;
        }

        bool complete = state.GradesChanged.Value;
        SetActiveSafe(gradesObjectiveRoot, !complete);
        if (gradesToggle) gradesToggle.isOn = complete;
    }

    // -------------------------
    // helpers
    // -------------------------

    private static void SetToggleReadOnly(Toggle t)
    {
        if (t != null) t.interactable = false;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    private void ConfigureAssignmentCountLayout()
    {
        if (assignmentCountText == null) return;

        if (assignmentCountAlignLeft)
            assignmentCountText.horizontalAlignment = HorizontalAlignmentOptions.Left;

        if (assignmentCountPreferredWidth > 0f)
        {
            var le = assignmentCountText.GetComponent<LayoutElement>();
            if (le != null)
                le.preferredWidth = assignmentCountPreferredWidth;
        }
    }
}