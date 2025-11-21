using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class StaminaSprint : MonoBehaviour
{
    [Header("Links")]
    public PlayerMovement player;       // drag your PlayerMovement here
    public AudioSource audioSource;     // optional
    public AudioClip lowStaminaClip;    // plays once when crossing below Low Threshold

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float startStamina = 100f;
    [Tooltip("Sprint drains this per second")]
    public float sprintDrainPerSec = 12f;
    [Tooltip("Base regen when not sprinting")]
    public float baseRegenPerSec = 2f;
    [Tooltip("Max regen when not sprinting (after ramp)")]
    public float maxRegenPerSec = 10f;
    [Tooltip("Seconds to ramp from base to near max regen")]
    public float regenRampSeconds = 6f;

    [Header("Thresholds")]
    [Tooltip("Below this, play the low-stamina sound once")]
    public float lowThreshold = 20f;
    [Tooltip("Must recover to this to allow sprint again after 0")]
    public float recoverTo = 30f;

    [Header("Audio Cooldown")]
    [Tooltip("Seconds before another low-stamina sound can play (if 0, uses clip length).")]
    public float lowWarnCooldown = 0f;

    [Header("Input")]
    public Key sprintKey = Key.LeftShift;

    [Header("Debug")]
    public bool debugLogs = false;

    enum StaminaState { Normal, Sprinting, Recovering, Exhausted }
    StaminaState _state = StaminaState.Normal;

    float _stamina;
    float _timeSinceSprint;
    bool _playedLowSound;
    float _nextAllowedWarnTime;

    void Reset()
    {
        player = GetComponent<PlayerMovement>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (!player) player = GetComponent<PlayerMovement>();
        _stamina = Mathf.Clamp(startStamina, 0f, maxStamina);
        _state = _stamina <= 0f ? StaminaState.Exhausted : StaminaState.Normal;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || player == null) return;

        bool moving = kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed;
        bool sprintRequested = kb[sprintKey].isPressed && moving;

        switch (_state)
        {
            case StaminaState.Sprinting:
                if (!sprintRequested) { _state = StaminaState.Recovering; break; }

                _stamina -= sprintDrainPerSec * Time.deltaTime;
                _stamina = Mathf.Max(0f, _stamina);
                _timeSinceSprint = 0f;

                if (_stamina <= 0f)
                {
                    _state = StaminaState.Exhausted;
                    if (debugLogs) Debug.Log("[Stamina] Exhausted");
                }
                break;

            case StaminaState.Normal:
                if (sprintRequested && _stamina > 0f) { _state = StaminaState.Sprinting; break; }
                RegenTick();
                break;

            case StaminaState.Recovering:
                if (sprintRequested && _stamina > 0f) { _state = StaminaState.Sprinting; _playedLowSound = false; break; }
                RegenTick();
                if (_stamina >= maxStamina * 0.999f) _state = StaminaState.Normal;
                break;

            case StaminaState.Exhausted:
                RegenTick();
                if (_stamina >= recoverTo)
                {
                    _state = StaminaState.Normal;
                    if (debugLogs) Debug.Log("[Stamina] Recovered");
                }
                break;
        }

        player.externalSprintAllowed = (_state != StaminaState.Exhausted && _stamina > 0f);

        // cooldown-based warning only
        if (_stamina <= lowThreshold && !_playedLowSound)
        {
            _playedLowSound = true;
            TryPlayLowWarn();
        }
        if (_stamina > lowThreshold)
            _playedLowSound = false;
    }

    void TryPlayLowWarn()
    {
        if (!lowStaminaClip) return;
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();

        float cooldown = (lowWarnCooldown > 0f) ? lowWarnCooldown : lowStaminaClip.length;
        if (Time.time < _nextAllowedWarnTime) return;

        audioSource.PlayOneShot(lowStaminaClip, 0.08f);
        _nextAllowedWarnTime = Time.time + cooldown;
    }

    void RegenTick()
    {
        _timeSinceSprint += Time.deltaTime;
        float ramp01 = 1f - Mathf.Exp(-_timeSinceSprint / Mathf.Max(0.0001f, regenRampSeconds));
        float regenPerSec = Mathf.Lerp(baseRegenPerSec, maxRegenPerSec, ramp01);
        float missing01 = 1f - (_stamina / Mathf.Max(0.0001f, maxStamina));
        float delta = regenPerSec * missing01 * Time.deltaTime;
        _stamina = Mathf.Min(maxStamina, _stamina + delta);
    }

    public void Refill()
    {
        _stamina = maxStamina;
        _state = StaminaState.Normal;
        _timeSinceSprint = 0f;
        _playedLowSound = false;
        _nextAllowedWarnTime = 0f;
    }

    public float Stamina => _stamina;
    public float Stamina01 => maxStamina > 0f ? _stamina / maxStamina : 0f;
    public bool CanSprintNow => player != null && player.externalSprintAllowed;
}
