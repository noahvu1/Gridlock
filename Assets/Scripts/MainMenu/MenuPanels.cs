using UnityEngine;

public class MenuPanels : MonoBehaviour
{
    // drag your Background_Panel here (the main/home panel)
    public GameObject backgroundPanel;

    // drag your Options_Panel here
    public GameObject optionsPanel;

    // call this from the Options button OnClick
    public void ShowOptions()
    {
        if (backgroundPanel) backgroundPanel.SetActive(false); // hide background
        if (optionsPanel) optionsPanel.SetActive(true);         // show options
    }

    // call this from the Back button OnClick
    public void ShowBackground()
    {
        if (optionsPanel) optionsPanel.SetActive(false);        // hide options
        if (backgroundPanel) backgroundPanel.SetActive(true);   // show background
    }
}