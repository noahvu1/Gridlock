using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToMenuButton : MonoBehaviour
{
    [Header("Scene Settings")]
    public string mainMenuSceneName = "MainMenu"; // name of your menu scene

    // call this from your UI Button OnClick()
    public void GoToMainMenu()
    {
        // make sure time scale is normal again
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}