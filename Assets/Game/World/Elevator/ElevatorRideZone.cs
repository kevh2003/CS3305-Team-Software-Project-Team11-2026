using UnityEngine;

public class ElevatorRideZone : MonoBehaviour
{
    [Tooltip("Set to elevator root")]
    [SerializeField] private Transform platformRoot;

    private void Awake()
    {
        if (platformRoot == null)
            platformRoot = transform.root;
    }

    private void OnTriggerEnter(Collider other)
    {
        // CharacterController lives on the player root
        var cc = other.GetComponent<CharacterController>();
        if (cc == null)
            cc = other.GetComponentInParent<CharacterController>();

        if (cc == null) return;

        // Parent the player root to the platform so they move with it
        cc.transform.SetParent(platformRoot, true);
    }

    private void OnTriggerExit(Collider other)
    {
        var cc = other.GetComponent<CharacterController>();
        if (cc == null)
            cc = other.GetComponentInParent<CharacterController>();

        if (cc == null) return;

        // Unparent
        if (cc.transform.parent == platformRoot)
            cc.transform.SetParent(null, true);
    }
}