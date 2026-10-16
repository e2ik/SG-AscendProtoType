using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(-100)]
public class SpriteStyleSwapper : MonoBehaviour
{
    [SerializeField] private AppearanceStyleSet styleSet;
    [SerializeField] [Min(0)] private int styleIndex;

    public AppearanceStyleSet StyleSet => styleSet;
    public int StyleIndex => styleIndex;

    private SpriteRenderer _renderer;
    private Dictionary<Sprite, Sprite> _map;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        Rebuild();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _renderer != null)
            Rebuild();
    }

    public void SetStyle(int index)
    {
        int max = styleSet != null ? Mathf.Max(0, styleSet.Count - 1) : 0;
        styleIndex = Mathf.Clamp(index, 0, max);
        Rebuild();
        Swap();
    }

    private void Rebuild()
    {
        _map = styleSet != null ? styleSet.BuildMap(styleIndex) : null;
    }

    private void LateUpdate() => Swap();

    private void Swap()
    {
        if (_map == null || _renderer == null || _renderer.sprite == null) return;

        if (_map.TryGetValue(_renderer.sprite, out var swapped))
            _renderer.sprite = swapped;
    }
}