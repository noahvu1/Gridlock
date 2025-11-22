using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHealthUI : MonoBehaviour
{
    [Header("Health")]
    [Min(1)] public int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("Regen")]
    public bool useRegen = true;
    public float regenPerSecond = 5f;
    public float regenStartDelay = 1.5f;
    float _regenBlockedUntil;

    [Header("UI")]
    public Image healthFillImage;
    public Image healthBackground;

    [Header("Colors")]
    [Range(0f,1f)] public float lowThreshold = 0.5f;
    [Range(0f,1f)] public float criticalThreshold = 0.2f;
    public Color healthyColor = new Color(0.2f, 0.85f, 0.2f);
    public Color lowColor = new Color(1f, 0.9f, 0.2f);
    public Color criticalColor = new Color(1f, 0.25f, 0.25f);

    [Header("Damage Sound")]
    public AudioClip damageSound;          // drag your sound here
    public AudioSource cameraAudioSource;  // drag your main camera here

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        RefreshUI();
    }

    void Update()
    {
        // regen
        if (useRegen && !IsDead() && Time.time >= _regenBlockedUntil && currentHealth < maxHealth)
        {
            float add = regenPerSecond * Time.deltaTime;
            if (add > 0f)
            {
                currentHealth = Mathf.Min(maxHealth, Mathf.RoundToInt(currentHealth + add));
                RefreshUI();
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead()) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        // pause regen after damage
        _regenBlockedUntil = Time.time + regenStartDelay;

        // --- NEW: play damage sfx ---
        if (cameraAudioSource && damageSound)
            cameraAudioSource.PlayOneShot(damageSound);

        RefreshUI();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead()) return;
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
            healthFillImage.fillAmount = t;

            if (t <= criticalThreshold) healthFillImage.color = criticalColor;
            else if (t <= lowThreshold) healthFillImage.color = lowColor;
            else healthFillImage.color = healthyColor;
        }
    }

    public int GetHealth() => currentHealth;
    public bool IsDead() => currentHealth <= 0;

#if UNITY_EDITOR
    void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (healthFillImage && maxHealth > 0)
        {
            float t = (float)currentHealth / maxHealth;
            healthFillImage.fillAmount = t;
            if (t <= criticalThreshold) healthFillImage.color = criticalColor;
            else if (t <= lowThreshold) healthFillImage.color = lowColor;
            else healthFillImage.color = healthyColor;
        }
    }
#endif
}
