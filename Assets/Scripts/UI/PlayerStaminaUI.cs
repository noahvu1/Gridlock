using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerStaminaUI : MonoBehaviour
{
    [Header("References")]
    public StaminaSprint staminaScript;  // drag your player with StaminaSprint here
    public Image staminaFillImage;       // blue fill
    public Image staminaBackground;      // grey background (optional)

    [Header("Visual Settings")]
    public bool hideWhenFull = false;    // optional: hide bar if stamina = 100%

    void Update()
    {
        if (!staminaScript || !staminaFillImage) return;

        // clamp just in case the script goes a bit over/under
        float t = Mathf.Clamp01(staminaScript.Stamina01);
        staminaFillImage.fillAmount = t; // expects Image Type = Filled (Horizontal)

        // if we don't want to hide when full, just keep everything on
        if (!hideWhenFull)
        {
            staminaFillImage.enabled = true;
            if (staminaBackground) staminaBackground.enabled = true;
            return;
        }

        // treat "full" as 99%+ so tiny float errors don't break it
        bool isFull = t >= 0.9f;

        staminaFillImage.enabled = !isFull;
        if (staminaBackground) staminaBackground.enabled = !isFull;
    }
}