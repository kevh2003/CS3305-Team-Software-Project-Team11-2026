using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class LightFlicker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Light targetLight;

    [Header("Base")]
    [SerializeField] private bool captureBaseOnAwake = true;
    [SerializeField] private float baseIntensity = 1f;
    [SerializeField] private float baseRange = 10f;

    [Header("When It Flickers (seconds)")]
    [SerializeField] private Vector2 flickerInterval = new Vector2(2.5f, 6.5f);
    [SerializeField] private Vector2 flickerDuration = new Vector2(0.08f, 0.22f);
    [SerializeField] private Vector2 sampleStep = new Vector2(0.015f, 0.06f);

    [Header("How Strong")]
    [SerializeField] private Vector2 intensityMultiplier = new Vector2(0.35f, 1f);
    [SerializeField] private bool affectRange = true;
    [SerializeField] private Vector2 rangeMultiplier = new Vector2(0.75f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float blackoutChance = 0.12f;
    [SerializeField] private bool useUnscaledTime = false;

    private bool _isFlickering;
    private float _nextFlickerAt;
    private float _flickerEndsAt;
    private float _nextSampleAt;

    private void Awake()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        SanitizeRanges();

        if (targetLight != null && captureBaseOnAwake)
        {
            baseIntensity = targetLight.intensity;
            baseRange = targetLight.range;
        }
    }

    private void OnEnable()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        SanitizeRanges();
        RestoreBase();
        ScheduleNextFlicker(Now);
    }

    private void OnDisable()
    {
        RestoreBase();
        _isFlickering = false;
    }

    private void Update()
    {
        if (targetLight == null || !targetLight.enabled)
            return;

        float now = Now;

        if (!_isFlickering)
        {
            if (now >= _nextFlickerAt)
                BeginFlicker(now);
            return;
        }

        if (now >= _flickerEndsAt)
        {
            EndFlicker(now);
            return;
        }

        if (now >= _nextSampleAt)
        {
            ApplyFlickerSample();
            _nextSampleAt = now + RandomRange(sampleStep);
        }
    }

    [ContextMenu("Capture Current As Base")]
    private void CaptureCurrentAsBase()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight == null)
            return;

        baseIntensity = targetLight.intensity;
        baseRange = targetLight.range;
    }

    private void BeginFlicker(float now)
    {
        _isFlickering = true;
        _flickerEndsAt = now + RandomRange(flickerDuration);
        _nextSampleAt = now;
    }

    private void EndFlicker(float now)
    {
        _isFlickering = false;
        RestoreBase();
        ScheduleNextFlicker(now);
    }

    private void ScheduleNextFlicker(float now)
    {
        _nextFlickerAt = now + RandomRange(flickerInterval);
    }

    private void ApplyFlickerSample()
    {
        if (Random.value < blackoutChance)
        {
            targetLight.intensity = 0f;
            if (affectRange)
                targetLight.range = Mathf.Max(0f, baseRange * 0.5f);
            return;
        }

        targetLight.intensity = Mathf.Max(0f, baseIntensity * RandomRange(intensityMultiplier));

        if (affectRange)
            targetLight.range = Mathf.Max(0f, baseRange * RandomRange(rangeMultiplier));
    }

    private void RestoreBase()
    {
        if (targetLight == null)
            return;

        targetLight.intensity = Mathf.Max(0f, baseIntensity);
        targetLight.range = Mathf.Max(0f, baseRange);
    }

    private void SanitizeRanges()
    {
        flickerInterval = SortClamp(flickerInterval, 0.01f);
        flickerDuration = SortClamp(flickerDuration, 0.01f);
        sampleStep = SortClamp(sampleStep, 0.005f);
        intensityMultiplier = SortClamp(intensityMultiplier, 0f);
        rangeMultiplier = SortClamp(rangeMultiplier, 0f);
    }

    private static Vector2 SortClamp(Vector2 value, float minFloor)
    {
        float min = Mathf.Max(minFloor, Mathf.Min(value.x, value.y));
        float max = Mathf.Max(min, Mathf.Max(value.x, value.y));
        return new Vector2(min, max);
    }

    private static float RandomRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
}