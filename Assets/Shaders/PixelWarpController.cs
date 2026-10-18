using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class PixelWarpController : MonoBehaviour, IMaterialModifier
{
    public enum BlendStyle { Normal, Additive }

    [Header("Colour")]
    [SerializeField] [Range(0f, 1f)] private float alpha = 1f;
    [SerializeField] private Color gradientColor = Color.white;
    [SerializeField] [Range(0f, 1f)] private float gradientAmount;
    [SerializeField] [Range(-1f, 1f)] private float gradientOffset;
    [SerializeField] [Range(0f, 1f)] private float pulseAmount;
    [SerializeField] private float pulseSpeed = 1f;

    [Header("Warp")]
    [SerializeField] private Vector2 warpAmplitude = new Vector2(2f, 1f);
    [SerializeField] private float amplitudeMultiplier = 1f;
    [SerializeField] private Vector2 warpFrequency = new Vector2(3f, 2f);
    [SerializeField] private Vector2 warpSpeed = new Vector2(1.5f, 1f);
    [SerializeField] private float noiseAmount;
    [SerializeField] private float noiseScale = 4f;
    [SerializeField] private float noiseSpeed = 0.5f;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private float phase;
    [SerializeField] private bool randomPhase = true;
    [SerializeField] private bool snapWarp = true;
    [SerializeField] private bool tile;

    [Header("Dither")]
    [SerializeField] [Range(0f, 1f)] private float ditherAmount = 1f;
    [SerializeField] [Range(1, 8)] private int alphaSteps = 2;
    [SerializeField] private float ditherScale = 1f;
    [SerializeField] private bool ditherInTexture = true;

    [Header("Blending")]
    [SerializeField] private BlendStyle blend = BlendStyle.Normal;

    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
    private static readonly int GradientAmountId = Shader.PropertyToID("_GradientAmount");
    private static readonly int GradientOffsetId = Shader.PropertyToID("_GradientOffset");
    private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int WarpAmplitudeId = Shader.PropertyToID("_WarpAmplitude");
    private static readonly int WarpFrequencyId = Shader.PropertyToID("_WarpFrequency");
    private static readonly int WarpSpeedId = Shader.PropertyToID("_WarpSpeed");
    private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int ScrollId = Shader.PropertyToID("_Scroll");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int SnapWarpId = Shader.PropertyToID("_SnapWarp");
    private static readonly int TileId = Shader.PropertyToID("_Tile");
    private static readonly int DitherAmountId = Shader.PropertyToID("_DitherAmount");
    private static readonly int AlphaStepsId = Shader.PropertyToID("_AlphaSteps");
    private static readonly int DitherScaleId = Shader.PropertyToID("_DitherScale");
    private static readonly int DitherInTextureId = Shader.PropertyToID("_DitherInTexture");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

    private Graphic _graphic;
    private SpriteRenderer _spriteRenderer;
    private Material _uiInstance;
    private Material _spriteInstance;
    private Material _spriteSource;
    private MaterialPropertyBlock _block;
    private float _randomPhase;
    private bool _dirty = true;
    private Coroutine _fade;

    public float Alpha { get => alpha; set { alpha = Mathf.Clamp01(value); MarkDirty(); } }
    public float GradientAmount { get => gradientAmount; set { gradientAmount = Mathf.Clamp01(value); MarkDirty(); } }
    public Color GradientColor { get => gradientColor; set { gradientColor = value; MarkDirty(); } }
    public Vector2 WarpAmplitude { get => warpAmplitude; set { warpAmplitude = value; MarkDirty(); } }
    public float AmplitudeMultiplier { get => amplitudeMultiplier; set { amplitudeMultiplier = value; MarkDirty(); } }
    public Vector2 WarpFrequency { get => warpFrequency; set { warpFrequency = value; MarkDirty(); } }
    public Vector2 WarpSpeed { get => warpSpeed; set { warpSpeed = value; MarkDirty(); } }
    public float NoiseAmount { get => noiseAmount; set { noiseAmount = value; MarkDirty(); } }
    public Vector2 Scroll { get => scroll; set { scroll = value; MarkDirty(); } }
    public float PulseAmount { get => pulseAmount; set { pulseAmount = Mathf.Clamp01(value); MarkDirty(); } }
    public float DitherAmount { get => ditherAmount; set { ditherAmount = Mathf.Clamp01(value); MarkDirty(); } }
    public int AlphaSteps { get => alphaSteps; set { alphaSteps = Mathf.Clamp(value, 1, 8); MarkDirty(); } }
    public BlendStyle Blend { get => blend; set { blend = value; MarkDirty(); } }

    private void OnEnable()
    {
        _graphic = GetComponent<Graphic>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (randomPhase && Application.isPlaying)
            _randomPhase = Random.Range(0f, 100f);
        MarkDirty();
        Apply();
    }

    private void OnDisable()
    {
        if (_fade != null)
        {
            StopCoroutine(_fade);
            _fade = null;
        }

        if (_graphic != null)
            _graphic.SetMaterialDirty();

        if (_spriteRenderer != null)
        {
            if (_spriteInstance != null && _spriteRenderer.sharedMaterial == _spriteInstance)
                _spriteRenderer.sharedMaterial = _spriteSource;
            _spriteRenderer.SetPropertyBlock(null);
        }

        Release(ref _uiInstance);
        Release(ref _spriteInstance);
    }

    private void OnValidate() => MarkDirty();

    private void Update()
    {
        if (_dirty) Apply();
    }

    public void MarkDirty() => _dirty = true;

    public void FadeTo(float target, float duration)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeRoutine(Mathf.Clamp01(target), duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float start = alpha;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        Alpha = target;
        _fade = null;
    }

    private void Apply()
    {
        _dirty = false;

        if (_graphic != null)
        {
            _graphic.SetMaterialDirty();
            return;
        }

        if (_spriteRenderer == null) return;

        if (Application.isPlaying)
        {
            if (_spriteInstance == null)
            {
                _spriteSource = _spriteRenderer.sharedMaterial;
                if (_spriteSource == null || !_spriteSource.HasProperty(WarpAmplitudeId)) return;
                _spriteInstance = new Material(_spriteSource) { hideFlags = HideFlags.HideAndDontSave };
                _spriteRenderer.sharedMaterial = _spriteInstance;
            }
            Push(_spriteInstance, null);
        }
        else
        {
            var material = _spriteRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(WarpAmplitudeId)) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _spriteRenderer.GetPropertyBlock(_block);
            Push(null, _block);
            _spriteRenderer.SetPropertyBlock(_block);
        }
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || baseMaterial == null || !baseMaterial.HasProperty(WarpAmplitudeId))
            return baseMaterial;

        if (_uiInstance == null || _uiInstance.shader != baseMaterial.shader)
        {
            Release(ref _uiInstance);
            _uiInstance = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }

        _uiInstance.CopyPropertiesFromMaterial(baseMaterial);
        foreach (var keyword in baseMaterial.shaderKeywords)
            _uiInstance.EnableKeyword(keyword);
        Push(_uiInstance, null);
        return _uiInstance;
    }

    private void Push(Material material, MaterialPropertyBlock block)
    {
        void F(int id, float value)
        {
            if (material != null) material.SetFloat(id, value);
            else block.SetFloat(id, value);
        }

        void V(int id, Vector2 value)
        {
            var v = new Vector4(value.x, value.y, 0f, 0f);
            if (material != null) material.SetVector(id, v);
            else block.SetVector(id, v);
        }

        if (material != null) material.SetColor(ColorBId, gradientColor);
        else block.SetColor(ColorBId, gradientColor);

        F(AlphaId, alpha);
        F(GradientAmountId, gradientAmount);
        F(GradientOffsetId, gradientOffset);
        F(PulseAmountId, pulseAmount);
        F(PulseSpeedId, pulseSpeed);
        V(WarpAmplitudeId, warpAmplitude * amplitudeMultiplier);
        V(WarpFrequencyId, warpFrequency);
        V(WarpSpeedId, warpSpeed);
        F(NoiseAmountId, noiseAmount);
        F(NoiseScaleId, noiseScale);
        F(NoiseSpeedId, noiseSpeed);
        V(ScrollId, scroll);
        F(PhaseId, phase + _randomPhase);
        F(SnapWarpId, snapWarp ? 1f : 0f);
        F(TileId, tile ? 1f : 0f);
        F(DitherAmountId, ditherAmount);
        F(AlphaStepsId, alphaSteps);
        F(DitherScaleId, ditherScale);
        F(DitherInTextureId, ditherInTexture ? 1f : 0f);

        if (material != null)
        {
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            material.SetFloat(DstBlendId, blend == BlendStyle.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        }
    }

    private static void Release(ref Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
        material = null;
    }
}
