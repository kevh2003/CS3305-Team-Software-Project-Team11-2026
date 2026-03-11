using UnityEngine;

public sealed class MenuCameraSwitcher : MonoBehaviour
{
    [SerializeField] GameObject[] cameras;
    [Min(0.1f)] [SerializeField] float delay = 6f;

    [Header("Sway")]
    [SerializeField] bool enableSway = true;
    [SerializeField] Vector2 swayAmplitudeEuler = new Vector2(0.4f, 0.6f); // pitch, yaw
    [SerializeField] Vector2 swayFrequencyHz = new Vector2(0.08f, 0.11f);

    const float Tau = Mathf.PI * 2f;

    int index;
    Transform activeCam;
    Quaternion baseRot;
    float phaseX, phaseY;

    void OnEnable()
    {
        if (cameras == null || cameras.Length == 0) return;
        SetCamera(0);
        InvokeRepeating(nameof(NextCamera), delay, delay);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(NextCamera));
        if (activeCam != null) activeCam.localRotation = baseRot;
    }

    void Update()
    {
        if (!enableSway || activeCam == null) return;

        float t = Time.unscaledTime;
        float pitch = Mathf.Sin(t * swayFrequencyHz.x * Tau + phaseX) * swayAmplitudeEuler.x;
        float yaw   = Mathf.Sin(t * swayFrequencyHz.y * Tau + phaseY) * swayAmplitudeEuler.y;

        activeCam.localRotation = baseRot * Quaternion.Euler(pitch, yaw, 0f);
    }

    void NextCamera() => SetCamera((index + 1) % cameras.Length);

    void SetCamera(int newIndex)
    {
        if (activeCam != null) activeCam.localRotation = baseRot;

        index = newIndex;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null)
                cameras[i].SetActive(i == index);

        if (cameras[index] != null)
        {
            activeCam = cameras[index].transform;
            baseRot = activeCam.localRotation;
            phaseX = Random.value * Tau;
            phaseY = Random.value * Tau;
        }
        else
        {
            activeCam = null;
        }
    }
}