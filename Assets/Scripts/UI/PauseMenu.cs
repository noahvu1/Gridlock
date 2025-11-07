using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; 

[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Drag your Pause_Panel GameObject here in the Inspector.")]
    public GameObject pausePanel;

    [Header("Cursor Behavior")]
    public bool unlockCursorOnPause = true;
    public bool lockCursorOnResume = true;

    private bool _isPaused;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false); // start hidden
        ApplyCursorState(locked: true);  // assume gameplay starts with locked cursor
    }

    void Update()
    {
        // Keyboard Esc
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();

        // Optional: Gamepad Start/Menu button
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (_isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        _isPaused = true;

        if (unlockCursorOnPause) ApplyCursorState(locked: false);
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        _isPaused = false;

        if (lockCursorOnResume) ApplyCursorState(locked: true);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f; // reset timescale before leaving
        SceneManager.LoadScene("MainMenu"); // make sure the scene name matches exactly
    }

    private void ApplyCursorState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}