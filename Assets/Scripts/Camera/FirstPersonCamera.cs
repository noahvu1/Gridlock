using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person camera look with clean "network seams".
/// + View bobbing (amount / frequency tunable).
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Authority / Control (Network Seam)")]
    [Tooltip("Should this instance simulate locally? In Fusion, set this true for the object with Input Authority.")]
    public bool isLocal = true;

    [Tooltip("If true, read from Unity Input System locally. If false, expect external injection via InjectLook/InjectAim/InjectStrafe.")]
    public bool useLocalInput = true;

    public void SetIsLocal(bool local) => isLocal = local;
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

    [Header("Tilt (Optional)")]
    public float maxTilt = 8f;
    [Range(0f, 30f)] public float tiltLerpSpeed = 10f;
    public float tiltInputScale = 1f;

    // --- View Bobbing ---
    [Header("View Bobbing")]
    public bool enableViewBob = true;
    [Tooltip("How far the camera bobs at full running speed (meters).")]
    public float bobAmount = 0.05f;              // public amount knob
    [Tooltip("Base bob frequency (cycles per second).")]
    public float bobFrequency = 6f;              // public frequency knob
    [Tooltip("How strongly speed scales the bob amplitude (1 = linear).")]
    public float bobSpeedInfluence = 1f;
    [Tooltip("How quickly the camera blends to the bob offset.")]
    public float bobLerpSpeed = 12f;
    [Tooltip("Optional: read planar velocity from this body. If null, tries to find one on characterRoot/parents.")]
    public Rigidbody velocityBody;
    [Tooltip("Optional explicit bob target. Defaults to pitchPivot (camera).")]
    public Transform bobTarget;

    // network seam helpers (optional)
    public void InjectPlanarSpeed(float mps) { _injectedPlanarSpeed = Mathf.Max(0f, mps); }
    public void InjectGrounded(bool grounded) { _injectedGrounded = grounded; _hasInjectedGrounded = true; }

    // internal state
    private float _yaw;
    private float _pitch;
    private float _smoothedYaw;
    private float _smoothedPitch;
    private float _currentTilt;
    private Camera _cam;

    // injected/collected inputs (network seam)
    private Vector2 _lookInput;   // x = yaw, y = pitch
    private float   _strafeInput; // for tilt
    private bool    _aimHeld;

    // bob internal
    private Vector3 _bobRestLocalPos;
    private float   _bobPhase;
    private float   _injectedPlanarSpeed;
    private bool    _injectedGrounded;
    private bool    _hasInjectedGrounded;

    public void InjectLook(Vector2 delta)  => _lookInput += delta;
    public void InjectStrafe(float x)      => _strafeInput = Mathf.Clamp(x, -1f, 1f);
    public void InjectAim(bool held)       => _aimHeld = held;

    public void EnableLook(bool enabled)
    {
        if (enabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _lookInput = Vector2.zero;
        }
    }

    void Awake()
    {
        if (!pitchPivot)    pitchPivot    = transform;
        if (!characterRoot) characterRoot = transform;
        if (!bobTarget)     bobTarget     = pitchPivot;

        _cam = GetComponent<Camera>();
        if (_cam) _cam.fieldOfView = normalFOV;

        // Initialize rotations
        Vector3 yawEuler   = characterRoot.localEulerAngles;
        Vector3 pitchEuler = pitchPivot.localEulerAngles;
        _yaw   = _smoothedYaw   = yawEuler.y;
        _pitch = _smoothedPitch = NormalizeAngle(pitchEuler.x);

        // Initialize bob
        _bobRestLocalPos = bobTarget.localPosition;

        // Try to find a Rigidbody for speed sampling
        if (!velocityBody && characterRoot)
            velocityBody = characterRoot.GetComponentInParent<Rigidbody>();
    }

    void Start()
    {
        if (lockCursorOnStart) EnableLook(true);
    }

    void Update()
    {
        if (!isLocal) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            EnableLook(Cursor.lockState != CursorLockMode.Locked);

        if (useLocalInput && Cursor.lockState == CursorLockMode.Locked)
            GatherLocalInput();

        StepLook();
        StepBob(); // <--- view bobbing

        _lookInput = Vector2.zero;
        _injectedPlanarSpeed = 0f; // consumers inject per-frame if desired
        _hasInjectedGrounded = false;
    }

    private void GatherLocalInput()
    {
        Vector2 look = Vector2.zero;

        if (Mouse.current != null)
            look += Mouse.current.delta.ReadValue();

        if (Gamepad.current != null)
            look += Gamepad.current.rightStick.ReadValue() * gamepadScale * Time.unscaledDeltaTime;

        _lookInput += look;

        float x = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        }
        if (Gamepad.current != null)
            x += Gamepad.current.leftStick.ReadValue().x;

        _strafeInput = Mathf.Clamp(x, -1f, 1f);

        bool mouseAim = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool padAim   = Gamepad.current != null && Gamepad.current.leftTrigger.ReadValue() > 0.2f;
        _aimHeld = mouseAim || padAim;
    }

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

        float targetTilt = -_strafeInput * maxTilt * tiltInputScale;
        float tt = tiltLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-tiltLerpSpeed * Time.unscaledDeltaTime);
        _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, tt);

        pitchPivot.localRotation = Quaternion.Euler(_smoothedPitch, 0f, _currentTilt);

        if (_cam)
        {
            float targetFov = _aimHeld ? adsFOV : normalFOV;
            float ft = fovLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-fovLerpSpeed * Time.unscaledDeltaTime);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, ft);
        }
    }

    private void StepBob()
    {
        if (!enableViewBob || bobAmount <= 0f || bobFrequency <= 0f || bobTarget == null)
        {
            // Snap back to rest if disabled
            if (bobTarget && bobTarget.localPosition != _bobRestLocalPos)
            {
                float lt = bobLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-bobLerpSpeed * Time.deltaTime);
                bobTarget.localPosition = Vector3.Lerp(bobTarget.localPosition, _bobRestLocalPos, lt);
            }
            return;
        }

        // --- Determine planar speed (m/s) and grounded state ---
        float planarSpeed = _injectedPlanarSpeed;
        bool grounded = _hasInjectedGrounded ? _injectedGrounded : true;

        if (velocityBody)
        {
            Vector3 v = velocityBody.linearVelocity;
            float rbPlanar = new Vector3(v.x, 0f, v.z).magnitude;
            // prefer injected speed if provided (network), else use rb
            if (planarSpeed <= 0f) planarSpeed = rbPlanar;
            grounded = _hasInjectedGrounded ? grounded : Mathf.Abs(v.y) < 0.1f; // cheap-ish guess
        }

        // Fade bob with movement speed
        float speed01 = Mathf.Clamp01(planarSpeed * bobSpeedInfluence);
        float amp = bobAmount * speed01;

        // Advance phase based on speed (slower when standing still)
        float freq = bobFrequency * Mathf.Lerp(0.5f, 1.0f, speed01);
        _bobPhase += (grounded ? 1f : 0.2f) * freq * Time.deltaTime;

        // Classic head-bob: small lateral + vertical (vertical is stronger, double freq)
        Vector3 targetOffset = Vector3.zero;
        targetOffset.x = Mathf.Cos(_bobPhase) * amp * 0.4f;
        targetOffset.y = Mathf.Sin(_bobPhase * 2f) * amp;

        // Lerp to target offset
        Vector3 targetPos = _bobRestLocalPos + targetOffset;
        float t = bobLerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-bobLerpSpeed * Time.deltaTime);
        bobTarget.localPosition = Vector3.Lerp(bobTarget.localPosition, targetPos, t);
    }

    /// <summary>Add recoil in degrees: x = pitch up (+), y = yaw right (+).</summary>
    public void AddRecoil(Vector2 degrees)
    {
        _pitch = Mathf.Clamp(_pitch + degrees.x, minPitch, maxPitch);
        _yaw  += degrees.y;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
