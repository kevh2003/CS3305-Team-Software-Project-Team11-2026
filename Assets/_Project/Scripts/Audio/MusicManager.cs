using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Music Tracks")]
    public AudioClip lobbyMusic;
    public AudioClip gameplayMusic;
    public AudioClip finalLevelMusic;

    private AudioSource audioSource;

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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "02_Lobby")
        {
            PlayMusic(lobbyMusic);
        }
        else if (scene.name == "03_Game")
        {
            PlayMusic(gameplayMusic);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (audioSource.clip == clip) return;

        audioSource.clip = clip;
        audioSource.Play();
    }

    public void PlayFinalLevelMusic()
    {
        PlayMusic(finalLevelMusic);
    }
}