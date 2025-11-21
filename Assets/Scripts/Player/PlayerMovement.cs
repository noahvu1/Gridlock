using UnityEngine;
using UnityEngine.InputSystem; // new Input System

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float jumpForce = 7f;

    [Header("Look / Reference")]
    public Transform moveReference;
    public bool alignBodyYawToCamera = true;
    public Transform bodyRoot;
    [Range(0f, 30f)] public float bodyYawLerp = 12f;

    [Header("Audio")]
    public AudioSource footstepSource;    // single looping walk sound
    public AudioClip walkLoop;
    [Range(0f, 1f)] public float walkVolume = 1f;
    [Range(0.5f, 2f)] public float sprintPitchMultiplier = 1.3f; // speed up when sprinting

    [HideInInspector] public bool externalSprintAllowed = true;
    [HideInInspector] public float externalSpeedScale = 1f;
    [HideInInspector] public bool externalJumpAllowed = true;

    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (!bodyRoot) bodyRoot = transform;
        if (!moveReference && Camera.main) moveReference = Camera.main.transform;

        if (!footstepSource)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = true;
            footstepSource.spatialBlend = 1f;
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // movement input
        float x = 0f, z = 0f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.wKey.isPressed) z += 1f;

        if (moveReference)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(moveReference.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            moveInput = right * x + fwd * z;
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
        }
        else
        {
            moveInput = new Vector3(x, 0f, z).normalized;
        }

        // jump
        if (kb.spaceKey.wasPressedThisFrame && isGrounded && externalJumpAllowed)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        // rotate body to camera yaw
        if (alignBodyYawToCamera && moveReference && bodyRoot)
        {
            float camYaw = moveReference.eulerAngles.y;
            Quaternion target = Quaternion.Euler(0f, camYaw, 0f);
            float t = bodyYawLerp <= 0f ? 1f : 1f - Mathf.Exp(-bodyYawLerp * Time.deltaTime);
            bodyRoot.rotation = Quaternion.Slerp(bodyRoot.rotation, target, t);
        }

        HandleFootstepAudio();
    }

    void FixedUpdate()
    {
        float speed = moveSpeed;

        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && externalSprintAllowed)
            speed *= sprintMultiplier;

        speed *= Mathf.Max(0f, externalSpeedScale);

        Vector3 targetDelta = moveInput * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + targetDelta);
    }

    void OnCollisionStay(Collision c)
    {
        if (c.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionExit(Collision c)
    {
        if (c.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    // play and adjust looping footsteps
    void HandleFootstepAudio()
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f && isGrounded;
        bool isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && externalSprintAllowed;

        if (isMoving)
        {
            if (!footstepSource.isPlaying && walkLoop)
            {
                footstepSource.clip = walkLoop;
                footstepSource.volume = walkVolume;
                footstepSource.loop = true;
                footstepSource.Play();
            }

            // adjust pitch for sprinting speed effect
            float targetPitch = isSprinting ? sprintPitchMultiplier : 1f;
            footstepSource.pitch = Mathf.Lerp(footstepSource.pitch, targetPitch, 10f * Time.deltaTime);
        }
        else
        {
            if (footstepSource.isPlaying)
                footstepSource.Stop();
        }
    }
}
