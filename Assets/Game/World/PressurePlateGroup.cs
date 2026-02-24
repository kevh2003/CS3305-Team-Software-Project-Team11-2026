using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class PressurePlateGroup : NetworkBehaviour
{
    [Header("Plate Group Settings")]
    [SerializeField] private string groupName = "PuzzleGroup1";
    [SerializeField] private PressurePlate[] plates;
    [SerializeField] private bool requireAllPlates = true;
    
    [Header("Timeout Settings (Optional)")]
    [SerializeField] private bool useTimeout = false;
    [SerializeField] private float timeoutSeconds = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private NetworkVariable<bool> isPuzzleSolved = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private float allPlatesActiveTime = 0f;
    private bool wasAllPlatesActive = false;
    
    public bool IsPuzzleSolved => isPuzzleSolved.Value;
    public string GroupName => groupName;
    
    private void Start()
    {
        if (plates == null || plates.Length == 0)
        {
            plates = GetComponentsInChildren<PressurePlate>();
            
        }
        
        foreach (var plate in plates)
        {
            if (plate != null)
            {
                plate.RegisterWithGroup(this);
            }
        }
        
        isPuzzleSolved.OnValueChanged += OnPuzzleSolvedChanged;
        
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        isPuzzleSolved.OnValueChanged -= OnPuzzleSolvedChanged;
    }
    
    private void Update()
    {
        if (!IsServer || isPuzzleSolved.Value || !useTimeout) return;
        
        bool allActive = AreAllPlatesActivated();
        
        if (allActive)
        {
            if (!wasAllPlatesActive)
            {
                allPlatesActiveTime = 0f;
                wasAllPlatesActive = true;
                
            }
            
            allPlatesActiveTime += Time.deltaTime;
            
            if (allPlatesActiveTime >= timeoutSeconds)
            {
                SolvePuzzle();
            }
        }
        else
        {
            
            wasAllPlatesActive = false;
            allPlatesActiveTime = 0f;
        }
    }
    
    public void OnPlateStateChanged(PressurePlate plate, bool activated)
    {
        if (!IsServer) return;
        
        
        if (!useTimeout)
        {
            if (AreAllPlatesActivated() && !isPuzzleSolved.Value)
            {
                SolvePuzzle();
            }
            else if (!AreAllPlatesActivated() && isPuzzleSolved.Value)
            {
                ResetPuzzle();
            }
        }
    }
    
    private bool AreAllPlatesActivated()
    {
        if (plates == null || plates.Length == 0) return false;
        
        return requireAllPlates ? plates.All(p => p.IsActivated) : plates.Any(p => p.IsActivated);
    }
    
    private void SolvePuzzle()
    {
        if (isPuzzleSolved.Value) return;
        
        isPuzzleSolved.Value = true;
        Debug.Log("Puzzle complete");
    }
    
    private void ResetPuzzle()
    {
        if (!isPuzzleSolved.Value) return;
        
        isPuzzleSolved.Value = false;
        allPlatesActiveTime = 0f;
        wasAllPlatesActive = false;
    }
    
    private void OnPuzzleSolvedChanged(bool previousValue, bool newValue)
    {
        // Callback when puzzle state changes on clients
    }
    
    public int GetActivatedPlateCount()
    {
        return plates.Count(p => p.IsActivated);
    }
    
    public int GetTotalPlateCount()
    {
        return plates.Length;
    }
}