using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Music Tracks")]
    public AudioClip menuMusic;
    public AudioClip lobbyMusic;
    public AudioClip gameplayMusic;
    public AudioClip finalLevelMusic;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";
    [SerializeField] private string lobbySceneName = "02_Lobby";
    [SerializeField] private string gameSceneName = "03_Game";

    [Header("Transition")]
    [SerializeField, Min(0f)] private float trackFadeSeconds = 0.35f;

    private AudioSource audioSource;
    private Coroutine transitionRoutine;
    private float baseVolume = 1f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            baseVolume = Mathf.Max(0f, audioSource.volume);
            audioSource.playOnAwake = false;
            audioSource.Stop();
            audioSource.clip = null;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            PlayMusic(menuMusic != null ? menuMusic : lobbyMusic);
        }
        else if (scene.name == lobbySceneName)
        {
            PlayMusic(lobbyMusic);
        }
        else if (scene.name == gameSceneName)
        {
            PlayMusic(gameplayMusic);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (audioSource == null) return;

        // Keep continuous playback when the selected clip is already active.
        if (audioSource.clip == clip && audioSource.isPlaying) return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (trackFadeSeconds <= 0f || !audioSource.isPlaying)
        {
            audioSource.clip = clip;
            audioSource.volume = baseVolume;
            if (clip != null) audioSource.Play();
            return;
        }

        transitionRoutine = StartCoroutine(FadeToClip(clip));
    }

    public void PlayFinalLevelMusic()
    {
        PlayMusic(finalLevelMusic);
    }

    private IEnumerator FadeToClip(AudioClip nextClip)
    {
        float startVolume = audioSource.volume;
        float fadeDuration = Mathf.Max(0.001f, trackFadeSeconds);

        // Fade out current track.
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / fadeDuration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, p);
            yield return null;
        }
        audioSource.volume = 0f;

        audioSource.Stop();
        audioSource.clip = nextClip;

        if (nextClip == null)
        {
            transitionRoutine = null;
            yield break;
        }

        audioSource.Play();

        // Fade in next track.
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / fadeDuration;
            audioSource.volume = Mathf.Lerp(0f, baseVolume, p);
            yield return null;
        }
        audioSource.volume = baseVolume;
        transitionRoutine = null;
    }
}