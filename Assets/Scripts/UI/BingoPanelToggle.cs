using UnityEngine;
using UnityEngine.InputSystem;

public class BingoPanelToggle : MonoBehaviour
{
    [Header("Assign your Bingo Panel here")]
    public GameObject bingoPanel;

    private bool isVisible = false;

    void Update()
    {
        // Check if TAB was pressed (using new Input System)
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isVisible = !isVisible;
            if (bingoPanel != null)
                bingoPanel.SetActive(isVisible);
        }
    }
}