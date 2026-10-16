using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class AppearanceStyle
{
    public string displayName;
    public Sprite icon;
    public List<Sprite> frames = new List<Sprite>();
}

[CreateAssetMenu(fileName = "NewStyleSet", menuName = "Character/Appearance Style Set")]
public class AppearanceStyleSet : ScriptableObject
{
    [SerializeField] private List<AppearanceStyle> styles = new List<AppearanceStyle>();

    public int Count => styles.Count;

    public AppearanceStyle Get(int index) =>
        index >= 0 && index < styles.Count ? styles[index] : null;

    public Sprite IconFor(int index)
    {
        var style = Get(index);
        if (style == null) return null;
        if (style.icon != null) return style.icon;
        return style.frames.Count > 0 ? style.frames[0] : null;
    }

    public Dictionary<Sprite, Sprite> BuildMap(int index)
    {
        if (index < 0 || index >= styles.Count) return null;

        var target = styles[index].frames;
        var map = new Dictionary<Sprite, Sprite>();

        foreach (var style in styles)
        {
            int count = Mathf.Min(style.frames.Count, target.Count);
            for (int i = 0; i < count; i++)
            {
                var from = style.frames[i];
                if (from != null && target[i] != null && from != target[i])
                    map[from] = target[i];
            }
        }

        if (styles.Count > 0 && styles[0].frames.Count != target.Count)
            Debug.LogWarning($"Style '{styles[index].displayName}' in '{name}' has {target.Count} frames but the base style has {styles[0].frames.Count}", this);

        return map;
    }

    [ContextMenu("Sort Frames By Name")]
    private void SortFramesByName()
    {
        foreach (var style in styles)
            style.frames.Sort(CompareNatural);
    }

    private static int CompareNatural(Sprite a, Sprite b)
    {
        if (a == null || b == null) return a == b ? 0 : a == null ? 1 : -1;

        var numberA = Regex.Match(a.name, @"(\d+)$");
        var numberB = Regex.Match(b.name, @"(\d+)$");
        string prefixA = numberA.Success ? a.name.Substring(0, numberA.Index) : a.name;
        string prefixB = numberB.Success ? b.name.Substring(0, numberB.Index) : b.name;

        int prefix = string.CompareOrdinal(prefixA, prefixB);
        if (prefix != 0 || !numberA.Success || !numberB.Success) return prefix != 0 ? prefix : string.CompareOrdinal(a.name, b.name);

        return int.Parse(numberA.Value).CompareTo(int.Parse(numberB.Value));
    }
}