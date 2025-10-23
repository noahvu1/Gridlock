using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Authority / Control (Network Seam)")]
    [Tooltip("Should this instance simulate locally? In Fusion, set this true for the object with Input Authority.")]
    public bool isLocal = true;

    [Tooltip("If true, read from Unity Input System locally. If false, expect external injection via InjectLook/InjectAim.")]
    public bool useLocalInput = true;

    // Sets whether this instance is controlled locally.
    public void SetIsLocal(bool local) => isLocal = local;

    // Enables or disables reading local input.
    public void SetUseLocalInput(bool use) => useLocalInput = use;

    [Header("Targets")]
    [Tooltip("Player body (yaw). If null, yaw rotates this transform.")]
    public Transform characterRoot;
    [Tooltip("Pitch pivot (usually the camera itself or a head object). If null, uses this transform.")]
    public Transform pitchPivot;

    [Header("Sensitivity")]
    public float sensitivityX = 1.6f;
    public float sensitivityY = 1.2f;
    [Tooltip("Extra multiplier for gamepad right-stick (per second).")]
    public float gamepadScale = 120f;
    public bool invertY = false;

    [Header("Smoothing")]
    [Range(0f, 30f)] public float smoothing = 12f;

    [Header("Pitch Clamp")]
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Cursor")]
    [Tooltip("Lock & hide cursor at Start. Your pause menu can call EnableLook(false) to unlock.")]
    public bool lockCursorOnStart = true;

    [Header("FOV / ADS")]
    public float normalFOV = 75f;
    public float adsFOV = 60f;
    [Range(0f, 30f)] public float fovLerpSpeed = 12f;

    // Injects additional look delta this frame.
    public void InjectLook(Vector2 delta) => _lookInput += delta;

    // Injects ADS/aim held state.
    public void InjectAim(bool held) => _aimHeld = held;

    // Locks/unlocks and shows/hides the cursor.
    public void EnableLook(bool enabled)
    {
        if (enabled) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; _lookInput = Vector2.zero; }
    }

    // Sets defaults, caches components, and initializes state.
    void Awake()
    {
        if (!pitchPivot)    pitchPivot    = transform;
        if (!characterRoot) characterRoot = transform;

        _cam = GetComponent<Camera>();
        if (_cam) _cam.fieldOfView = normalFOV;

        var yawEuler = characterRoot.localEulerAngles;
        var pitchEuler = pitchPivot.localEulerAngles;
        _yaw   = _smoothedYaw   = yawEuler.y;
        _pitch = _smoothedPitch = NormalizeAngle(pitchEuler.x);
    }

    // Optionally locks the cursor on start.
    void Start() { if (lockCursorOnStart) EnableLook(true); }

    // Main update: input, look step, and per-frame resets.
    void Update()
    {
        if (!isLocal) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            EnableLook(Cursor.lockState != CursorLockMode.Locked);

        if (useLocalInput && Cursor.lockState == CursorLockMode.Locked)
            GatherLocalInput();

        StepLook();

        _lookInput = Vector2.zero;
    }

    // Reads local input from mouse/gamepad.
    private void GatherLocalInput()
    {
        Vector2 look = Vector2.zero;
        if (Mouse.current  != null) look += Mouse.current.delta.ReadValue();
        if (Gamepad.current != null) look += Gamepad.current.rightStick.ReadValue() * gamepadScale * Time.unscaledDeltaTime;
        _lookInput += look;

        bool mouseAim = Mouse.current  != null && Mouse.current.rightButton.isPressed;
        bool padAim   = Gamepad.current != null && Gamepad.current.leftTrigger.ReadValue() > 0.2f;
        _aimHeld = mouseAim || padAim;
    }

    // Applies yaw/pitch smoothing and FOV transitions (no lean/bob).
    private void StepLook()
    {
        float inv = invertY ? 1f : -1f;
        _yaw   += _lookInput.x * sensitivityX;
        _pitch += _lookInput.y * sensitivityY * inv;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);

        float t = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        _smoothedYaw   = Mathf.LerpAngle(_smoothedYaw, _yaw, t);
        _smoothedPitch = Mathf.Lerp(_smoothedPitch, _pitch, t);

        characterRoot.localRotation = Quaternion.Euler(0f, _smoothedYaw, 0f);
        pitchPivot.localRotation    = Quaternion.Euler(_smoothedPitch, 0f, 0f);

        if (_cam)
        {
            float targetFov = _aimHeld ? adsFOV : normalFOV;
            float ft = fovLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-fovLerpSpeed * Time.unscaledDeltaTime);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, ft);
        }
    }

    // Adds recoil in degrees (x=pitch up, y=yaw right).
    public void AddRecoil(Vector2 degrees)
    {
        _pitch = Mathf.Clamp(_pitch + degrees.x, minPitch, maxPitch);
        _yaw  += degrees.y;
    }

    // Normalizes an angle to [-180, 180].
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    // --- internals ---
    private float _yaw, _pitch, _smoothedYaw, _smoothedPitch;
    private Camera _cam;
    private Vector2 _lookInput;
    private bool _aimHeld;
}
