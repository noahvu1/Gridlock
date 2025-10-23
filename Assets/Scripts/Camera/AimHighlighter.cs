using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class AimHighlighter : MonoBehaviour
{
    [Header("Authority / Control (Network Seam)")]
    [Tooltip("Set true on the locally controlled player (e.g., Input Authority in Fusion).")]
    public bool isLocal = true;
    public void SetIsLocal(bool local) => isLocal = local;

    [Header("Raycast")]
    [Tooltip("Camera used for center-screen raycast. Defaults to Camera.main.")]
    public Camera cam;
    [Tooltip("Max distance for raycast.")]
    public float maxDistance = 5f;
    [Tooltip("Layers that can be hit.")]
    public LayerMask hitMask = ~0;
    [Tooltip("Only highlight objects with this tag (on item or a parent).")]
    public string requiredTag = "Item";
    [Tooltip("Seconds between raycasts (smaller = more responsive).")]
    [Range(0.01f, 0.2f)] public float rayInterval = 0.05f;

    [Header("Highlight")]
    [Tooltip("Tint applied while aimed.")]
    public Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);
    [Tooltip("Extra glow (0 disables emission).")]
    [Range(0f, 5f)] public float emissionBoost = 1.5f;
    [Range(0f, 1f), Tooltip("How strong the tint is vs original base color.")]
    public float tintStrength = 0.6f;
    
    // Expose current target (null if none)
    public Transform CurrentTarget => _currentTarget;


    // internals
    private Transform _currentTarget;
    private readonly List<Renderer> _currentRenderers = new();
    private MaterialPropertyBlock _mpb;
    private float _nextRayTime;

    // shader ids
    private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ID_Color     = Shader.PropertyToID("_Color");
    private static readonly int ID_Emission  = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void OnDisable()
    {
        // make sure nothing stays glowing when this script is disabled/destroyed
        ClearHighlightForList(_currentRenderers);
        _currentRenderers.Clear();
        _currentTarget = null;
    }

    void Update()
    {
        if (!isLocal || !cam || Cursor.lockState != CursorLockMode.Locked)
        {
            SetTarget(null); // ensure we clear if we lose authority/lock
            return;
        }

        if (Time.unscaledTime >= _nextRayTime)
        {
            _nextRayTime = Time.unscaledTime + rayInterval;
            PerformRaycast();
        }
    }

    // Cast a ray from the crosshair and pick the tagged ancestor
    private void PerformRaycast()
    {
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, maxDistance, hitMask, QueryTriggerInteraction.Collide))
        {
            var tagged = FindTaggedAncestor(hit.collider.transform, requiredTag);
            SetTarget(tagged);
        }
        else
        {
            SetTarget(null);
        }
    }

    // Walk up until we find the first parent with the tag (nearest match)
    private static Transform FindTaggedAncestor(Transform t, string tag)
    {
        while (t)
        {
            if (t.CompareTag(tag)) return t;
            t = t.parent;
        }
        return null;
    }

    // Switch target: unpaint old, then paint new
    private void SetTarget(Transform t)
    {
        if (_currentTarget == t) return;

        // 1) unpaint previous
        if (_currentRenderers.Count > 0)
            ClearHighlightForList(_currentRenderers);

        _currentRenderers.Clear();
        _currentTarget = t;

        // 2) cache and paint new
        if (_currentTarget)
        {
            _currentTarget.GetComponentsInChildren(true, _currentRenderers);
            ApplyHighlightForList(_currentRenderers);
        }
    }

    // Reset each renderer to its material's original base color and no emission
    private void ClearHighlightForList(List<Renderer> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var r = list[i];
            if (!r) { list.RemoveAt(i); continue; }

            var mat = r.sharedMaterial;
            if (!mat) continue;

            int baseId = mat.HasProperty(ID_BaseColor) ? ID_BaseColor : ID_Color;
            var origBase = mat.HasProperty(baseId) ? mat.GetColor(baseId) : Color.white;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(baseId, origBase);
            _mpb.SetColor(ID_Emission, Color.black);
            r.SetPropertyBlock(_mpb);

            // turn off keyword so it doesn't glow
            r.material.DisableKeyword("_EMISSION");
        }
    }

    // Apply a tinted base + optional emission
    private void ApplyHighlightForList(List<Renderer> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var r = list[i];
            if (!r) { list.RemoveAt(i); continue; }

            var mat = r.sharedMaterial;
            if (!mat) continue;

            int baseId = mat.HasProperty(ID_BaseColor) ? ID_BaseColor : ID_Color;
            var origBase = mat.HasProperty(baseId) ? mat.GetColor(baseId) : Color.white;

            var tinted = Color.Lerp(origBase, highlightColor, Mathf.Clamp01(tintStrength));

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(baseId, tinted);

            if (emissionBoost > 0f)
            {
                _mpb.SetColor(ID_Emission, highlightColor * emissionBoost);
                r.material.EnableKeyword("_EMISSION");
            }
            else
            {
                _mpb.SetColor(ID_Emission, Color.black);
                r.material.DisableKeyword("_EMISSION");
            }

            r.SetPropertyBlock(_mpb);
        }
    }
}
