using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Collider2D))]
public class FogRevealZone : MonoBehaviour
{
    private const int MaxZones = 16;

    [SerializeField] private string revealFlag = "tier1_west_reached";
    [SerializeField] private bool setFlagOnEnter = true;
    [SerializeField] [Min(0f)] private float revealDuration = 2f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool revealOnReturn = true;
    [SerializeField] [Min(0f)] private float glowFadeIn = 0.3f;
    [SerializeField] [Min(0f)] private float glowFadeOut = 1.2f;

    [Header("Shape")]
    [SerializeField] private float expand = 1f;
    [SerializeField] [Min(0f)] private float cornerRadius = 1.5f;
    [SerializeField] [Min(0.01f)] private float edgeSoftness = 1.5f;
    [SerializeField] [Min(0f)] private float edgeNoise = 0.8f;

    [Header("Editor")]
    [SerializeField] private bool previewRevealed;
    [SerializeField] private Color gizmoColour = new Color(0.6f, 0.9f, 1f, 0.5f);

    private static readonly List<FogRevealZone> Zones = new List<FogRevealZone>();
    private static readonly Vector4[] ZoneShapes = new Vector4[MaxZones];
    private static readonly Vector4[] ZoneData = new Vector4[MaxZones];
    private static readonly Vector4[] ZoneGlow = new Vector4[MaxZones];
    private static readonly int CountId = Shader.PropertyToID("_FogZoneCount");
    private static readonly int ShapesId = Shader.PropertyToID("_FogZones");
    private static readonly int DataId = Shader.PropertyToID("_FogZoneData");
    private static readonly int GlowId = Shader.PropertyToID("_FogZoneGlow");
    private static int _uploadedFrame = -1;

    public bool IsRevealed => _reveal >= 1f;

    private Collider2D _collider;
    private readonly List<Collider2D> _shapes = new List<Collider2D>();
    private float _reveal;
    private float _glow;
    private bool _revealing;
    private bool _seenUnrevealed;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        CollectShapes();
    }

    private void CollectShapes()
    {
        _shapes.Clear();
        if (_collider == null) _collider = GetComponent<Collider2D>();

        foreach (var child in GetComponentsInChildren<Collider2D>(true))
        {
            if (child == _collider) continue;
            if (Application.isPlaying)
                child.isTrigger = true;
            _shapes.Add(child);
        }
    }

    private void OnEnable()
    {
        if (_collider == null) _collider = GetComponent<Collider2D>();
        if (!Zones.Contains(this)) Zones.Add(this);
        if (!Application.isPlaying) return;

        SaveData.FlagChanged += HandleFlagChanged;

        if (_reveal >= 1f || _revealing) return;

        if (IsFlagSet())
        {
            if (revealOnReturn && _seenUnrevealed)
                BeginReveal();
            else
                _reveal = 1f;
        }
        else
        {
            _seenUnrevealed = true;
        }
    }

    private void OnDisable()
    {
        Zones.Remove(this);
        if (Application.isPlaying)
            SaveData.FlagChanged -= HandleFlagChanged;
        Upload();
    }

    private void Reset()
    {
        var shape = GetComponent<Collider2D>();
        if (shape != null) shape.isTrigger = true;
    }

    private void LateUpdate()
    {
        if (_uploadedFrame == Time.frameCount && Application.isPlaying) return;
        Upload();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CollectShapes();
            Upload();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!setFlagOnEnter || _reveal >= 1f || _revealing) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(revealFlag))
        {
            save.SetFlag(revealFlag, true);
            SaveManager.Instance.SaveCurrent();
        }

        if (!_revealing && _reveal < 1f)
            BeginReveal();
    }

    private bool IsFlagSet()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && !string.IsNullOrEmpty(revealFlag) && save.GetFlag(revealFlag);
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (!value || key != revealFlag || _reveal >= 1f || _revealing) return;
        BeginReveal();
    }

    private void BeginReveal()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        _revealing = true;
        for (float t = 0f; t < revealDuration; t += Time.deltaTime)
        {
            _reveal = Mathf.Max(0.001f, revealCurve.Evaluate(Mathf.Clamp01(t / revealDuration)));
            _glow = glowFadeIn > 0f ? Mathf.Clamp01(t / glowFadeIn) : 1f;
            yield return null;
        }
        _reveal = 1f;
        _revealing = false;

        float start = _glow;
        for (float t = 0f; t < glowFadeOut; t += Time.deltaTime)
        {
            float p = t / glowFadeOut;
            _glow = start * (1f - p) * (1f - p);
            yield return null;
        }
        _glow = 0f;
    }

    private float CurrentReveal => Application.isPlaying ? _reveal : (previewRevealed ? 1f : 0f);

    private static bool TryGetShape(Collider2D shapeCollider, float cornerRadius, out Vector4 shape, out float radius)
    {
        shape = Vector4.zero;
        radius = cornerRadius;
        if (shapeCollider == null) return false;

        var bounds = shapeCollider.bounds;
        if (bounds.extents.x <= 0f && bounds.extents.y <= 0f) return false;

        shape = new Vector4(bounds.center.x, bounds.center.y, bounds.extents.x, bounds.extents.y);
        if (shapeCollider is CircleCollider2D)
            radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        return true;
    }

    private static void Upload()
    {
        _uploadedFrame = Time.frameCount;
        int count = 0;

        foreach (var zone in Zones)
        {
            if (zone == null) continue;
            if (!Application.isPlaying) zone.CollectShapes();

            foreach (var shapeCollider in zone._shapes)
            {
                if (count >= MaxZones) break;
                if (!TryGetShape(shapeCollider, zone.cornerRadius, out var shape, out float radius)) continue;

                ZoneShapes[count] = shape;
                ZoneData[count] = new Vector4(zone.CurrentReveal, radius, zone.edgeSoftness, zone.edgeNoise);
                ZoneGlow[count] = new Vector4(Application.isPlaying ? zone._glow : 0f, zone.expand, 0f, 0f);
                count++;
            }
        }

        for (int i = count; i < MaxZones; i++)
        {
            ZoneShapes[i] = Vector4.zero;
            ZoneData[i] = Vector4.zero;
            ZoneGlow[i] = Vector4.zero;
        }

        Shader.SetGlobalFloat(CountId, count);
        Shader.SetGlobalVectorArray(ShapesId, ZoneShapes);
        Shader.SetGlobalVectorArray(DataId, ZoneData);
        Shader.SetGlobalVectorArray(GlowId, ZoneGlow);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) CollectShapes();
        Gizmos.color = gizmoColour;
        foreach (var shapeCollider in _shapes)
        {
            if (!TryGetShape(shapeCollider, cornerRadius, out var shape, out _)) continue;
            Gizmos.color = gizmoColour;
            Gizmos.DrawWireCube(new Vector3(shape.x, shape.y, 0f), new Vector3(shape.z * 2f, shape.w * 2f, 0f));
            if (Mathf.Abs(expand) > 0.01f)
            {
                var faded = gizmoColour;
                faded.a *= 0.4f;
                Gizmos.color = faded;
                Gizmos.DrawWireCube(new Vector3(shape.x, shape.y, 0f), new Vector3(Mathf.Max(0f, shape.z + expand) * 2f, Mathf.Max(0f, shape.w + expand) * 2f, 0f));
            }
        }

        if (_collider != null && TryGetShape(_collider, 0f, out var trigger, out _))
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(trigger.x, trigger.y, 0f), new Vector3(trigger.z * 2f, trigger.w * 2f, 0f));
        }
    }
}