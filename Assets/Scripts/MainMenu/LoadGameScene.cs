using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadGameScene : MonoBehaviour
{
    // Call this from your UI button's OnClick in the Inspector
    public void LoadGame()
    {
        SceneManager.LoadScene("Game");
    }

    // Optional: for quitting the game (works in builds, not in editor)
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit game called"); // shows in editor
    }
}