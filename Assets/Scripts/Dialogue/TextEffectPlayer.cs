using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class TextEffectPlayer : MonoBehaviour
{
    [Header("Shake")]
    [SerializeField] private float shakeAmount = 1.5f;
    [SerializeField] private float shakeSpeed = 25f;

    [Header("Wave")]
    [SerializeField] private float waveHeight = 5f;
    [SerializeField] private float waveSpeed = 6f;
    [SerializeField] private float waveSpacing = 0.5f;

    [Header("Rainbow")]
    [SerializeField] private float rainbowSpeed = 0.5f;
    [SerializeField] private float rainbowSpacing = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float rainbowSaturation = 0.8f;

    [Header("Pulse")]
    [SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseSpacing = 0.3f;

    [Header("Flicker")]
    [SerializeField] private float flickerSpeed = 12f;
    [SerializeField] [Range(0f, 1f)] private float flickerMinAlpha = 0.2f;

    private readonly List<TextEffectRange> _ranges = new List<TextEffectRange>();
    private readonly List<TextPause> _pauses = new List<TextPause>();
    private readonly List<TextExpressionChange> _expressions = new List<TextExpressionChange>();
    private TMP_Text _text;
    private bool _hasVisualEffects;

    public IReadOnlyList<TextPause> Pauses => _pauses;
    public IReadOnlyList<TextExpressionChange> Expressions => _expressions;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    public void SetText(string raw, Predicate<string> isExpression = null)
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        _text.text = TextEffectParser.Parse(raw, _ranges, _pauses, _expressions, isExpression);

        _hasVisualEffects = false;
        foreach (var range in _ranges)
        {
            if (range.Type != TextEffectType.Speed)
            {
                _hasVisualEffects = true;
                break;
            }
        }
    }

    public float GetSpeedMultiplier(int index)
    {
        float multiplier = 1f;
        foreach (var range in _ranges)
        {
            if (range.Type == TextEffectType.Speed && range.Contains(index))
                multiplier *= range.Strength;
        }
        return Mathf.Max(0.01f, multiplier);
    }

    private void LateUpdate()
    {
        if (!_hasVisualEffects || _text == null) return;

        _text.ForceMeshUpdate();
        var info = _text.textInfo;
        float time = Time.unscaledTime;
        int visibleLimit = Mathf.Min(info.characterCount, _text.maxVisibleCharacters);

        foreach (var range in _ranges)
        {
            if (range.Type == TextEffectType.Speed) continue;

            int end = Mathf.Min(range.End, visibleLimit);
            for (int i = range.Start; i < end; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible) continue;

                var mesh = info.meshInfo[character.materialReferenceIndex];
                ApplyEffect(range, i, time, mesh.vertices, mesh.colors32, character.vertexIndex);
            }
        }

        _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private void ApplyEffect(TextEffectRange range, int index, float time, Vector3[] vertices, Color32[] colors, int v)
    {
        float strength = range.Strength;

        switch (range.Type)
        {
            case TextEffectType.Shake:
            {
                float x = Mathf.PerlinNoise(index * 13.1f, time * shakeSpeed) - 0.5f;
                float y = Mathf.PerlinNoise(index * 7.7f + 100f, time * shakeSpeed) - 0.5f;
                Offset(vertices, v, new Vector3(x, y, 0f) * (2f * shakeAmount * strength));
                break;
            }

            case TextEffectType.Wave:
                Offset(vertices, v, new Vector3(0f, Mathf.Sin(time * waveSpeed + index * waveSpacing) * waveHeight * strength, 0f));
                break;

            case TextEffectType.Pulse:
            {
                float scale = 1f + Mathf.Sin(time * pulseSpeed + index * pulseSpacing) * pulseAmount * strength;
                var center = (vertices[v] + vertices[v + 2]) * 0.5f;
                for (int k = 0; k < 4; k++)
                    vertices[v + k] = center + (vertices[v + k] - center) * scale;
                break;
            }

            case TextEffectType.Rainbow:
            {
                float hue = Mathf.Repeat(time * rainbowSpeed * strength + index * rainbowSpacing, 1f);
                Color32 rainbow = Color.HSVToRGB(hue, rainbowSaturation, 1f);
                for (int k = 0; k < 4; k++)
                {
                    rainbow.a = colors[v + k].a;
                    colors[v + k] = rainbow;
                }
                break;
            }

            case TextEffectType.Flicker:
            {
                float noise = Mathf.PerlinNoise(index * 3.3f, time * flickerSpeed);
                float alphaScale = Mathf.Lerp(1f, Mathf.Lerp(flickerMinAlpha, 1f, noise), Mathf.Clamp01(strength));
                for (int k = 0; k < 4; k++)
                {
                    var c = colors[v + k];
                    c.a = (byte)(c.a * alphaScale);
                    colors[v + k] = c;
                }
                break;
            }
        }
    }

    private static void Offset(Vector3[] vertices, int v, Vector3 offset)
    {
        vertices[v] += offset;
        vertices[v + 1] += offset;
        vertices[v + 2] += offset;
        vertices[v + 3] += offset;
    }
}