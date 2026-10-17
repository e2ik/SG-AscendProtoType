using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PixelWaterController : MonoBehaviour
{
    [Header("Waves")]
    [SerializeField] [Min(0f)] private float amplitude = 3f;
    [SerializeField] [Min(0f)] private float frequency = 2f;
    [SerializeField] private float waveSpeed = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float secondaryWave = 0.5f;
    [SerializeField] [Min(0f)] private float smoothTime = 0.5f;

    [Header("Random Amplitude")]
    [SerializeField] private bool randomiseAmplitude = true;
    [SerializeField] private Vector2 amplitudeMultiplierRange = new Vector2(0.5f, 1.5f);
    [SerializeField] private Vector2 changeIntervalRange = new Vector2(2f, 5f);

    [Header("Scroll")]
    [SerializeField] private float scrollSpeed = 0f;
    [SerializeField] private EndlessRunnerGame runner;
    [SerializeField] private float runnerSpeedFactor = 1f;

    [Header("Waterline")]
    [SerializeField] private bool autoWaterline = true;
    [SerializeField] [Min(0f)] private float extraPadding;

    [Header("Pixels")]
    [SerializeField] [Min(0f)] private float pixelsPerUnitOverride;

    private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");
    private static readonly int FrequencyId = Shader.PropertyToID("_Frequency");
    private static readonly int SecondaryId = Shader.PropertyToID("_SecondaryWave");
    private static readonly int PhaseId = Shader.PropertyToID("_WavePhase");
    private static readonly int ScrollId = Shader.PropertyToID("_ScrollOffset");
    private static readonly int PpuId = Shader.PropertyToID("_PixelsPerUnit");
    private static readonly int TopId = Shader.PropertyToID("_TopY");
    private static readonly int SurfaceOffsetId = Shader.PropertyToID("_SurfaceOffset");
    private static readonly int BackRiseId = Shader.PropertyToID("_BackRise");
    private static readonly int BackAmplitudeScaleId = Shader.PropertyToID("_BackAmplitudeScale");

    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _block;
    private float _amplitude;
    private float _frequency;
    private float _speed;
    private float _secondary;
    private float _phase;
    private float _scroll;
    private bool _initialised;
    private float _cameraPixelsPerUnit;
    private float _randomMultiplier = 1f;
    private float _nextRandomTime;

    public float Amplitude { get => amplitude; set => amplitude = Mathf.Max(0f, value); }
    public float Frequency { get => frequency; set => frequency = Mathf.Max(0f, value); }
    public float WaveSpeed { get => waveSpeed; set => waveSpeed = value; }
    public float SecondaryWave { get => secondaryWave; set => secondaryWave = Mathf.Clamp01(value); }
    public float ScrollSpeed { get => scrollSpeed; set => scrollSpeed = value; }

    public void SetWaves(float newAmplitude, float newFrequency, float newSpeed)
    {
        Amplitude = newAmplitude;
        Frequency = newFrequency;
        WaveSpeed = newSpeed;
    }

    private float TargetAmplitude => randomiseAmplitude ? amplitude * _randomMultiplier : amplitude;

    private float MaxMultiplier => randomiseAmplitude ? Mathf.Max(amplitudeMultiplierRange.x, amplitudeMultiplierRange.y, 1f) : 1f;

    private void UpdateRandomAmplitude()
    {
        if (!randomiseAmplitude || !Application.isPlaying)
        {
            _randomMultiplier = 1f;
            return;
        }

        if (Time.time < _nextRandomTime) return;

        float low = Mathf.Min(amplitudeMultiplierRange.x, amplitudeMultiplierRange.y);
        float high = Mathf.Max(amplitudeMultiplierRange.x, amplitudeMultiplierRange.y);
        _randomMultiplier = Random.Range(low, high);

        float minWait = Mathf.Max(0.1f, Mathf.Min(changeIntervalRange.x, changeIntervalRange.y));
        float maxWait = Mathf.Max(minWait, Mathf.Max(changeIntervalRange.x, changeIntervalRange.y));
        _nextRandomTime = Time.time + Random.Range(minWait, maxWait);
    }

    public void SnapToTargets()
    {
        _amplitude = TargetAmplitude;
        _frequency = frequency;
        _speed = waveSpeed;
        _secondary = secondaryWave;
    }

    private void OnEnable()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _block ??= new MaterialPropertyBlock();
        CacheCameraPixelsPerUnit();
        SnapToTargets();
        _initialised = true;
        Apply();
    }

    private void OnDisable()
    {
        if (_renderer != null)
            _renderer.SetPropertyBlock(null);
    }

    private void Update()
    {
        if (!_initialised) OnEnable();

        float dt = Application.isPlaying ? Time.deltaTime : 0f;
        UpdateRandomAmplitude();

        if (!Application.isPlaying || smoothTime <= 0f)
        {
            SnapToTargets();
        }
        else
        {
            float k = 1f - Mathf.Exp(-dt / smoothTime);
            _amplitude = Mathf.Lerp(_amplitude, TargetAmplitude, k);
            _frequency = Mathf.Lerp(_frequency, frequency, k);
            _speed = Mathf.Lerp(_speed, waveSpeed, k);
            _secondary = Mathf.Lerp(_secondary, secondaryWave, k);
        }

        float speed = scrollSpeed;
        if (runner != null)
            speed = runner.CurrentPhase == MinigameBase.Phase.Playing ? runner.Speed * runnerSpeedFactor : 0f;

        _phase += _speed * Mathf.PI * 2f * dt;
        _scroll += speed * dt * PixelsPerUnit;

        if (_phase > 10000f) _phase -= Mathf.PI * 2f * 1000f;

        Apply();
    }

    private void CacheCameraPixelsPerUnit()
    {
        var pixelCamera = FindFirstObjectByType<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        _cameraPixelsPerUnit = pixelCamera != null ? pixelCamera.assetsPPU : 0f;
    }

    private float PixelsPerUnit
    {
        get
        {
            if (pixelsPerUnitOverride > 0f) return pixelsPerUnitOverride;
            if (_cameraPixelsPerUnit > 0f) return _cameraPixelsPerUnit;
            return _renderer != null && _renderer.sprite != null ? _renderer.sprite.pixelsPerUnit : 16f;
        }
    }

    private float AutoWaterline()
    {
        var material = _renderer.sharedMaterial;
        float backRise = material != null && material.HasProperty(BackRiseId) ? material.GetFloat(BackRiseId) : 0f;
        float backScale = material != null && material.HasProperty(BackAmplitudeScaleId) ? material.GetFloat(BackAmplitudeScaleId) : 1f;

        float maxAmplitude = Mathf.Max(amplitude * MaxMultiplier, _amplitude);
        float frontCrest = Mathf.Ceil(maxAmplitude);
        float backCrest = Mathf.Ceil(maxAmplitude * backScale) + backRise;
        return Mathf.Max(frontCrest, backCrest) + extraPadding;
    }

    private void Apply()
    {
        if (_renderer == null) return;

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(AmplitudeId, _amplitude);
        _block.SetFloat(FrequencyId, _frequency);
        _block.SetFloat(SecondaryId, _secondary);
        _block.SetFloat(PhaseId, _phase);
        _block.SetFloat(ScrollId, _scroll);
        _block.SetFloat(PpuId, PixelsPerUnit);
        _block.SetFloat(TopId, _renderer.bounds.max.y);

        if (autoWaterline)
            _block.SetFloat(SurfaceOffsetId, AutoWaterline());

        _renderer.SetPropertyBlock(_block);
    }
}