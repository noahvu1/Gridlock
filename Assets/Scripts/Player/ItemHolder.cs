using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ItemHolder : MonoBehaviour
{
    [Header("Refs")]
    public AimHighlighter highlighter;   // must expose CurrentTarget
    public Transform holdPoint;          // set this to Hip_R_end (or a child empty under it)
    public Transform fallbackDropParent; // optional: where to put item if original parent is null

    [Header("Drop force")]
    public float dropForward = 1.5f;
    public float dropUp = 0.5f;

    [Header("Audio")]
    public AudioSource audioSource;      // optional: will be created if missing
    [Space(4)]
    public AudioClip pickupSound;        // sound to play when you pick up an item
    [Range(0f, 1f)] public float pickupVolume = 1f;
    [Space(4)]
    public AudioClip dropSound;          // sound to play when you drop an item
    [Range(0f, 1f)] public float dropVolume = 1f;

    // runtime
    Transform held;
    Rigidbody heldRb;
    List<Collider> heldCols = new();
    Transform originalParent;

    void Reset()
    {
        highlighter = GetComponentInChildren<AimHighlighter>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        // make sure we have an AudioSource
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // make it 3D
    }

    void Update()
    {
        if (Mouse.current?.leftButton.wasPressedThisFrame == true) TryPickup();
        if (Keyboard.current?.gKey.wasPressedThisFrame == true) Drop();
    }

    void TryPickup()
    {
        if (held || !highlighter || !holdPoint) return;

        // what we're aiming at
        Transform t = highlighter.CurrentTarget;
        if (!t) return;

        // walk up to the root tagged "Item"
        Transform root = FindTaggedAncestor(t, "Item");
        if (!root) return;

        // needs a rigidbody
        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (!rb) rb = root.GetComponentInChildren<Rigidbody>();
        if (!rb) return;

        // cache
        held = root;
        heldRb = rb;
        originalParent = held.parent;
        heldCols.Clear();
        held.GetComponentsInChildren(true, heldCols);

        // disable collisions + freeze
        for (int i = 0; i < heldCols.Count; i++)
            if (heldCols[i]) heldCols[i].enabled = false;

        heldRb.isKinematic = true;
        heldRb.linearVelocity = Vector3.zero;
        heldRb.angularVelocity = Vector3.zero;

        // parent to hip hold point
        held.SetParent(holdPoint, worldPositionStays: true);
        held.localPosition = Vector3.zero;
        held.localRotation = Quaternion.identity;

        // 🔊 play pickup sound
        if (pickupSound && audioSource)
            audioSource.PlayOneShot(pickupSound, pickupVolume);
    }

    public void Drop()
    {
        if (!held) return;

        // unparent
        Transform dropParent = originalParent ? originalParent : fallbackDropParent;
        held.SetParent(dropParent, worldPositionStays: true);

        // restore physics + enable colliders
        for (int i = 0; i < heldCols.Count; i++)
            if (heldCols[i]) heldCols[i].enabled = true;

        if (heldRb)
        {
            heldRb.isKinematic = false;

            // small toss forward (uses camera forward if available)
            Transform fwdRef = highlighter && highlighter.cam ? highlighter.cam.transform : transform;
            Vector3 fwd = fwdRef ? fwdRef.forward : Vector3.forward;
            heldRb.AddForce(fwd * dropForward + Vector3.up * dropUp, ForceMode.VelocityChange);
        }

        // 🔊 play drop sound
        if (dropSound && audioSource)
            audioSource.PlayOneShot(dropSound, dropVolume);

        // clear
        held = null;
        heldRb = null;
        heldCols.Clear();
        originalParent = null;
    }

    static Transform FindTaggedAncestor(Transform t, string tag)
    {
        while (t)
        {
            if (t.CompareTag(tag)) return t;
            t = t.parent;
        }
        return null;
    }
}
