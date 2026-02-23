using UnityEngine;

public class TriggerTest : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TriggerTest] SOMETHING ENTERED! Name: {other.name}, Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
    }
    
    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[TriggerTest] SOMETHING EXITED! Name: {other.name}");
    }
    
    private void OnTriggerStay(Collider other)
    {
        Debug.LogWarning($"[TriggerTest] STAYING: {other.name}");
    }
}