using UnityEngine;

public static class OutlineDriver
{
    private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int SideId = Shader.PropertyToID("_OutlineSide");
    private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int AmountId = Shader.PropertyToID("_OutlineAmount");

    public static float Evaluate(HighlightSettings settings)
    {
        switch (settings.style)
        {
            case HighlightStyle.Pulse:
                float wave = (Mathf.Sin(Time.time * settings.speed * Mathf.PI * 2f) + 1f) * 0.5f;
                return Mathf.Lerp(settings.minAmount, settings.maxAmount, wave);
            case HighlightStyle.Flash:
                return Mathf.Repeat(Time.time * settings.speed, 1f) < 0.5f ? settings.maxAmount : settings.minAmount;
            default:
                return settings.maxAmount;
        }
    }

    public static void Apply(SpriteRenderer[] renderers, MaterialPropertyBlock block, HighlightSettings settings, Color colour, float amount)
    {
        if (renderers == null || block == null || settings == null) return;

        foreach (var target in renderers)
        {
            if (target == null) continue;

            target.GetPropertyBlock(block);
            block.SetColor(ColorId, colour);
            block.SetFloat(SideId, settings.side == OutlineSide.Outside ? 1f : 0f);
            block.SetFloat(WidthId, settings.width);
            block.SetFloat(AmountId, amount);
            target.SetPropertyBlock(block);
        }
    }

    public static void AssignMaterial(SpriteRenderer[] renderers, Material material)
    {
        if (renderers == null || material == null) return;

        foreach (var target in renderers)
        {
            if (target != null && target.sharedMaterial != null && target.sharedMaterial.shader != material.shader)
                target.sharedMaterial = material;
        }
    }
}
