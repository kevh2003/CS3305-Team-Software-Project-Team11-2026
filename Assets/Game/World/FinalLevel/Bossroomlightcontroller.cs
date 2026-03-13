using System.Collections;
using UnityEngine;

/// <summary>
/// Controls all lights in the boss room as well as the boss's eye lights.
/// Both sets are plain Unity Light components (point lights).
/// The boss calls SetGreen / SetRed / SetTurning / SetIdle and this script
/// handles colour transitions and flicker effects on all clients locally --
/// no NetworkBehaviour needed because the boss's NetworkVariable already
/// syncs the phase, and each client reacts via OnValueChanged.
/// </summary>
public class BossRoomLightController : MonoBehaviour
{
    private static readonly Color ForcedRoomIdleColour = new Color(0.05f, 0.12f, 0.28f);
    private static readonly Color ForcedEyeIdleColour  = new Color(0.30f, 0.55f, 1f);

    [Header("Room Lights")]
    [Tooltip("All ceiling / ambient Light components in the boss room.")]
    [SerializeField] private Light[] roomLights;

    [Header("Boss Eye Lights")]
    [Tooltip("The two (or more) point lights that represent the boss's eyes.")]
    [SerializeField] private Light[] eyeLights;

    [Header("Room Colours")]
    [SerializeField] private Color roomIdleColour    = new Color(0.05f, 0.12f, 0.28f);
    [SerializeField] private Color roomGreenColour   = new Color(0.2f, 1f, 0.2f);
    [SerializeField] private Color roomTurningColour = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color roomRedColour     = new Color(1f, 0.1f, 0.1f);

    [Header("Eye Colours")]
    [Tooltip("Eyes use a brighter tint so they glow distinctly against the room.")]
    [SerializeField] private Color eyeIdleColour    = new Color(0.30f, 0.55f, 1f);
    [SerializeField] private Color eyeGreenColour   = new Color(0f, 1f, 0f);
    [SerializeField] private Color eyeTurningColour = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color eyeRedColour     = Color.red;

    [Header("Room Intensities")]
    [SerializeField] private float roomIdleIntensity    = 0.6f;
    [SerializeField] private float roomGreenIntensity   = 1.4f;
    [SerializeField] private float roomTurningIntensity = 1.2f;
    [SerializeField] private float roomRedIntensity     = 1.6f;

    [Header("Eye Intensities")]
    [SerializeField] private float eyeIdleIntensity    = 2f;
    [SerializeField] private float eyeGreenIntensity   = 4f;
    [SerializeField] private float eyeTurningIntensity = 3f;
    [SerializeField] private float eyeRedIntensity     = 6f;

    [Header("Transition")]
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Red Light Flicker")]
    [Tooltip("Flicker both room and eye lights briefly when Red Light begins.")]
    [SerializeField] private bool  flickerOnRed    = true;
    [SerializeField] private float flickerDuration = 0.3f;
    [SerializeField] private int   flickerCount    = 4;

    private Color _roomTargetColour;
    private float _roomTargetIntensity;
    private Color _eyeTargetColour;
    private float _eyeTargetIntensity;
    private bool  _transitioning;

    private Coroutine _flickerRoutine;

    private void Awake()
    {
        roomIdleColour        = ForcedRoomIdleColour;
        eyeIdleColour         = ForcedEyeIdleColour;

        _roomTargetColour     = roomIdleColour;
        _roomTargetIntensity  = roomIdleIntensity;
        _eyeTargetColour      = eyeIdleColour;
        _eyeTargetIntensity   = eyeIdleIntensity;

        ApplyImmediate(roomIdleColour, roomIdleIntensity, eyeIdleColour, eyeIdleIntensity);
    }

    private void Update()
    {
        if (!_transitioning) return;

        bool allDone = true;

        foreach (var l in roomLights)
        {
            if (l == null) continue;
            l.color     = Color.Lerp(l.color, _roomTargetColour, transitionSpeed * Time.deltaTime);
            l.intensity = Mathf.Lerp(l.intensity, _roomTargetIntensity, transitionSpeed * Time.deltaTime);

            if ((l.color - _roomTargetColour).maxColorComponent > 0.01f ||
                Mathf.Abs(l.intensity - _roomTargetIntensity) > 0.01f)
                allDone = false;
        }

        foreach (var l in eyeLights)
        {
            if (l == null) continue;
            l.color     = Color.Lerp(l.color, _eyeTargetColour, transitionSpeed * Time.deltaTime);
            l.intensity = Mathf.Lerp(l.intensity, _eyeTargetIntensity, transitionSpeed * Time.deltaTime);

            if ((l.color - _eyeTargetColour).maxColorComponent > 0.01f ||
                Mathf.Abs(l.intensity - _eyeTargetIntensity) > 0.01f)
                allDone = false;
        }

        if (allDone)
        {
            ApplyImmediate(_roomTargetColour, _roomTargetIntensity, _eyeTargetColour, _eyeTargetIntensity);
            _transitioning = false;
        }
    }

    public void SetIdle()
    {
        StopFlicker();
        TransitionTo(roomIdleColour, roomIdleIntensity, eyeIdleColour, eyeIdleIntensity);
    }

    public void SetGreen()
    {
        StopFlicker();
        TransitionTo(roomGreenColour, roomGreenIntensity, eyeGreenColour, eyeGreenIntensity);
    }

    public void SetTurning()
    {
        StopFlicker();
        TransitionTo(roomTurningColour, roomTurningIntensity, eyeTurningColour, eyeTurningIntensity);
    }

    public void SetRed()
    {
        StopFlicker();

        if (flickerOnRed)
            _flickerRoutine = StartCoroutine(FlickerThenRed());
        else
            TransitionTo(roomRedColour, roomRedIntensity, eyeRedColour, eyeRedIntensity);
    }

    private void TransitionTo(Color roomCol, float roomInt, Color eyeCol, float eyeInt)
    {
        _roomTargetColour    = roomCol;
        _roomTargetIntensity = roomInt;
        _eyeTargetColour     = eyeCol;
        _eyeTargetIntensity  = eyeInt;
        _transitioning       = true;
    }

    private void ApplyImmediate(Color roomCol, float roomInt, Color eyeCol, float eyeInt)
    {
        foreach (var l in roomLights)
        {
            if (l == null) continue;
            l.color     = roomCol;
            l.intensity = roomInt;
        }

        foreach (var l in eyeLights)
        {
            if (l == null) continue;
            l.color     = eyeCol;
            l.intensity = eyeInt;
        }

        _transitioning = false;
    }

    private void StopFlicker()
    {
        if (_flickerRoutine != null)
        {
            StopCoroutine(_flickerRoutine);
            _flickerRoutine = null;
        }
    }

    private IEnumerator FlickerThenRed()
    {
        float interval = flickerDuration / (flickerCount * 2f);

        for (int i = 0; i < flickerCount; i++)
        {
            ApplyImmediate(Color.black, 0f, Color.black, 0f);
            yield return new WaitForSeconds(interval);
            ApplyImmediate(roomRedColour, roomRedIntensity * 0.5f,
                           eyeRedColour,  eyeRedIntensity  * 0.5f);
            yield return new WaitForSeconds(interval);
        }

        // Settle on full red
        TransitionTo(roomRedColour, roomRedIntensity, eyeRedColour, eyeRedIntensity);
        _flickerRoutine = null;
    }
}