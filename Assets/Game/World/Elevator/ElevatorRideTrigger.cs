using UnityEngine;

public class ElevatorRideTrigger : MonoBehaviour
{
    [SerializeField] private Transform platformRoot;

    private void Awake()
    {
        if (platformRoot == null)
            platformRoot = transform.root;
    }

    private void OnTriggerEnter(Collider other)
    {
        var rider = other.GetComponentInParent<ElevatorRider>();
        if (rider == null) return;

        rider.SetPlatform(platformRoot);
    }

    private void OnTriggerExit(Collider other)
    {
        var rider = other.GetComponentInParent<ElevatorRider>();
        if (rider == null) return;

        rider.ClearPlatform(platformRoot);
    }
}