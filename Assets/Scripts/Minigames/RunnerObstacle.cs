using UnityEngine;

public class RunnerObstacle : MonoBehaviour
{
    [SerializeField] [Min(0f)] private float heightOverride;

    public float Height => heightOverride > 0f ? heightOverride : Measure();

    private float Measure()
    {
        var box = GetComponentInChildren<BoxCollider2D>();
        if (box != null)
            return HeightAboveBase(box.transform, box.offset + Vector2.up * box.size.y * 0.5f);

        var col = GetComponentInChildren<Collider2D>();
        var sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
            return HeightAboveBase(sprite.transform, new Vector2(0f, sprite.sprite.bounds.max.y));

        if (col != null)
            Debug.LogWarning($"{name}: only BoxCollider2D or a sprite can be measured automatically - set Height Override", this);

        return 1f;
    }

    private float HeightAboveBase(Transform part, Vector2 localTopPoint)
    {
        Vector3 topInRootSpace = transform.InverseTransformPoint(part.TransformPoint(localTopPoint));
        return Mathf.Max(0f, topInRootSpace.y * Mathf.Abs(transform.localScale.y));
    }
}