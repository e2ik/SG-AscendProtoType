using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public class MultiplyOutline : MonoBehaviour
{
    public enum PixelScale { Quarter, Third, Half, Full, Double, Triple, Quadruple }

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private List<SpriteRenderer> sources = new List<SpriteRenderer>();
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

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = -1;

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

    private const string ChildName = "MultiplyOutline";

    private readonly List<SpriteRenderer> _activeSources = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _outlines = new List<SpriteRenderer>();
    private MaterialPropertyBlock _block;
    private bool _rebuildPending;

    private void OnEnable()
    {
        _block ??= new MaterialPropertyBlock();
        Rebuild();
        Sync();
    }

    private void OnDisable()
    {
        foreach (var outline in _outlines)
        {
            if (outline == null) continue;
            if (Application.isPlaying) Destroy(outline.gameObject);
            else DestroyImmediate(outline.gameObject);
        }

        _outlines.Clear();
        _activeSources.Clear();
    }

    private void Rebuild()
    {
        OnDisable();

        if (sources.Count == 0)
        {
            var own = GetComponent<SpriteRenderer>();
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
            _outlines.Add(CreateChild(source.transform));
    }

    private static SpriteRenderer CreateChild(Transform parent)
    {
        var existing = parent.Find(ChildName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        var child = new GameObject(ChildName) { hideFlags = HideFlags.HideAndDontSave };
        child.transform.SetParent(parent, false);
        child.layer = parent.gameObject.layer;
        return child.AddComponent<SpriteRenderer>();
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

        if (!SourcesMatch())
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

        if (sources.Count == 0) expected = GetComponent<SpriteRenderer>() != null ? 1 : 0;
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

    private int LowestSortingOrder()
    {
        int lowest = int.MaxValue;
        foreach (var source in _activeSources)
        {
            if (source != null && source.sortingOrder < lowest)
                lowest = source.sortingOrder;
        }
        return lowest == int.MaxValue ? 0 : lowest;
    }

    private void Sync()
    {
        _block ??= new MaterialPropertyBlock();
        int order = LowestSortingOrder() + sortingOrderOffset;

        for (int i = 0; i < _activeSources.Count && i < _outlines.Count; i++)
        {
            var source = _activeSources[i];
            var outline = _outlines[i];
            if (source == null || outline == null) continue;

            outline.sharedMaterial = outlineMaterial;
            outline.sprite = source.sprite;
            outline.flipX = source.flipX;
            outline.flipY = source.flipY;
            outline.enabled = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null && outlineMaterial != null;
            outline.sortingLayerID = source.sortingLayerID;
            outline.sortingOrder = order;
            outline.drawMode = source.drawMode;
            if (source.drawMode != SpriteDrawMode.Simple) outline.size = source.size;
            outline.color = new Color(1f, 1f, 1f, source.color.a);

            outline.GetPropertyBlock(_block);
            _block.SetColor(OutlineColorId, tint);
            _block.SetFloat(StrengthId, strength);
            _block.SetFloat(FalloffId, falloff);
            _block.SetFloat(RingsId, rings);
            _block.SetFloat(PixelSizeId, PixelSizeValue(pixelSize));
            _block.SetFloat(CheckerId, checkerContrast);
            _block.SetFloat(LineEnabledId, showLine ? 1f : 0f);
            _block.SetColor(LineColorId, lineColor);
            _block.SetFloat(LineStrengthId, lineStrength);
            _block.SetFloat(LineDiagonalsId, lineDiagonals ? 1f : 0f);
            outline.SetPropertyBlock(_block);
        }
    }
}