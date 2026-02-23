using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PressurePlate : NetworkBehaviour
{
    [Header("Plate Settings")]
    [SerializeField] private int plateID = 0;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Visual Feedback")]
    [SerializeField] private Material activatedMaterial;
    [SerializeField] private Material deactivatedMaterial;
    [SerializeField] private MeshRenderer plateRenderer;
    [SerializeField] private float pressDepth = 0.1f;
    [SerializeField] private Color activatedColor = Color.green;
    [SerializeField] private Color deactivatedColor = Color.red;
    
    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private AudioClip deactivateSound;
    
    private NetworkVariable<bool> isActivated = new NetworkVariable<bool>(
        false, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );
    
    private Vector3 originalPosition;
    private Vector3 pressedPosition;
    private HashSet<Collider> playersOnPlate = new HashSet<Collider>();
    private PressurePlateGroup plateGroup;
    
    public int PlateID => plateID;
    public bool IsActivated => isActivated.Value;
    
    private void Awake()
    {
        
        
        originalPosition = transform.localPosition;
        pressedPosition = originalPosition - new Vector3(0, pressDepth, 0);
        
        if (plateRenderer == null)
            plateRenderer = GetComponent<MeshRenderer>();
    }
    
    private void Start()
    {
        
        
        isActivated.OnValueChanged += OnActivationChanged;
        UpdateVisuals(isActivated.Value);
        

    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        isActivated.OnValueChanged -= OnActivationChanged;
    }
    
    public void RegisterWithGroup(PressurePlateGroup group)
    {
        plateGroup = group;
        
    }
    
    private void OnTriggerEnter(Collider other)
    {
        
        if (IsPlayerCollider(other))
        {
            playersOnPlate.Add(other);
            
            
            if (playersOnPlate.Count == 1)
            {
                
                isActivated.Value = true;
                
                if (plateGroup != null)
                    plateGroup.OnPlateStateChanged(this, true);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        
        
        if (!IsServer) return;
        
        if (IsPlayerCollider(other))
        {
            playersOnPlate.Remove(other);
            
            if (playersOnPlate.Count == 0)
            {
                
                isActivated.Value = false;
                
                if (plateGroup != null)
                    plateGroup.OnPlateStateChanged(this, false);
            }
        }
    }
    
    private bool IsPlayerCollider(Collider col)
    {
        if (col.CompareTag(playerTag))
        {
            
            return true;
        }
        
        if (col.transform.parent != null && col.transform.parent.CompareTag(playerTag))
        {
            
            return true;
        }
        
        Transform root = col.transform.root;
        if (root != null && root.CompareTag(playerTag))
        {

            return true;
        }
        
        return false;
    }
    
    private void OnActivationChanged(bool previousValue, bool newValue)
    {
        
        UpdateVisuals(newValue);
        PlaySound(newValue);
    }
    
    private void UpdateVisuals(bool activated)
    {

        
        if (plateRenderer != null)
        {
            if (activated && activatedMaterial != null)
            {
                plateRenderer.material = activatedMaterial;
            }
            else if (!activated && deactivatedMaterial != null)
            {
                plateRenderer.material = deactivatedMaterial;
            }
            else
            {
                Color targetColor = activated ? activatedColor : deactivatedColor;
                plateRenderer.material.color = targetColor;
            }
        }
        
        StopAllCoroutines();
        StartCoroutine(AnimatePlate(activated ? pressedPosition : originalPosition));
    }
    
    private System.Collections.IEnumerator AnimatePlate(Vector3 targetPosition)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startPosition = transform.localPosition;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        
        transform.localPosition = targetPosition;
    }
    
    private void PlaySound(bool activated)
    {
        if (audioSource == null) return;
        
        AudioClip clipToPlay = activated ? activateSound : deactivateSound;
        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }
}
