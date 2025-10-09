using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Highlights the object under the crosshair (center of camera) if it has tag "Item".
/// Network seam ready:
///  - Only runs when IsLocal == true (set by your network code on spawn).
///  - No Fusion dependency; just call SetIsLocal(true/false) from your NetworkBehaviour.
/// Perf:
///  - Raycasts on a small interval, not every frame.
///  - Uses MaterialPropertyBlock (no material instancing).
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class AimHighlighter : MonoBehaviour
{
    [Header("Authority / Control (Network Seam)")]
    [Tooltip("Should this instance simulate locally? In Fusion, set this true for the object with Input Authority.")]
    public bool isLocal = true;

    /// <summary>Call this from your network spawn code: SetIsLocal(Object.HasInputAuthority)</summary>
    public void SetIsLocal(bool local) => isLocal = local;

    [Header("Raycast")]
    [Tooltip("Camera used for center-screen raycast. Defaults to Camera.main at runtime.")]
    public Camera cam;
    [Tooltip("Max distance for raycast.")]
    public float maxDistance = 5f;
    [Tooltip("Which layers should be hit by the raycast.")]
    public LayerMask hitMask = ~0; // everything
    [Tooltip("Only highlight objects with this tag (set on the item or a parent).")]
    public string requiredTag = "Item";
    [Tooltip("Seconds between raycasts (smaller = more responsive, bigger = cheaper).")]
    [Range(0.01f, 0.2f)] public float rayInterval = 0.05f;

    [Header("Highlight")]
    [Tooltip("Color applied while aimed.")]
    public Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);
    [Tooltip("Emission intensity (0 to disable emission entirely).")]
    [Range(0f, 5f)] public float emissionBoost = 1.5f;
    [Tooltip("How quickly highlight fades in/out.")]
    [Range(0f, 30f)] public float lerpSpeed = 18f;

    [Header("Debug")]
    public bool drawRay = false;

    // --- internals ---
    private Transform _currentTarget;
    private readonly List<Renderer> _currentRenderers = new();
    private readonly Dictionary<Renderer, MaterialPropertyBlock> _mpbCache = new();
    private float _nextRayTime;

    // Shader property ids
    private static readonly int ID_BaseColor   = Shader.PropertyToID("_BaseColor");      // URP Lit
    private static readonly int ID_Color       = Shader.PropertyToID("_Color");          // Standard/Legacy
    private static readonly int ID_Emission    = Shader.PropertyToID("_EmissionColor");  // URP/Standard

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        // Network seam: only the local player should drive this (camera + highlight)
        if (!isLocal || !cam) { FadeOutAndCleanup(); return; }

        // Cheap gate: don't raycast if cursor isn't locked (e.g., in menus)
        if (Cursor.lockState != CursorLockMode.Locked) { FadeOutAndCleanup(); return; }

        // Throttled raycast
        if (Time.unscaledTime >= _nextRayTime)
        {
            _nextRayTime = Time.unscaledTime + rayInterval;
            PerformRaycast();
        }

        // Smoothly apply highlight each frame
        UpdateHighlight();
    }

    private void PerformRaycast()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (drawRay) Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.yellow, rayInterval);

        if (Physics.Raycast(ray, out var hit, maxDistance, hitMask, QueryTriggerInteraction.Collide))
        {
            // Find nearest ancestor that actually has the tag
            Transform tagged = FindTaggedAncestor(hit.collider.transform, requiredTag);

            if (tagged != null)
            {
                if (_currentTarget != tagged)
                    SetTarget(tagged);
                return;
            }
        }

        // No valid hit
        ClearTarget();
    }

    private static Transform FindTaggedAncestor(Transform t, string tag)
    {
        Transform found = null;
        while (t != null)
        {
            if (t.CompareTag(tag))
                found = t; // remember but keep walking up
            t = t.parent;
        }
        return found;
    }

    private void SetTarget(Transform t)
    {
        if (_currentTarget == t) return;

        ClearTarget();
        _currentTarget = t;

        // Grab all renderers under this item
        _currentRenderers.AddRange(_currentTarget.GetComponentsInChildren<Renderer>(true));

        // Ensure MPB cache entries exist
        foreach (var r in _currentRenderers)
            if (r && !_mpbCache.ContainsKey(r))
                _mpbCache[r] = new MaterialPropertyBlock();
    }

    private void ClearTarget()
    {
        if (_currentTarget == null) return;
        _currentTarget = null; // we'll fade out in UpdateHighlight()
    }

    private void FadeOutAndCleanup()
    {
        // If we’re not active, still let highlight fade back to normal
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        var toRemove = new List<Renderer>();
        float t = lerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);

        foreach (var kv in _mpbCache)
        {
            var r = kv.Key;
            if (!r) { toRemove.Add(r); continue; }

            bool shouldHighlight = _currentTarget != null && _currentRenderers.Contains(r);
            var mpb = kv.Value;

            r.GetPropertyBlock(mpb);

            // Read current colors
            Color baseCol = GetBaseColor(r, mpb, out int baseId);
            Color emisCol = mpb.GetColor(ID_Emission);

            // Targets
            Color targetBase = shouldHighlight
                ? Color.Lerp(baseCol, highlightColor, 0.65f)
                : Color.Lerp(baseCol, Color.white,   0.65f);

            Color targetEmis = (emissionBoost > 0f && shouldHighlight)
                ? highlightColor * emissionBoost
                : Color.black;

            // Lerp
            baseCol = Color.Lerp(baseCol, targetBase, t);
            emisCol = Color.Lerp(emisCol, targetEmis, t);

            // Write back
            mpb.SetColor(baseId, baseCol);
            mpb.SetColor(ID_Emission, emisCol);

            // NOTE: enabling _EMISSION on r.material creates an instance.
            // If you want zero instancing, keep emissionBoost = 0 and only tint base color.
            if (emissionBoost > 0f)
                r.material.EnableKeyword("_EMISSION");

            r.SetPropertyBlock(mpb);
        }

        foreach (var r in toRemove) _mpbCache.Remove(r);

        // Keep current renderer list tidy
        if (_currentTarget == null && _currentRenderers.Count > 0)
            _currentRenderers.Clear();
    }

    private static Color GetBaseColor(Renderer r, MaterialPropertyBlock mpb, out int idUsed)
    {
        // Prefer URP Lit
        if (r.sharedMaterial && r.sharedMaterial.HasProperty(ID_BaseColor))
        {
            idUsed = ID_BaseColor;
            return mpb.GetVector(ID_BaseColor);
        }
        // Fallback: Standard/Legacy
        idUsed = ID_Color;
        return mpb.GetVector(ID_Color);
    }
}
