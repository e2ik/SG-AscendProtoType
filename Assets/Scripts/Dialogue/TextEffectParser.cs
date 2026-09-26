using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public enum TextEffectType
{
    Shake,
    Wave,
    Rainbow,
    Pulse,
    Flicker,
    Speed
}

public struct TextEffectRange
{
    public TextEffectType Type;
    public int Start;
    public int End;
    public float Strength;

    public bool Contains(int index) => index >= Start && index < End;
}

public struct TextPause
{
    public int Index;
    public float Duration;
}

public struct TextExpressionChange
{
    public int Index;
    public string Expression;
}

public static class TextEffectParser
{
    private const float DefaultPauseDuration = 0.5f;
    private const float DefaultSpeedMultiplier = 0.5f;

    public static string Parse(string raw, List<TextEffectRange> ranges, List<TextPause> pauses,
        List<TextExpressionChange> expressions = null, Predicate<string> isExpression = null)
    {
        ranges.Clear();
        pauses.Clear();
        expressions?.Clear();
        if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;

        var output = new StringBuilder(raw.Length);
        var open = new Dictionary<TextEffectType, (int start, float strength)>();
        int characterIndex = 0;
        int i = 0;

        while (i < raw.Length)
        {
            if (raw[i] == '<')
            {
                int close = raw.IndexOf('>', i + 1);
                if (close > i)
                {
                    string tag = raw.Substring(i + 1, close - i - 1);

                    if (!TryHandleTag(tag, characterIndex, open, ranges, pauses, expressions, isExpression))
                        output.Append(raw, i, close - i + 1);

                    i = close + 1;
                    continue;
                }
            }

            output.Append(raw[i]);
            characterIndex++;
            i++;
        }

        foreach (var pair in open)
            ranges.Add(new TextEffectRange { Type = pair.Key, Start = pair.Value.start, End = characterIndex, Strength = pair.Value.strength });

        pauses.Sort((a, b) => a.Index.CompareTo(b.Index));
        return output.ToString();
    }

    private static bool TryHandleTag(string tag, int index, Dictionary<TextEffectType, (int start, float strength)> open,
        List<TextEffectRange> ranges, List<TextPause> pauses, List<TextExpressionChange> expressions, Predicate<string> isExpression)
    {
        bool closing = tag.StartsWith("/");
        string body = closing ? tag.Substring(1) : tag;

        string name = body;
        string value = null;

        int equals = body.IndexOf('=');
        if (equals >= 0)
        {
            name = body.Substring(0, equals);
            value = body.Substring(equals + 1);
        }

        name = name.Trim().ToLowerInvariant();

        if (name == "pause")
        {
            if (!closing)
                pauses.Add(new TextPause { Index = index, Duration = ParseOr(value, DefaultPauseDuration) });
            return true;
        }

        if (!TryGetType(name, out var type))
        {
            if (expressions == null || isExpression == null || !isExpression(name)) return false;

            expressions.Add(new TextExpressionChange { Index = index, Expression = closing ? string.Empty : name });
            return true;
        }

        if (closing)
        {
            if (open.TryGetValue(type, out var start))
            {
                ranges.Add(new TextEffectRange { Type = type, Start = start.start, End = index, Strength = start.strength });
                open.Remove(type);
            }
        }
        else if (!open.ContainsKey(type))
        {
            float fallback = type == TextEffectType.Speed ? DefaultSpeedMultiplier : 1f;
            open[type] = (index, ParseOr(value, fallback));
        }

        return true;
    }

    private static float ParseOr(string value, float fallback)
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static bool TryGetType(string name, out TextEffectType type)
    {
        switch (name)
        {
            case "shake": type = TextEffectType.Shake; return true;
            case "wave": type = TextEffectType.Wave; return true;
            case "rainbow": type = TextEffectType.Rainbow; return true;
            case "pulse": type = TextEffectType.Pulse; return true;
            case "flicker": type = TextEffectType.Flicker; return true;
            case "speed": type = TextEffectType.Speed; return true;
            default: type = default; return false;
        }
    }
}