using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHealthUI : MonoBehaviour
{
    [Header("Health")]
    [Min(1)] public int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("UI")]
    public Image healthFillImage;     // green fill image
    public Image healthBackground;    // optional background

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        RefreshUI();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        RefreshUI();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshUI();
    }

    public void SetHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (healthFillImage)
        {
            float t = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            healthFillImage.fillAmount = t; // expects Image Type = Filled, Fill Method = Horizontal
        }
    }

    public int GetHealth() => currentHealth;
    public bool IsDead() => currentHealth <= 0;

#if UNITY_EDITOR
    void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (healthFillImage && maxHealth > 0)
            healthFillImage.fillAmount = (float)currentHealth / maxHealth;
    }
#endif
}