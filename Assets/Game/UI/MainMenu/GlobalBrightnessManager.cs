using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class GlobalBrightnessManager : MonoBehaviour
{
    private const string PrefBrightness = "settings_brightness";
    private const float MinBrightness = -2.0f;
    private const float MaxBrightness = 2.0f;
    private const float DefaultBrightness = 0f;

    private static GlobalBrightnessManager instance;

    private Volume volume;
    private ColorAdjustments colorAdjustments;
    private float currentBrightness = DefaultBrightness;
    private int volumeLayerBit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static float LoadSavedBrightness()
    {
        return Mathf.Clamp(PlayerPrefs.GetFloat(PrefBrightness, DefaultBrightness), MinBrightness, MaxBrightness);
    }

    public static float GetCurrentBrightness()
    {
        return EnsureInstance().currentBrightness;
    }

    public static void SetBrightness(float value, bool saveToPrefs)
    {
        EnsureInstance().SetBrightnessInternal(value, saveToPrefs);
    }

    private static GlobalBrightnessManager EnsureInstance()
    {
        if (instance != null) return instance;

        var go = new GameObject("GlobalBrightnessManager");
        instance = go.AddComponent<GlobalBrightnessManager>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeVolume();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Camera.onPreCull += HandleCameraPreCull;
        ApplyToAllCameras();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Camera.onPreCull -= HandleCameraPreCull;
    }

    private void InitializeVolume()
    {
        if (gameObject.layer == 0)
            gameObject.layer = 30;

        volumeLayerBit = 1 << gameObject.layer;

        volume = GetComponent<Volume>();
        if (volume == null)
            volume = gameObject.AddComponent<Volume>();

        volume.isGlobal = true;
        volume.priority = 100f;

        if (volume.sharedProfile == null)
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (!volume.sharedProfile.TryGet(out colorAdjustments))
            colorAdjustments = volume.sharedProfile.Add<ColorAdjustments>(true);

        currentBrightness = LoadSavedBrightness();
        ApplyBrightnessToVolume(currentBrightness);
    }

    private void SetBrightnessInternal(float value, bool saveToPrefs)
    {
        currentBrightness = Mathf.Clamp(value, MinBrightness, MaxBrightness);
        ApplyBrightnessToVolume(currentBrightness);

        if (!saveToPrefs) return;

        PlayerPrefs.SetFloat(PrefBrightness, currentBrightness);
        PlayerPrefs.Save();
    }

    private void ApplyBrightnessToVolume(float brightness)
    {
        if (colorAdjustments == null) return;
        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = brightness;
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        ApplyToAllCameras();
    }

    private void HandleCameraPreCull(Camera camera)
    {
        ApplyToCamera(camera);
    }

    private void ApplyToAllCameras()
    {
#if UNITY_2023_1_OR_NEWER
        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var cameras = FindObjectsOfType<Camera>(true);
#endif
        for (int i = 0; i < cameras.Length; i++)
            ApplyToCamera(cameras[i]);
    }

    private void ApplyToCamera(Camera camera)
    {
        if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            return;

        if (!cameraData.renderPostProcessing)
            cameraData.renderPostProcessing = true;

        int currentMask = cameraData.volumeLayerMask.value;
        if ((currentMask & volumeLayerBit) == 0)
            cameraData.volumeLayerMask = currentMask | volumeLayerBit;
    }
}