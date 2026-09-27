using UnityEngine;

public class RunnerObstacle : MonoBehaviour
{
    [SerializeField] [Min(0f)] private float heightOverride;
    [SerializeField] [Min(0f)] private float widthOverride;

    public float Height => heightOverride > 0f ? heightOverride : MeasureHeight();
    public float Width => widthOverride > 0f ? widthOverride : MeasureWidth();

    private float MeasureHeight()
    {
        var box = GetComponentInChildren<BoxCollider2D>();
        if (box != null)
            return HeightAboveBase(box.transform, box.offset + Vector2.up * box.size.y * 0.5f);

        var sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
            return HeightAboveBase(sprite.transform, new Vector2(0f, sprite.sprite.bounds.max.y));

        if (GetComponentInChildren<Collider2D>() != null)
            Debug.LogWarning($"{name}: only BoxCollider2D or a sprite can be measured automatically - set the overrides", this);

        return 1f;
    }

    private float MeasureWidth()
    {
        var box = GetComponentInChildren<BoxCollider2D>();
        if (box != null)
            return box.size.x * Mathf.Abs(box.transform.lossyScale.x);

        var sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
            return sprite.sprite.bounds.size.x * Mathf.Abs(sprite.transform.lossyScale.x);

        return 1f;
    }

    private float HeightAboveBase(Transform part, Vector2 localTopPoint)
    {
        Vector3 topInRootSpace = transform.InverseTransformPoint(part.TransformPoint(localTopPoint));
        return Mathf.Max(0f, topInRootSpace.y * Mathf.Abs(transform.localScale.y));
    }
}