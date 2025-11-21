using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ByakuganCullingPlus : MonoBehaviour
{
    [Header("Camera / Visibility")]
    public Camera targetCamera;
    public Camera[] extraCameras;

    [Header("Movement Clamp")]
    [Range(0f, 1f)] public float speedScaleWhileActive = 0.08f;
    public bool disableSprintWhileActive = true;
    public bool disableJumpWhileActive = true;

    [Header("Toggle")]
    public Key toggleKey = Key.E;

    [Header("Radial Wipe")]
    public bool useRadialWipe = true;
    public float wipeDuration = 0.75f;          // seconds
    public float wipeMaxRadius = 250f;          // how far the wave expands
    public float scanInterval = 0.05f;          // how often we update batch visibility

    [Header("Highlight Pulse (Items/Players)")]
    public bool pulseVisibleLayers = true;
    public Color pulseColor = new Color(0.75f, 0.95f, 1f); // pale cyan
    public float pulseSpeed = 2.0f;             // Hz
    public float pulseMin = 0.0f;
    public float pulseMax = 2.5f;               // emission intensity peak

    [Header("Audio")]
    public AudioClip toggleSound;               // one sound for on/off
    [Range(0f,1f)] public float volume = 1f;   // volume for the sound

    // runtime
    bool _active;
    int _origMask;
    int[] _extraOrigMasks;
    PlayerMovement _move;

    int _layerPlayer, _layerItem, _layerUI;

    AudioSource _audio; // uses the AudioSource on the camera

    // wipe bookkeeping
    struct RendState
    {
        public Renderer r;
        public bool wasEnabled;
        public bool hiddenByWipe;
    }
    List<RendState> _nonKept = new List<RendState>(2048);

    // pulse bookkeeping
    struct PulseState
    {
        public Renderer r;
        public string emissionProp; // _EmissionColor if present
        public bool hasEmission;
        public MaterialPropertyBlock mpb;
    }
    List<PulseState> _pulsers = new List<PulseState>(1024);

    void Awake()
    {
        if (!targetCamera && Camera.main) targetCamera = Camera.main;
        _move = GetComponent<PlayerMovement>();
        if (extraCameras != null && extraCameras.Length > 0)
            _extraOrigMasks = new int[extraCameras.Length];

        _layerPlayer = LayerMask.NameToLayer("Player");
        _layerItem   = LayerMask.NameToLayer("Item");
        _layerUI     = LayerMask.NameToLayer("UI");

        // grab the AudioSource from the camera
        if (targetCamera) _audio = targetCamera.GetComponent<AudioSource>();
        if (!_audio && Camera.main) _audio = Camera.main.GetComponent<AudioSource>();
    }

    void OnDisable()
    {
        if (_active) Toggle(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
            Toggle(!_active);

        if (_active && pulseVisibleLayers && _pulsers.Count > 0)
            UpdatePulse(Time.time);
    }

    public void Toggle(bool on)
    {
        _active = on;

        // play one sound for both on/off
        if (_audio && toggleSound) _audio.PlayOneShot(toggleSound, volume);

        // movement restriction
        if (_move)
        {
            _move.externalSpeedScale    = on ? Mathf.Clamp01(speedScaleWhileActive) : 1f;
            _move.externalSprintAllowed = on ? !disableSprintWhileActive : true;
            _move.externalJumpAllowed   = on ? !disableJumpWhileActive   : true;
        }

        if (!targetCamera) return;

        if (on)
        {
            // store camera masks
            _origMask = targetCamera.cullingMask;
            if (extraCameras != null)
            {
                for (int i = 0; i < extraCameras.Length; i++)
                {
                    if (!extraCameras[i]) continue;
                    _extraOrigMasks[i] = extraCameras[i].cullingMask;
                }
            }

            // collect renderers for wipe and pulsing
            if (useRadialWipe) CollectRenderersForWipe();
            if (pulseVisibleLayers) CollectPulsers();

            // do radial wipe, then lock to culling mask
            if (useRadialWipe)
                StartCoroutine(WipeOutThenMask());
            else
                ApplyKeptMask(); // immediate
        }
        else
        {
            // if we wiped + masked, first restore mask so world is renderable again
            RestoreCameraMask();

            // reverse wipe to bring things back nicely
            if (useRadialWipe && _nonKept.Count > 0)
                StartCoroutine(WipeIn());
            else
                RestoreWipeInstant();

            // stop pulsing
            ClearPulse();
        }
    }

    // --- Camera mask helpers ---

    void ApplyKeptMask()
    {
        int mask = (1 << _layerPlayer) | (1 << _layerItem) | (1 << _layerUI);
        targetCamera.cullingMask = mask;
        if (extraCameras != null)
        {
            for (int i = 0; i < extraCameras.Length; i++)
            {
                if (!extraCameras[i]) continue;
                extraCameras[i].cullingMask = mask;
            }
        }
    }

    void RestoreCameraMask()
    {
        targetCamera.cullingMask = _origMask;
        if (extraCameras != null)
        {
            for (int i = 0; i < extraCameras.Length; i++)
            {
                if (!extraCameras[i]) continue;
                extraCameras[i].cullingMask = _extraOrigMasks[i];
            }
        }
    }

    // --- Radial wipe ---

    void CollectRenderersForWipe()
    {
        _nonKept.Clear();
        var all = Object.FindObjectsOfType<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (!r) continue;
            int L = r.gameObject.layer;
            if (L == _layerPlayer || L == _layerItem || L == _layerUI) continue;

            _nonKept.Add(new RendState
            {
                r = r,
                wasEnabled = r.enabled,
                hiddenByWipe = false
            });
        }
    }

    IEnumerator WipeOutThenMask()
    {
        float t = 0f;
        Vector3 center = transform.position;
        float radius = 0f;
        float stepTimer = 0f;

        while (t < wipeDuration)
        {
            t += Time.deltaTime;
            stepTimer += Time.deltaTime;
            center = transform.position; // follow player
            float k = Mathf.Clamp01(t / wipeDuration);
            radius = Mathf.Lerp(0f, wipeMaxRadius, k);

            if (stepTimer >= scanInterval)
            {
                stepTimer = 0f;
                HideWithinRadius(center, radius);
            }

            yield return null;
        }

        // final pass
        HideWithinRadius(transform.position, wipeMaxRadius);

        // switch to culling mask so it costs nothing afterward
        ApplyKeptMask();
    }

    void HideWithinRadius(Vector3 center, float radius)
    {
        float r2 = radius * radius;
        for (int i = 0; i < _nonKept.Count; i++)
        {
            var rs = _nonKept[i];
            if (!rs.r) continue;
            if (rs.hiddenByWipe) continue;

            Vector3 p = rs.r.bounds.ClosestPoint(center);
            float d2 = (p - center).sqrMagnitude;
            if (d2 <= r2)
            {
                rs.r.enabled = false;
                rs.hiddenByWipe = true;
                _nonKept[i] = rs;
            }
        }
    }

    IEnumerator WipeIn()
    {
        float t = 0f;

        // Precompute distances
        Vector3 center = transform.position;
        List<(float dist2, int idx)> order = new List<(float, int)>(_nonKept.Count);
        order.Clear();
        for (int i = 0; i < _nonKept.Count; i++)
        {
            var rs = _nonKept[i];
            if (!rs.r) continue;
            Vector3 p = rs.r.bounds.ClosestPoint(center);
            float d2 = (p - center).sqrMagnitude;
            order.Add((d2, i));
        }
        order.Sort((a, b) => b.dist2.CompareTo(a.dist2)); // far -> near

        int cursor = 0;
        while (t < wipeDuration)
        {
            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / wipeDuration);
            float threshold = Mathf.Lerp(0f, 1f, k); // 0..1 progression
            int targetIndex = Mathf.FloorToInt(threshold * (order.Count - 1));

            while (cursor <= targetIndex && cursor < order.Count)
            {
                int idx = order[cursor].idx;
                var rs = _nonKept[idx];
                if (rs.r)
                {
                    rs.r.enabled = rs.wasEnabled;
                    rs.hiddenByWipe = false;
                    _nonKept[idx] = rs;
                }
                cursor++;
            }

            yield return null;
        }

        RestoreWipeInstant();
    }

    void RestoreWipeInstant()
    {
        for (int i = 0; i < _nonKept.Count; i++)
        {
            var rs = _nonKept[i];
            if (!rs.r) continue;
            rs.r.enabled = rs.wasEnabled;
            rs.hiddenByWipe = false;
            _nonKept[i] = rs;
        }
        _nonKept.Clear();
    }

    // --- Pulse ---

    void CollectPulsers()
    {
        _pulsers.Clear();
        AddLayerPulsers(_layerPlayer);
        AddLayerPulsers(_layerItem);
    }

    void AddLayerPulsers(int layer)
    {
        var all = Object.FindObjectsOfType<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (!r) continue;
            if (r.gameObject.layer != layer) continue;

            string prop = null;
            var mats = r.sharedMaterials;
            bool hasEmission = false;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (!mat) continue;
                if (mat.HasProperty("_EmissionColor")) { prop = "_EmissionColor"; hasEmission = true; break; }
            }

            var st = new PulseState
            {
                r = r,
                emissionProp = prop,
                hasEmission = hasEmission,
                mpb = new MaterialPropertyBlock()
            };
            _pulsers.Add(st);
        }
    }

    void UpdatePulse(float tNow)
    {
        float a = (Mathf.Sin(tNow * Mathf.PI * 2f * pulseSpeed) * 0.5f + 0.5f);
        float intensity = Mathf.Lerp(pulseMin, pulseMax, a);
        Color c = pulseColor * intensity;

        for (int i = 0; i < _pulsers.Count; i++)
        {
            var ps = _pulsers[i];
            if (!ps.r) continue;

            ps.r.GetPropertyBlock(ps.mpb);

            if (ps.hasEmission && !string.IsNullOrEmpty(ps.emissionProp))
            {
                ps.mpb.SetColor(ps.emissionProp, c);
                ps.r.SetPropertyBlock(ps.mpb);
            }
            else
            {
                if (TrySetBaseColor(ps.r, ps.mpb, c))
                    ps.r.SetPropertyBlock(ps.mpb);
            }
        }
    }

    bool TrySetBaseColor(Renderer r, MaterialPropertyBlock mpb, Color c)
    {
        string[] props = { "_BaseColor", "_Color", "_TintColor" };
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (!m) continue;
            for (int p = 0; p < props.Length; p++)
            {
                if (m.HasProperty(props[p]))
                {
                    mpb.SetColor(props[p], c);
                    return true;
                }
            }
        }
        return false;
    }

    void ClearPulse()
    {
        for (int i = 0; i < _pulsers.Count; i++)
        {
            var ps = _pulsers[i];
            if (!ps.r) continue;
            ps.r.SetPropertyBlock(null);
        }
        _pulsers.Clear();
    }
}
