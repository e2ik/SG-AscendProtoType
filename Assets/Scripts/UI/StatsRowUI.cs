using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatRowUI : MonoBehaviour
{
    [SerializeField] private StatType stat;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private Color pendingColor = new Color(0.5f, 1f, 0.4f);

    [Header("Active Row")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color activeLabelColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color activeBackgroundColor = new Color(0.2f, 0.45f, 0.9f);
    [SerializeField] [Min(0f)] private float activeFadeDuration = 0.1f;
    [SerializeField] private GameObject icon;
    [SerializeField] [Range(0f, 1f)] private float inactiveIconAlpha = 0.6f;

    public StatType Stat => stat;
    public Button Plus => plusButton;
    public Button Minus => minusButton;

    private TMP_Text _plusText;
    private TMP_Text _minusText;
    private bool _cached;
    private Color _labelBaseColor;
    private Color _backgroundBaseColor;
    private CanvasGroup _iconGroup;
    private float _activeBlend;

    private void Awake()
    {
        if (labelText != null) _labelBaseColor = labelText.color;
        if (backgroundImage != null) _backgroundBaseColor = backgroundImage.color;
        if (icon != null)
        {
            _iconGroup = icon.GetComponent<CanvasGroup>();
            if (_iconGroup == null)
                _iconGroup = icon.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable() => ApplyActive();

    private void OnDisable()
    {
        _activeBlend = 0f;
        ApplyActive();
    }

    private void Update()
    {
        float target = IsActive() ? 1f : 0f;
        if (Mathf.Approximately(_activeBlend, target)) return;

        _activeBlend = activeFadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(_activeBlend, target, Time.unscaledDeltaTime / activeFadeDuration);
        ApplyActive();
    }

    private bool IsActive()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null) return false;

        return (plusButton != null && selected == plusButton.gameObject)
               || (minusButton != null && selected == minusButton.gameObject)
               || selected.transform.IsChildOf(transform);
    }

    private void ApplyActive()
    {
        if (labelText != null)
            labelText.color = Color.Lerp(_labelBaseColor, activeLabelColor, _activeBlend);
        if (backgroundImage != null)
            backgroundImage.color = Color.Lerp(_backgroundBaseColor, activeBackgroundColor, _activeBlend);

        if (_iconGroup != null)
            _iconGroup.alpha = Mathf.Lerp(inactiveIconAlpha, 1f, _activeBlend);
    }

    public void Display(int current, int pending, int max, string effect)
    {
        int shown = current + pending;
        string value = pending > 0
            ? $"<color=#{ColorUtility.ToHtmlStringRGBA(pendingColor)}>{shown}</color>"
            : shown.ToString();
        string cap = max == int.MaxValue ? string.Empty : $"/{max}";
        valueText.text = $"{value}{cap}";

        if (effectText != null)
        {
            effectText.text = effect ?? string.Empty;
            effectText.gameObject.SetActive(!string.IsNullOrEmpty(effect));
        }
    }

    public void SetButtonStates(bool plusInteractable, bool minusInteractable)
    {
        CacheTexts();
        ApplyState(plusButton, _plusText, plusInteractable);
        ApplyState(minusButton, _minusText, minusInteractable);
    }

    private void CacheTexts()
    {
        if (_cached) return;
        _cached = true;
        if (plusButton != null) _plusText = plusButton.GetComponentInChildren<TMP_Text>(true);
        if (minusButton != null) _minusText = minusButton.GetComponentInChildren<TMP_Text>(true);
    }

    private static void ApplyState(Button button, TMP_Text text, bool interactable)
    {
        if (button == null) return;

        button.interactable = interactable;

        if (text == null || button.transition != Selectable.Transition.ColorTint) return;

        var colors = button.colors;
        var tint = (interactable ? colors.normalColor : colors.disabledColor) * colors.colorMultiplier;
        text.CrossFadeColor(tint, Application.isPlaying ? colors.fadeDuration : 0f, true, true);
    }
}