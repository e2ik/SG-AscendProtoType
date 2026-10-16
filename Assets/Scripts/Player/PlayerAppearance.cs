using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer hair;
    [SerializeField] private SpriteRenderer skin;
    [SerializeField] private SpriteRenderer eyes;
    [SerializeField] private SpriteRenderer clothesTop;
    [SerializeField] private SpriteRenderer clothesBottom;
    [SerializeField] private SpriteRenderer shoes;

    public void Apply(CharacterData data)
    {
        if (data == null) return;

        SetColour(hair, data.hairColor);
        SetColour(skin, data.skinColor);
        SetColour(eyes, data.eyeColor);
        SetColour(clothesTop, data.clothesTopColor);
        SetColour(clothesBottom, data.clothesBottomColor);
        SetColour(shoes, data.shoesColor);

        SetStyle(hair, data.hairStyle);
        SetStyle(clothesTop, data.clothesTopStyle);
        SetStyle(clothesBottom, data.clothesBottomStyle);
    }

    private static void SetStyle(SpriteRenderer part, int style)
    {
        if (part != null && part.TryGetComponent<SpriteStyleSwapper>(out var swapper))
            swapper.SetStyle(style);
    }

    private static void SetColour(SpriteRenderer part, Color colour)
    {
        if (part == null || colour.a <= 0f) return;
        part.color = colour;
    }
}
