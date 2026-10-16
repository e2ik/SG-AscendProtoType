using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ChoiceReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private TMP_Text label;

    [Header("Size")]
    [SerializeField] [Min(0f)] private float collapsedWidth;
    [SerializeField] private bool clipText = true;

    [Header("Animation")]
    [SerializeField] [Min(0.01f)] private float revealDuration = 0.18f;
    [SerializeField] [Min(0.01f)] private float hideDuration = 0.12f;
    [SerializeField] [Min(0f)] private float stepsPerSecond;
    [SerializeField] private bool snapToPixels = true;

    [Header("Number")]
    [SerializeField] private TMP_Text numberLabel;
    [SerializeField] private string numberFormat = "#{0}";
    [SerializeField] private Color numberColour = Color.white;
    [SerializeField] private bool hideLabelWhenCollapsed = true;

    [Header("Background Colour")]
    [SerializeField] private bool changeBackgroundColour = true;
    [SerializeField] private Color collapsedBackgroundColour = Color.white;
    [SerializeField] private Color revealedBackgroundColour = Color.white;

    [Header("Active Size")]
    [SerializeField] [Min(0.1f)] private float activeScale = 1.15f;
    [SerializeField] [Min(0f)] private float extraSpacing = 4f;

    [Header("Idle Shove")]
    [SerializeField] private Vector2 shoveAmount = new Vector2(1f, 1f);
    [SerializeField] [Min(0f)] private float shoveRotation = 2f;
    [SerializeField] [Min(0f)] private float shovesPerSecond = 4f;

    [Header("Active Text Hover")]
    [SerializeField] private Vector2 textShoveAmount = new Vector2(1f, 1f);
    [SerializeField] [Min(0f)] private float textShovesPerSecond = 6f;
    [SerializeField] private bool textShovePerLetter;
    [SerializeField] private bool shoveBackgroundToo = true;
    [SerializeField] [Min(0f)] private float backgroundShoveRotation = 1.5f;

    [Header("Back Layer")]
    [SerializeField] private bool useBackLayer = true;
    [SerializeField] private Color backLayerColour = new Color(0.2f, 0.2f, 0.3f, 1f);
    [SerializeField] [Min(1f)] private float backLayerScale = 1.15f;
    [SerializeField] private Vector2 backLayerShoveAmount = new Vector2(2f, 2f);
    [SerializeField] [Min(0f)] private float backLayerShoveRotation = 3f;
    [SerializeField] [Min(0f)] private float backLayerShovesPerSecond = 3f;

    [Header("Text Colour")]
    [SerializeField] private bool changeTextColour = true;
    [SerializeField] private Color collapsedTextColour = Color.white;
    [SerializeField] private Color revealedTextColour = Color.black;

    public TMP_Text Label => label;
    public float Reveal { get; private set; }
    public Vector2 ShoveOffset => _shoveOffset * (1f - Reveal);
    public float PushDistance => Reveal * (_rect != null ? _rect.rect.height * Mathf.Max(0f, activeScale - 1f) * 0.5f + extraSpacing : extraSpacing);

    private RectTransform _rect;
    private RectMask2D _mask;
    private Image _backgroundImage;
    private float _progress;
    private float _shownProgress;
    private float _stepTimer;
    private bool _hovered;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private Vector2 _shoveOffset;
    private float _shoveAngle;
    private float _shoveTimer;
    private float _lastReveal = -1f;
    private Vector2 _lastShove;
    private float _textShoveTimer;
    private int _textShoveSeed;
    private bool _textShoved;
    private RectTransform _backLayer;
    private Image _backLayerImage;
    private float _backShoveTimer;
    private Vector2 _backShoveOffset;
    private float _frameDelta;
    private bool _mirrored;
    private ChoicesFollowPlayer _follow;
    private float _backShoveAngle;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;

        if (label == null)
        {
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == numberLabel) continue;
                label = text;
                break;
            }
        }

        if (background != null)
            _backgroundImage = background.GetComponent<Image>();

        if (background != null)
        {
            var layout = background.GetComponent<LayoutElement>();
            if (layout == null)
                layout = background.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            background.anchorMin = new Vector2(0f, 0f);
            background.anchorMax = new Vector2(0f, 1f);
            background.pivot = new Vector2(0f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.SetAsFirstSibling();
        }

        if (useBackLayer && _backgroundImage != null)
            CreateBackLayer();

        if (numberLabel != null)
        {
            var numberRect = numberLabel.rectTransform;
            var numberLayout = numberLabel.GetComponent<LayoutElement>();
            if (numberLayout == null)
                numberLayout = numberLabel.gameObject.AddComponent<LayoutElement>();
            numberLayout.ignoreLayout = true;

            numberRect.anchorMin = new Vector2(0f, 0f);
            numberRect.anchorMax = new Vector2(0f, 1f);
            numberRect.pivot = new Vector2(0.5f, 0.5f);
            numberRect.anchoredPosition = Vector2.zero;
            numberLabel.alignment = TextAlignmentOptions.Center;
            numberLabel.raycastTarget = false;
        }

        if (clipText)
        {
            _mask = GetComponent<RectMask2D>();
            if (_mask == null)
                _mask = gameObject.AddComponent<RectMask2D>();
        }
    }

    private void CreateBackLayer()
    {
        var go = new GameObject("ButtonBGBack", typeof(RectTransform));
        go.layer = gameObject.layer;
        _backLayer = (RectTransform)go.transform;
        _backLayer.SetParent(transform, false);
        _backLayer.SetSiblingIndex(0);

        var layout = go.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        _backLayerImage = go.AddComponent<Image>();
        _backLayerImage.sprite = _backgroundImage.sprite;
        _backLayerImage.type = _backgroundImage.type;
        _backLayerImage.pixelsPerUnitMultiplier = _backgroundImage.pixelsPerUnitMultiplier;
        _backLayerImage.preserveAspect = _backgroundImage.preserveAspect;
        _backLayerImage.raycastTarget = false;
        _backLayerImage.color = Color.clear;

        _backLayer.anchorMin = new Vector2(0f, 0.5f);
        _backLayer.anchorMax = new Vector2(0f, 0.5f);
        _backLayer.pivot = new Vector2(0.5f, 0.5f);
    }

    private float BackLayerMargin => _backLayer == null ? 0f
        : Reveal * (_rect.rect.height * (backLayerScale - 1f) * 0.5f + Mathf.Max(backLayerShoveAmount.x, backLayerShoveAmount.y) + 1f);

    private void UpdateBackLayer(float dt, float width)
    {
        if (_backLayer == null) return;

        if (Reveal <= 0.001f)
        {
            _backLayerImage.enabled = false;
            _backShoveTimer = 0f;
            return;
        }

        _backLayerImage.enabled = true;

        if (backLayerShovesPerSecond > 0f)
        {
            _backShoveTimer += dt;
            if (_backShoveTimer >= 1f / backLayerShovesPerSecond)
            {
                _backShoveTimer = 0f;
                var offset = new Vector2(Random.Range(-backLayerShoveAmount.x, backLayerShoveAmount.x), Random.Range(-backLayerShoveAmount.y, backLayerShoveAmount.y));
                if (snapToPixels)
                    offset = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
                _backShoveOffset = offset;
                _backShoveAngle = Random.Range(-backLayerShoveRotation, backLayerShoveRotation);
            }
        }

        float height = _rect.rect.height;
        float grow = height * (backLayerScale - 1f) * Reveal;
        var size = new Vector2(width + grow, height + grow);
        if (snapToPixels)
            size = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));

        _backLayer.sizeDelta = size;
        _backLayer.anchoredPosition = new Vector2(_mirrored ? -width * 0.5f : width * 0.5f, 0f) + _backShoveOffset * Reveal;
        _backLayer.localRotation = Quaternion.Euler(0f, 0f, _backShoveAngle * Reveal);

        var colour = backLayerColour;
        colour.a *= Reveal;
        _backLayerImage.color = colour;
    }

    public void SetNumber(int number)
    {
        if (numberLabel != null)
            numberLabel.text = string.Format(numberFormat, number);
    }

    public void SetMirrored(bool mirrored)
    {
        if (_mirrored == mirrored) return;
        _mirrored = mirrored;
        ApplyMirror();
    }

    private void ApplyMirror()
    {
        float edge = _mirrored ? 1f : 0f;
        float sign = _mirrored ? -1f : 1f;

        _rect.pivot = new Vector2(edge, _rect.pivot.y);

        if (background != null)
        {
            background.anchorMin = new Vector2(edge, 0f);
            background.anchorMax = new Vector2(edge, 1f);
            background.pivot = new Vector2(0f, 0.5f);
            var scale = background.localScale;
            scale.x = Mathf.Abs(scale.x) * sign;
            background.localScale = scale;
        }

        if (numberLabel != null)
        {
            var numberRect = numberLabel.rectTransform;
            bool insideBackground = background != null && numberRect.IsChildOf(background);
            float numberEdge = insideBackground ? 0f : edge;
            numberRect.anchorMin = new Vector2(numberEdge, 0f);
            numberRect.anchorMax = new Vector2(numberEdge, 1f);
            numberRect.pivot = new Vector2(0.5f, 0.5f);
            var scale = numberRect.localScale;
            scale.x = Mathf.Abs(scale.x) * (insideBackground ? sign : 1f);
            numberRect.localScale = scale;
        }

        if (_backLayer != null)
        {
            _backLayer.anchorMin = new Vector2(edge, 0.5f);
            _backLayer.anchorMax = new Vector2(edge, 0.5f);
            var scale = _backLayer.localScale;
            scale.x = Mathf.Abs(scale.x) * sign;
            _backLayer.localScale = scale;
        }

        Apply(_shownProgress);
    }

    private void Start()
    {
        _follow = transform.parent != null ? transform.parent.GetComponent<ChoicesFollowPlayer>() : null;
        if (_follow != null)
            SetMirrored(!_follow.OnRight);

        if (numberLabel == null || transform.parent == null) return;

        int number = 0;
        foreach (Transform sibling in transform.parent)
        {
            if (sibling.GetComponent<ChoiceReveal>() == null) continue;
            number++;
            if (sibling == transform) break;
        }

        SetNumber(number);
    }

    private void OnEnable()
    {
        _progress = IsHighlighted ? 1f : 0f;
        _shownProgress = _progress;
        Apply(_shownProgress);
    }

    private void OnDisable()
    {
        _hovered = false;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.HoverAllowed)
            _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    private bool IsHighlighted
    {
        get
        {
            var eventSystem = EventSystem.current;
            bool selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            return selected || (_hovered && UIManager.HoverAllowed);
        }
    }

    private void LateUpdate()
    {
        if (_follow != null)
            SetMirrored(!_follow.OnRight);

        float dt = Time.unscaledDeltaTime;
        _frameDelta = dt;

        if (!UIManager.HoverAllowed)
            _hovered = false;

        UpdateShove(dt);
        float target = IsHighlighted ? 1f : 0f;
        float duration = target > _progress ? revealDuration : hideDuration;
        _progress = Mathf.MoveTowards(_progress, target, dt / duration);

        if (stepsPerSecond > 0f && !Mathf.Approximately(_progress, target))
        {
            _stepTimer += dt;
            if (_stepTimer < 1f / stepsPerSecond)
            {
                Apply(_shownProgress);
                OnRenderTextShove(dt);
                return;
            }
            _stepTimer = 0f;
        }

        _shownProgress = _progress;
        Apply(_shownProgress);
        OnRenderTextShove(dt);
    }

    private void OnRenderTextShove(float dt)
    {
        bool active = Reveal > 0.001f && textShovesPerSecond > 0f && textShoveAmount != Vector2.zero;
        if (!active)
        {
            if (_textShoved)
            {
                if (label != null)
                    label.ForceMeshUpdate();
                ResetBackgroundShove();
                _textShoved = false;
            }
            return;
        }

        _textShoveTimer += dt;
        if (_textShoveTimer >= 1f / textShovesPerSecond)
        {
            _textShoveTimer = 0f;
            _textShoveSeed = Random.Range(0, int.MaxValue);
        }

        var random = new System.Random(_textShoveSeed);
        var whole = NextOffset(random);

        if (background != null && shoveBackgroundToo)
        {
            background.anchoredPosition = whole;
            float angle = (float)(random.NextDouble() * 2.0 - 1.0) * backgroundShoveRotation * Reveal;
            background.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        _textShoved = true;
        if (label == null) return;

        label.ForceMeshUpdate();
        var info = label.textInfo;

        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;

            var offset = textShovePerLetter ? NextOffset(random) : whole;
            var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            int start = character.vertexIndex;
            for (int v = 0; v < 4; v++)
                vertices[start + v] += (Vector3)offset;
        }

        label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private void ResetBackgroundShove()
    {
        if (background == null) return;

        background.anchoredPosition = Vector2.zero;
        background.localRotation = Quaternion.identity;
    }

    private Vector2 NextOffset(System.Random random)
    {
        float x = (float)(random.NextDouble() * 2.0 - 1.0) * textShoveAmount.x * Reveal;
        float y = (float)(random.NextDouble() * 2.0 - 1.0) * textShoveAmount.y * Reveal;
        if (snapToPixels)
        {
            x = Mathf.Round(x);
            y = Mathf.Round(y);
        }
        return new Vector2(x, y);
    }

    private void UpdateShove(float dt)
    {
        if (shovesPerSecond <= 0f || (shoveAmount == Vector2.zero && shoveRotation <= 0f))
        {
            _shoveOffset = Vector2.zero;
            _shoveAngle = 0f;
            return;
        }

        _shoveTimer += dt;
        if (_shoveTimer < 1f / shovesPerSecond) return;
        _shoveTimer = 0f;

        var offset = new Vector2(Random.Range(-shoveAmount.x, shoveAmount.x), Random.Range(-shoveAmount.y, shoveAmount.y));
        if (snapToPixels)
            offset = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));

        _shoveOffset = offset;
        _shoveAngle = Random.Range(-shoveRotation, shoveRotation);
    }

    private void RequestLayout()
    {
        var shove = ShoveOffset;
        if (Mathf.Approximately(Reveal, _lastReveal) && shove == _lastShove) return;

        _lastReveal = Reveal;
        _lastShove = shove;

        if (transform.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    private void Apply(float progress)
    {
        float eased = progress * progress * (3f - 2f * progress);
        Reveal = eased;

        transform.localScale = _baseScale * Mathf.Lerp(1f, activeScale, eased);
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _shoveAngle * (1f - eased));
        RequestLayout();

        float fullWidth = _rect.rect.width;
        float height = _rect.rect.height;
        float collapsed = collapsedWidth > 0f ? collapsedWidth : height;
        collapsed = Mathf.Min(collapsed, fullWidth);

        float width = Mathf.Lerp(collapsed, fullWidth, eased);
        if (snapToPixels)
            width = Mathf.Round(width);

        if (background != null)
            background.sizeDelta = new Vector2(width, 0f);

        UpdateBackLayer(_frameDelta, width);

        if (_mask != null)
        {
            float margin = BackLayerMargin + (shoveBackgroundToo ? Reveal * (Mathf.Max(textShoveAmount.x, textShoveAmount.y) + 1f) : 0f);
            float hidden = Mathf.Max(-margin, fullWidth - width - margin);
            _mask.padding = _mirrored
                ? new Vector4(hidden, -margin, -margin, -margin)
                : new Vector4(-margin, -margin, hidden, -margin);
        }

        if (changeBackgroundColour && _backgroundImage != null)
        {
            _backgroundImage.color = Color.Lerp(collapsedBackgroundColour, revealedBackgroundColour, eased);
        }

        if (label != null)
        {
            var colour = changeTextColour ? Color.Lerp(collapsedTextColour, revealedTextColour, eased) : label.color;
            if (numberLabel != null && hideLabelWhenCollapsed)
                colour.a = (changeTextColour ? Mathf.Lerp(collapsedTextColour.a, revealedTextColour.a, eased) : 1f) * eased;
            label.color = colour;
        }

        if (numberLabel != null)
        {
            var numberRect = numberLabel.rectTransform;
            bool insideBackground = background != null && numberRect.IsChildOf(background);
            float centre = collapsed * 0.5f * (_mirrored && !insideBackground ? -1f : 1f);
            numberRect.sizeDelta = new Vector2(collapsed, 0f);
            numberRect.anchoredPosition = new Vector2(centre, 0f);
            var numberTint = numberColour;
            numberTint.a *= 1f - eased;
            numberLabel.color = numberTint;
        }
    }
}