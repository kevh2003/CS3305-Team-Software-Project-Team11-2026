using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetAutoReturnToMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";

    private void OnEnable()
    {
        if (Services.NetSession != null)
            Services.NetSession.OnDisconnected += HandleDisconnected;
    }

    private void OnDisable()
    {
        if (Services.NetSession != null)
            Services.NetSession.OnDisconnected -= HandleDisconnected;
    }

    private void HandleDisconnected(string reason)
    {
        // If already in menu, do nothing
        if (SceneManager.GetActiveScene().name == mainMenuSceneName)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}