using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public class UIMultiplyOutline : MonoBehaviour
{
    public enum PixelScale { Quarter, Third, Half, Full, Double, Triple, Quadruple }

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private List<Image> sources = new List<Image>();
    [SerializeField] private Color tint = Color.white;
    [SerializeField, Range(0f, 1f)] private float strength = 0.5f;
    [SerializeField, Range(0f, 1f)] private float falloff = 0.5f;
    [SerializeField, Range(1, 8)] private int rings = 3;
    [SerializeField] private PixelScale pixelSize = PixelScale.Full;
    [SerializeField, Range(0f, 1f)] private float checkerContrast = 1f;

    [Header("Line")]
    [SerializeField] private bool showLine = true;
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float lineStrength = 1f;
    [SerializeField] private bool lineDiagonals = true;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int RingsId = Shader.PropertyToID("_Rings");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int CheckerId = Shader.PropertyToID("_Checker");
    private static readonly int LineEnabledId = Shader.PropertyToID("_LineEnabled");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int LineStrengthId = Shader.PropertyToID("_LineStrength");
    private static readonly int LineDiagonalsId = Shader.PropertyToID("_LineDiagonals");

    private const string ChildName = "UIMultiplyOutline";

    private readonly List<Image> _activeSources = new List<Image>();
    private readonly List<Image> _outlines = new List<Image>();
    private Material _material;
    private bool _rebuildPending;

    private void OnEnable()
    {
        if (_outlines.Count == 0 || _outlines.Exists(o => o == null))
        {
            _rebuildPending = true;
            return;
        }

        Sync();
    }

    private void OnDisable()
    {
        foreach (var outline in _outlines)
        {
            if (outline != null)
                outline.enabled = false;
        }
    }

    private void OnDestroy()
    {
        DestroyOutlines();
    }

    private void DestroyOutlines()
    {
        foreach (var outline in _outlines)
        {
            if (outline == null) continue;
            if (Application.isPlaying) Destroy(outline.gameObject);
            else DestroyImmediate(outline.gameObject);
        }

        _outlines.Clear();
        _activeSources.Clear();

        if (_material != null)
        {
            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
            _material = null;
        }
    }

    private void Rebuild()
    {
        DestroyOutlines();

        if (sources.Count == 0)
        {
            var own = GetComponent<Image>();
            if (own != null) _activeSources.Add(own);
        }
        else
        {
            foreach (var source in sources)
            {
                if (source != null && !_activeSources.Contains(source))
                    _activeSources.Add(source);
            }
        }

        foreach (var source in _activeSources)
            _outlines.Add(CreateOutline(source));
    }

    private static Image CreateOutline(Image source)
    {
        var parent = source.transform.parent;
        if (parent != null)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name != ChildName + "_" + source.name) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        var go = new GameObject(ChildName + "_" + source.name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
        go.layer = source.gameObject.layer;
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void LateUpdate()
    {
        if (_rebuildPending)
        {
            _rebuildPending = false;
            Rebuild();
        }

        Sync();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;

        if (_outlines.Count == 0 || !SourcesMatch())
        {
            _rebuildPending = true;
            return;
        }

        Sync();
    }

    private bool SourcesMatch()
    {
        int expected = 0;
        foreach (var source in sources)
            if (source != null) expected++;

        if (sources.Count == 0) expected = GetComponent<Image>() != null ? 1 : 0;
        return expected == _activeSources.Count;
    }

    private static float PixelSizeValue(PixelScale scale)
    {
        switch (scale)
        {
            case PixelScale.Quarter: return 0.25f;
            case PixelScale.Third: return 1f / 3f;
            case PixelScale.Half: return 0.5f;
            case PixelScale.Double: return 2f;
            case PixelScale.Triple: return 3f;
            case PixelScale.Quadruple: return 4f;
            default: return 1f;
        }
    }

    private void UpdateMaterial()
    {
        if (outlineMaterial == null)
            return;

        if (_material == null || _material.shader != outlineMaterial.shader)
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }

            _material = new Material(outlineMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }

        _material.SetColor(OutlineColorId, tint);
        _material.SetFloat(StrengthId, strength);
        _material.SetFloat(FalloffId, falloff);
        _material.SetFloat(RingsId, rings);
        _material.SetFloat(PixelSizeId, PixelSizeValue(pixelSize));
        _material.SetFloat(CheckerId, checkerContrast);
        _material.SetFloat(LineEnabledId, showLine ? 1f : 0f);
        _material.SetColor(LineColorId, lineColor);
        _material.SetFloat(LineStrengthId, lineStrength);
        _material.SetFloat(LineDiagonalsId, lineDiagonals ? 1f : 0f);
    }

    private int LowestSiblingIndex(Transform parent)
    {
        int lowest = int.MaxValue;
        foreach (var source in _activeSources)
        {
            if (source != null && source.transform.parent == parent)
                lowest = Mathf.Min(lowest, source.transform.GetSiblingIndex());
        }
        return lowest == int.MaxValue ? 0 : lowest;
    }

    private void Sync()
    {
        UpdateMaterial();

        for (int i = 0; i < _activeSources.Count && i < _outlines.Count; i++)
        {
            var source = _activeSources[i];
            var outline = _outlines[i];
            if (source == null || outline == null) continue;

            var sourceRect = source.rectTransform;
            var outlineRect = outline.rectTransform;

            if (outlineRect.parent != sourceRect.parent)
                outlineRect.SetParent(sourceRect.parent, false);

            outlineRect.anchorMin = sourceRect.anchorMin;
            outlineRect.anchorMax = sourceRect.anchorMax;
            outlineRect.pivot = sourceRect.pivot;
            outlineRect.anchoredPosition = sourceRect.anchoredPosition;
            outlineRect.sizeDelta = sourceRect.sizeDelta;
            outlineRect.localRotation = sourceRect.localRotation;
            outlineRect.localScale = sourceRect.localScale;

            int index = LowestSiblingIndex(sourceRect.parent);
            int current = outlineRect.GetSiblingIndex();
            if (current > index)
                outlineRect.SetSiblingIndex(index);

            outline.material = _material;
            outline.sprite = source.sprite;
            outline.type = source.type;
            outline.preserveAspect = source.preserveAspect;
            outline.useSpriteMesh = source.useSpriteMesh;
            outline.fillMethod = source.fillMethod;
            outline.fillAmount = source.fillAmount;
            outline.fillOrigin = source.fillOrigin;
            outline.fillClockwise = source.fillClockwise;
            outline.color = new Color(1f, 1f, 1f, source.color.a);
            outline.enabled = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null && _material != null;
        }
    }
}