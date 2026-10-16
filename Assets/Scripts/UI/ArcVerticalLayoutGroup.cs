using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Arc Vertical Layout Group")]
public class ArcVerticalLayoutGroup : VerticalLayoutGroup
{
    [SerializeField] private float arcAmount = 20f;
    [SerializeField] private float baseOffset;
    [SerializeField] private bool flipDirection;
    [SerializeField] [Range(0f, 1f)] private float pairStagger = 0.5f;
    [SerializeField] private bool snapToPixels = true;

    public float ArcAmount
    {
        get => arcAmount;
        set
        {
            arcAmount = value;
            SetDirty();
        }
    }

    public bool FlipDirection
    {
        get => flipDirection;
        set
        {
            flipDirection = value;
            SetDirty();
        }
    }

    public override void SetLayoutHorizontal()
    {
        base.SetLayoutHorizontal();
        ApplyArc();
    }

    private void ApplyArc()
    {
        int count = rectChildren.Count;
        if (count == 0) return;

        float direction = flipDirection ? -1f : 1f;

        for (int i = 0; i < count; i++)
        {
            float offset = (baseOffset + ArcFactor(i, count) * arcAmount) * direction;
            if (snapToPixels)
                offset = Mathf.Round(offset);

            var child = rectChildren[i];
            var position = child.anchoredPosition;
            position.x += offset;
            child.anchoredPosition = position;
        }
    }

    private float ArcFactor(int index, int count)
    {
        if (count <= 1) return 0f;
        if (count == 2) return index == 0 ? 0f : pairStagger;

        float edge = Mathf.Sin(0.5f / count * Mathf.PI);
        float value = Mathf.Sin((index + 0.5f) / count * Mathf.PI);
        return Mathf.Clamp01((value - edge) / Mathf.Max(0.0001f, 1f - edge));
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetDirty();
    }
#endif
}