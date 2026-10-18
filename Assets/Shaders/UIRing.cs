using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
[DisallowMultipleComponent]
public class UIRing : MonoBehaviour, IMaterialModifier
{
    [SerializeField] [Range(0f, 1f)] private float innerRadius = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float outerRadius = 0.8f;
    [SerializeField] [Min(0f)] private float pixelResolution = 64f;
    [SerializeField] private Color edgeColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] [Min(0f)] private float edgeWidth = 1f;

    [Header("Dither")]
    [SerializeField] [Min(0f)] private float ditherEdge = 2f;
    [SerializeField] private bool ditherInnerEdge = true;
    [SerializeField] private bool ditherOuterEdge = true;
    [SerializeField] private bool ditherAlpha = true;
    [SerializeField] [Min(0f)] private float ditherScale;
    [SerializeField] [Range(-1f, 1f)] private float radialFade;

    private static readonly int InnerId = Shader.PropertyToID("_InnerRadius");
    private static readonly int OuterId = Shader.PropertyToID("_OuterRadius");
    private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int DitherEdgeId = Shader.PropertyToID("_DitherEdge");
    private static readonly int DitherInnerId = Shader.PropertyToID("_DitherInner");
    private static readonly int DitherOuterId = Shader.PropertyToID("_DitherOuter");
    private static readonly int DitherAlphaId = Shader.PropertyToID("_DitherAlpha");
    private static readonly int DitherScaleId = Shader.PropertyToID("_DitherScale");
    private static readonly int RadialFadeId = Shader.PropertyToID("_RadialFade");

    private Graphic _graphic;
    private Material _instance;

    public float InnerRadius => innerRadius;
    public float OuterRadius => outerRadius;

    public float PixelResolution
    {
        get => pixelResolution;
        set { pixelResolution = Mathf.Max(0f, value); Refresh(); }
    }

    public float DitherEdge
    {
        get => ditherEdge;
        set { ditherEdge = Mathf.Max(0f, value); Refresh(); }
    }

    public float RadialFade
    {
        get => radialFade;
        set { radialFade = Mathf.Clamp(value, -1f, 1f); Refresh(); }
    }

    public Graphic Graphic => _graphic != null ? _graphic : _graphic = GetComponent<Graphic>();

    private void OnEnable() => Graphic.SetMaterialDirty();

    private void OnDisable()
    {
        if (_graphic != null)
            _graphic.SetMaterialDirty();

        if (_instance != null)
        {
            if (Application.isPlaying) Destroy(_instance);
            else DestroyImmediate(_instance);
            _instance = null;
        }
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled)
            Graphic.SetMaterialDirty();
    }

    public void SetRadii(float inner, float outer)
    {
        innerRadius = Mathf.Clamp01(Mathf.Min(inner, outer));
        outerRadius = Mathf.Clamp01(Mathf.Max(inner, outer));
        Refresh();
    }

    public void SetBand(float centre, float thickness)
    {
        float half = thickness * 0.5f;
        SetRadii(centre - half, centre + half);
    }

    private void Refresh()
    {
        if (_instance != null)
            Push(_instance);
        else if (isActiveAndEnabled)
            Graphic.SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || baseMaterial == null || !baseMaterial.HasProperty(InnerId))
            return baseMaterial;

        if (_instance == null || _instance.shader != baseMaterial.shader)
        {
            if (_instance != null)
            {
                if (Application.isPlaying) Destroy(_instance);
                else DestroyImmediate(_instance);
            }
            _instance = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }

        _instance.CopyPropertiesFromMaterial(baseMaterial);
        foreach (var keyword in baseMaterial.shaderKeywords)
            _instance.EnableKeyword(keyword);
        Push(_instance);
        return _instance;
    }

    private void Push(Material material)
    {
        material.SetFloat(InnerId, innerRadius);
        material.SetFloat(OuterId, outerRadius);
        material.SetFloat(ResolutionId, pixelResolution);
        material.SetColor(EdgeColorId, edgeColor);
        material.SetFloat(EdgeWidthId, edgeWidth);
        material.SetFloat(DitherEdgeId, ditherEdge);
        material.SetFloat(DitherInnerId, ditherInnerEdge ? 1f : 0f);
        material.SetFloat(DitherOuterId, ditherOuterEdge ? 1f : 0f);
        material.SetFloat(DitherAlphaId, ditherAlpha ? 1f : 0f);
        material.SetFloat(DitherScaleId, ditherScale);
        material.SetFloat(RadialFadeId, radialFade);
    }
}