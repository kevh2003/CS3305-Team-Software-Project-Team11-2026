using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class ElevatorRider : NetworkBehaviour
{
    private CharacterController _cc;
    private Transform _platform;
    private Vector3 _lastPlatformPos;
    private Quaternion _lastPlatformRot;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (!IsOwner) return; // only move the local player
        if (_platform == null) return;
        if (_cc == null || !_cc.enabled) return;

        Vector3 posDelta = _platform.position - _lastPlatformPos;

        if (posDelta.sqrMagnitude > 0f)
            _cc.Move(posDelta);

        _lastPlatformPos = _platform.position;
        _lastPlatformRot = _platform.rotation;
    }

    public void SetPlatform(Transform platform)
    {
        _platform = platform;
        if (_platform != null)
        {
            _lastPlatformPos = _platform.position;
            _lastPlatformRot = _platform.rotation;
        }
    }

    public void ClearPlatform(Transform platform)
    {
        if (_platform == platform)
            _platform = null;
    }
}