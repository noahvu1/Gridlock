using UnityEngine;
using UnityEngine.InputSystem; // new Input System

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Ground movement speed in m/s.")]
    public float moveSpeed = 5f;

    [Tooltip("Optional sprint multiplier when Left Shift is held.")]
    public float sprintMultiplier = 1.5f;

    [Tooltip("Impulse strength for spacebar jump.")]
    public float jumpForce = 7f;

    [Header("Look / Reference")]
    [Tooltip("Usually your Camera (or a head pivot). Movement will follow this transform's yaw.")]
    public Transform moveReference;

    [Tooltip("If true, rotate the visual/body to face the camera yaw.")]
    public bool alignBodyYawToCamera = true;

    [Tooltip("Optional visual/body to rotate. If null, rotates this transform.")]
    public Transform bodyRoot;

    [Range(0f, 30f), Tooltip("How quickly the body turns to face camera yaw.")]
    public float bodyYawLerp = 12f;

    private Rigidbody rb;
    private Vector3 moveInput; // camera-relative planar input
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        if (!bodyRoot) bodyRoot = transform;
        if (!moveReference && Camera.main) moveReference = Camera.main.transform;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // --- Raw input ---
        float x = 0f, z = 0f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.wKey.isPressed) z += 1f;

        // --- Build move relative to camera yaw ---
        if (moveReference)
        {
            // Get camera's yaw-only forward/right on the ground plane
            Vector3 fwd = moveReference.forward;
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; // fallback

            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            moveInput = (right * x + fwd * z);
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
        }
        else
        {
            // Fallback to world axes if no reference found
            moveInput = new Vector3(x, 0f, z).normalized;
        }

        // --- Jump ---
        if (kb.spaceKey.wasPressedThisFrame && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false; // prevent double jump until grounded again
        }

        // --- Optional: turn body/mesh to face camera yaw ---
        if (alignBodyYawToCamera && moveReference && bodyRoot)
        {
            float camYaw = moveReference.eulerAngles.y;
            Quaternion target = Quaternion.Euler(0f, camYaw, 0f);
            float t = bodyYawLerp <= 0f ? 1f : 1f - Mathf.Exp(-bodyYawLerp * Time.deltaTime);
            bodyRoot.rotation = Quaternion.Slerp(bodyRoot.rotation, target, t);
        }
    }

    void FixedUpdate()
    {
        // Sprint (optional)
        float speed = moveSpeed;
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            speed *= sprintMultiplier;

        // Use MovePosition to move along the ground plane
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
}
