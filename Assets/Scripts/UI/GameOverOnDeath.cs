using UnityEngine;

[DisallowMultipleComponent]
public class GameOverOnDeath : MonoBehaviour
{
    [Header("References")]
    public PlayerHealthUI healthScript;     
    public GameObject gameOverPanel;        // assign your GameOver_Panel here
    public MonoBehaviour movementScript;    // your movement script (ex: PlayerMovement)

    [Header("Cursor")]
    public bool showCursorOnDeath = true;
    public CursorLockMode lockModeOnDeath = CursorLockMode.None;

    bool _isGameOver = false;

    void Awake()
    {
        // hide panel at start
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        if (_isGameOver) return;
        if (!healthScript) return;

        if (healthScript.IsDead())
        {
            TriggerGameOver();
        }
    }

    void TriggerGameOver()
    {
        _isGameOver = true;

        // show UI
        if (gameOverPanel) gameOverPanel.SetActive(true);

        // disable movement
        if (movementScript) movementScript.enabled = false;

        // show cursor
        if (showCursorOnDeath)
        {
            Cursor.lockState = lockModeOnDeath; // usually None
            Cursor.visible = true;
        }
    }
}