using System.Collections;
using UnityEngine;

public class FogZone : MonoBehaviour
{
    public enum RevealOrigin { Player, Transform, Centre }

    [SerializeField] private string clearedByFlag = "tier1_west_reached";
    [SerializeField] private float fadeDuration = 2.5f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private SpriteRenderer[] renderers;

    [Header("Reveal")]
    [SerializeField] private RevealOrigin origin = RevealOrigin.Player;
    [SerializeField] private Transform originTransform;
    [SerializeField] [Min(0f)] private float radiusPadding = 1f;
    [SerializeField] private bool revealOnReturn = true;

    [Header("Player Hole")]
    [SerializeField] private bool playerHole = true;
    [SerializeField] [Min(0f)] private float holeRadius = 1.5f;
    [SerializeField] [Min(0.01f)] private float holeSoftness = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float holeStrength = 0.6f;

    [Header("Drift")]
    [SerializeField] private float driftDistance = 0.4f;
    [SerializeField] private float driftCyclesPerSecond = 0.1f;

    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int RevealCenterId = Shader.PropertyToID("_RevealCenter");
    private static readonly int RevealRadiusId = Shader.PropertyToID("_RevealRadius");
    private static readonly int HolePositionId = Shader.PropertyToID("_HolePosition");
    private static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    private static readonly int HoleSoftnessId = Shader.PropertyToID("_HoleSoftness");
    private static readonly int HoleStrengthId = Shader.PropertyToID("_HoleStrength");

    private float[] _baseAlphas;
    private bool[] _usesShader;
    private MaterialPropertyBlock _block;
    private Vector3 _origin;
    private bool _cleared;
    private bool _revealing;
    private bool _seenUncleared;
    private float _reveal;
    private Vector2 _revealCenter;
    private float _revealRadius = 10f;
    private Transform _player;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        _block = new MaterialPropertyBlock();
        _baseAlphas = new float[renderers.Length];
        _usesShader = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            _baseAlphas[i] = renderers[i].color.a;
            var material = renderers[i].sharedMaterial;
            _usesShader[i] = material != null && material.HasProperty(RevealId);
        }

        _origin = transform.localPosition;
    }

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;

        if (_cleared) return;

        if (IsFlagSet())
        {
            if (revealOnReturn && _seenUncleared)
                BeginReveal();
            else
                ClearInstantly();
        }
        else
        {
            _seenUncleared = true;
            ApplyShader();
        }
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;

        if (_revealing)
        {
            _revealing = false;
            ClearInstantly();
        }
    }

    private void Update()
    {
        if (_cleared && !_revealing) return;

        if (driftDistance > 0f)
        {
            float offset = Mathf.Sin(Time.time * driftCyclesPerSecond * Mathf.PI * 2f) * driftDistance;
            transform.localPosition = _origin + Vector3.right * offset;
        }

        ApplyShader();
    }

    private bool IsFlagSet()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && !string.IsNullOrEmpty(clearedByFlag) && save.GetFlag(clearedByFlag);
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (_cleared || !value || key != clearedByFlag) return;
        BeginReveal();
    }

    private void BeginReveal()
    {
        if (!isActiveAndEnabled)
        {
            ClearInstantly();
            return;
        }

        _cleared = true;
        _revealing = true;
        _revealCenter = ResolveCentre();
        _revealRadius = ResolveRadius(_revealCenter);
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float p = revealCurve.Evaluate(Mathf.Clamp01(t / fadeDuration));
            _reveal = p;
            SetAlpha(1f - p, true);
            yield return null;
        }

        _revealing = false;
        ClearInstantly();
    }

    private void ClearInstantly()
    {
        _cleared = true;
        _reveal = 1f;
        SetAlpha(0f, true);
        ApplyShader();
        foreach (var r in renderers)
            r.enabled = false;
    }

    private Vector2 ResolveCentre()
    {
        switch (origin)
        {
            case RevealOrigin.Player:
                var player = Player();
                if (player != null) return player.position;
                break;
            case RevealOrigin.Transform:
                if (originTransform != null) return originTransform.position;
                break;
        }

        return Bounds().center;
    }

    private float ResolveRadius(Vector2 centre)
    {
        var bounds = Bounds();
        float radius = 0f;
        radius = Mathf.Max(radius, Vector2.Distance(centre, new Vector2(bounds.min.x, bounds.min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(centre, new Vector2(bounds.min.x, bounds.max.y)));
        radius = Mathf.Max(radius, Vector2.Distance(centre, new Vector2(bounds.max.x, bounds.min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(centre, new Vector2(bounds.max.x, bounds.max.y)));
        return radius + radiusPadding;
    }

    private Bounds Bounds()
    {
        bool found = false;
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    private Transform Player()
    {
        if (_player == null)
        {
            var controller = FindFirstObjectByType<PlayerController>();
            _player = controller != null ? controller.transform : null;
        }
        return _player;
    }

    private void ApplyShader()
    {
        Vector2 holePosition = Vector2.zero;
        bool hole = playerHole && Player() != null;
        if (hole) holePosition = _player.position;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!_usesShader[i] || renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(_block);
            _block.SetFloat(RevealId, _reveal);
            _block.SetVector(RevealCenterId, _revealCenter);
            _block.SetFloat(RevealRadiusId, _revealRadius);
            _block.SetVector(HolePositionId, holePosition);
            _block.SetFloat(HoleRadiusId, hole ? holeRadius : 0f);
            _block.SetFloat(HoleSoftnessId, holeSoftness);
            _block.SetFloat(HoleStrengthId, holeStrength);
            renderers[i].SetPropertyBlock(_block);
        }
    }

    private void SetAlpha(float multiplier, bool skipShaderRenderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (skipShaderRenderers && _usesShader[i]) continue;
            var color = renderers[i].color;
            color.a = _baseAlphas[i] * multiplier;
            renderers[i].color = color;
        }
    }
}