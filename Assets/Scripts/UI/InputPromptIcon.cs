using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputPromptIcon : MonoBehaviour
{
    [Header("Targets (auto-found if empty)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text label;

    [Header("Keyboard & Mouse")]
    [SerializeField] private Sprite keyboardSprite;
    [SerializeField] private string keyboardText;

    [Header("Gamepad")]
    [SerializeField] private Sprite gamepadSprite;
    [SerializeField] private string gamepadText;

    private void Awake()
    {
        if (spriteRenderer == null && image == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            image = GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnDeviceChanged += Apply;
        Apply(InputManager.Instance.ActiveDevice);
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged -= Apply;
    }

    private void Apply(InputDeviceType device)
    {
        bool gamepad = device == InputDeviceType.Gamepad;

        var sprite = gamepad ? gamepadSprite : keyboardSprite;
        if (sprite != null)
        {
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
            if (image != null) image.sprite = sprite;
        }

        string text = gamepad ? gamepadText : keyboardText;
        if (label != null && !string.IsNullOrEmpty(text))
            label.text = text;
    }
}