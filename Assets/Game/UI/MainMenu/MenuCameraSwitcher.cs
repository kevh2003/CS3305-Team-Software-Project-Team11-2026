using UnityEngine;

public sealed class MenuCameraSwitcher : MonoBehaviour
{
    [SerializeField] GameObject[] cameras;
    [Min(0.1f)] [SerializeField] float delay = 6f;

    int index;

    void OnEnable()
    {
        if (cameras == null || cameras.Length == 0) return;
        SetCamera(0);
        InvokeRepeating(nameof(NextCamera), delay, delay);
    }

    void OnDisable() => CancelInvoke(nameof(NextCamera));

    void NextCamera() => SetCamera((index + 1) % cameras.Length);

    void SetCamera(int newIndex)
    {
        index = newIndex;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null)
                cameras[i].SetActive(i == index);
    }
}