using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CategoryInfoPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private TMP_Text flavourText;
    [SerializeField] private Image categoryImage;
    [SerializeField] private bool hideImageWhenEmpty = true;
    [SerializeField] private bool tintImageWithLabelColour = true;

    [Header("Category Label Style")]
    [SerializeField] [Min(0.1f)] private float labelSizeMultiplier = 1.5f;
    [SerializeField] private bool boldLabel = true;
    [SerializeField] private List<Color> labelColours = new List<Color>
    {
        new Color32(0xFF, 0xB3, 0xBA, 0xFF),
        new Color32(0xFF, 0xDF, 0xBA, 0xFF),
        new Color32(0xFF, 0xFF, 0xBA, 0xFF),
        new Color32(0xBA, 0xFF, 0xC9, 0xFF),
        new Color32(0xBA, 0xE1, 0xFF, 0xFF),
        new Color32(0xD4, 0xB8, 0xFF, 0xFF)
    };

    [Header("Category Label Bounce")]
    [SerializeField] private bool bounceLabel = true;
    [SerializeField] private float bounceHeight = 6f;
    [SerializeField] [Min(0.01f)] private float bouncesPerSecond = 1.2f;
    [SerializeField] private bool snapToWholePixels = true;
    [SerializeField] private bool perLetterWave = true;
    [SerializeField] [Min(0f)] private float letterDelay = 0.12f;

    [Header("Category Change")]
    [SerializeField] private RectTransform wobbleTarget;
    [SerializeField] [Min(0f)] private float wobbleDuration = 0.5f;
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] [Min(1)] private int wobbleSwings = 3;
    [SerializeField] private bool fadeOnChange = true;
    [SerializeField] [Min(0f)] private float changeFadeDuration = 0.12f;

    [Header("Flavour Text Style")]
    [SerializeField] private List<Color> flavourColours = new List<Color>
    {
        new Color32(0xFF, 0x8F, 0x9A, 0xFF),
        new Color32(0xFF, 0xC2, 0x85, 0xFF),
        new Color32(0xFF, 0xF0, 0x7A, 0xFF),
        new Color32(0x8C, 0xF5, 0xA8, 0xFF),
        new Color32(0x8C, 0xCB, 0xFF, 0xFF),
        new Color32(0xBE, 0x93, 0xFF, 0xFF)
    };

    public event Action BackPressed;

    public bool IsOpen => panel != null && panel.activeSelf;
    public Transform PanelRoot => panel != null ? panel.transform : transform;

    private bool _labelSaved;
    private string _savedText;
    private float _savedSize;
    private float _savedSizeMin;
    private float _savedSizeMax;
    private FontStyles _savedStyle;
    private Color _savedColour;
    private Vector2 _savedPosition;
    private RectTransform _labelRect;
    private Color _imageBaseColour = Color.white;
    private CanvasGroup _panelGroup;
    private UIPanelAnimator _panelAnimator;
    private Quaternion _wobbleBaseRotation = Quaternion.identity;
    private Coroutine _swapRoutine;
    private Coroutine _wobbleRoutine;

    private void Awake()
    {
        if (categoryImage != null)
            _imageBaseColour = categoryImage.color;

        if (wobbleTarget != null)
            _wobbleBaseRotation = wobbleTarget.localRotation;

        if (panel == null) return;

        _panelGroup = panel.GetComponent<CanvasGroup>();
        _panelAnimator = panel.GetComponent<UIPanelAnimator>();

        panel.SetActive(false);

        var uiPanel = panel.GetComponent<UIPanel>();
        if (uiPanel != null)
            uiPanel.OnBack += () => BackPressed?.Invoke();
    }

    public Color ColourForIndex(int index)
    {
        if (labelColours == null || labelColours.Count == 0 || index < 0) return Color.white;
        return labelColours[index % labelColours.Count];
    }

    public void Show(string label, string flavour, Sprite image, Color labelColour, FlavourColourMode flavourMode = FlavourColourMode.MatchLabel, Color singleFlavourColour = default)
    {
        bool switching = IsOpen && (_panelAnimator == null || !_panelAnimator.IsHiding);
        void Apply() => ApplyContent(label, flavour, image, labelColour, flavourMode, singleFlavourColour);

        if (!switching)
        {
            StopSwap();
            Apply();
            UIPanelAnimator.SetVisible(panel, true);
            return;
        }

        StartWobble();

        if (!fadeOnChange || _panelGroup == null || changeFadeDuration <= 0f)
        {
            Apply();
            return;
        }

        StopSwap();
        _swapRoutine = StartCoroutine(SwapRoutine(Apply));
    }

    private IEnumerator SwapRoutine(Action apply)
    {
        float start = _panelGroup.alpha;
        for (float t = 0f; t < changeFadeDuration; t += Time.unscaledDeltaTime)
        {
            _panelGroup.alpha = Mathf.Lerp(start, 0f, t / changeFadeDuration);
            yield return null;
        }

        _panelGroup.alpha = 0f;
        apply();

        for (float t = 0f; t < changeFadeDuration; t += Time.unscaledDeltaTime)
        {
            _panelGroup.alpha = Mathf.Lerp(0f, 1f, t / changeFadeDuration);
            yield return null;
        }

        _panelGroup.alpha = 1f;
        _swapRoutine = null;
    }

    private void StopSwap()
    {
        if (_swapRoutine == null) return;

        StopCoroutine(_swapRoutine);
        _swapRoutine = null;
        if (_panelGroup != null)
            _panelGroup.alpha = 1f;
    }

    private void StartWobble()
    {
        if (wobbleTarget == null || wobbleDuration <= 0f) return;

        if (_wobbleRoutine != null)
            StopCoroutine(_wobbleRoutine);
        _wobbleRoutine = StartCoroutine(WobbleRoutine());
    }

    private IEnumerator WobbleRoutine()
    {
        for (float t = 0f; t < wobbleDuration; t += Time.unscaledDeltaTime)
        {
            float progress = t / wobbleDuration;
            float angle = Mathf.Sin(progress * wobbleSwings * Mathf.PI * 2f) * wobbleAngle * (1f - progress);
            wobbleTarget.localRotation = _wobbleBaseRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        wobbleTarget.localRotation = _wobbleBaseRotation;
        _wobbleRoutine = null;
    }

    private void StopWobble()
    {
        if (_wobbleRoutine != null)
        {
            StopCoroutine(_wobbleRoutine);
            _wobbleRoutine = null;
        }

        if (wobbleTarget != null)
            wobbleTarget.localRotation = _wobbleBaseRotation;
    }

    private void ApplyContent(string label, string flavour, Sprite image, Color labelColour, FlavourColourMode flavourMode, Color singleFlavourColour)
    {
        if (categoryLabel != null)
        {
            SaveLabel();

            categoryLabel.text = label;
            categoryLabel.color = labelColour;
            categoryLabel.fontStyle = boldLabel ? _savedStyle | FontStyles.Bold : _savedStyle;

            if (categoryLabel.enableAutoSizing)
            {
                categoryLabel.fontSizeMin = _savedSizeMin * labelSizeMultiplier;
                categoryLabel.fontSizeMax = _savedSizeMax * labelSizeMultiplier;
            }
            else
            {
                categoryLabel.fontSize = _savedSize * labelSizeMultiplier;
            }
        }

        if (flavourText != null)
        {
            switch (flavourMode)
            {
                case FlavourColourMode.Pastel:
                    flavourText.text = ColourWords(flavour);
                    flavourText.color = Color.white;
                    break;
                case FlavourColourMode.Single:
                    flavourText.text = flavour;
                    flavourText.color = singleFlavourColour;
                    break;
                default:
                    flavourText.text = flavour;
                    flavourText.color = labelColour;
                    break;
            }
        }

        if (categoryImage != null)
        {
            categoryImage.sprite = image;
            categoryImage.enabled = image != null || !hideImageWhenEmpty;
            categoryImage.color = tintImageWithLabelColour ? labelColour : _imageBaseColour;
        }
    }

    private void LateUpdate()
    {
        if (!_labelSaved || !bounceLabel || _labelRect == null) return;

        if (perLetterWave)
        {
            WaveLetters();
            return;
        }

        _labelRect.anchoredPosition = _savedPosition + new Vector2(0f, BounceOffset(0f));
    }

    private float BounceOffset(float delay)
    {
        float offset = Mathf.Abs(Mathf.Sin((Time.unscaledTime - delay) * bouncesPerSecond * Mathf.PI)) * bounceHeight;
        return snapToWholePixels ? Mathf.Round(offset) : offset;
    }

    private void WaveLetters()
    {
        categoryLabel.ForceMeshUpdate();
        var textInfo = categoryLabel.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var character = textInfo.characterInfo[i];
            if (!character.isVisible) continue;

            var offset = new Vector3(0f, BounceOffset(i * letterDelay), 0f);
            var vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            int start = character.vertexIndex;

            for (int v = 0; v < 4; v++)
                vertices[start + v] += offset;
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            var meshInfo = textInfo.meshInfo[m];
            meshInfo.mesh.vertices = meshInfo.vertices;
            categoryLabel.UpdateGeometry(meshInfo.mesh, m);
        }
    }

    private string ColourWords(string text)
    {
        var palette = flavourColours != null && flavourColours.Count > 0 ? flavourColours : labelColours;
        if (string.IsNullOrEmpty(text) || palette == null || palette.Count == 0) return text;

        var builder = new StringBuilder(text.Length * 4);
        int wordIndex = 0;
        int i = 0;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;

            string hex = ColorUtility.ToHtmlStringRGB(palette[wordIndex % palette.Count]);
            builder.Append("<color=#").Append(hex).Append('>').Append(text, start, i - start).Append("</color>");
            wordIndex++;
        }

        return builder.ToString();
    }

    public void Hide()
    {
        StopSwap();
        StopWobble();
        RestoreLabel();
        UIPanelAnimator.SetVisible(panel, false);
    }

    private void SaveLabel()
    {
        if (_labelSaved) return;

        _savedText = categoryLabel.text;
        _savedSize = categoryLabel.fontSize;
        _savedSizeMin = categoryLabel.fontSizeMin;
        _savedSizeMax = categoryLabel.fontSizeMax;
        _savedStyle = categoryLabel.fontStyle;
        _savedColour = categoryLabel.color;
        _labelRect = categoryLabel.rectTransform;
        _savedPosition = _labelRect.anchoredPosition;
        _labelSaved = true;
    }

    private void RestoreLabel()
    {
        if (!_labelSaved || categoryLabel == null) return;

        categoryLabel.text = _savedText;
        categoryLabel.fontSize = _savedSize;
        categoryLabel.fontSizeMin = _savedSizeMin;
        categoryLabel.fontSizeMax = _savedSizeMax;
        categoryLabel.fontStyle = _savedStyle;
        categoryLabel.color = _savedColour;
        if (_labelRect != null)
            _labelRect.anchoredPosition = _savedPosition;
        _labelSaved = false;
        categoryLabel.ForceMeshUpdate();
    }
}