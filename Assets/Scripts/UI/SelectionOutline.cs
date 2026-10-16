using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SelectionOutline : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject outline;
    [SerializeField] private float minAlpha = 0.4f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private int sortingOrderOffset = 1;

    [Header("Own Settings")]
    [SerializeField] private bool ignoreUIManager;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private bool pulseWhenChosen = true;
    [SerializeField] private ActiveCategoryStyle activeCategoryStyle = new ActiveCategoryStyle();

    public ActiveCategoryStyle OverrideCategoryStyle => ignoreUIManager ? activeCategoryStyle : null;

    private Graphic _outlineGraphic;
    private bool _isSelected;
    private bool _isHovered;
    private bool _isChosen;
    private Color? _chosenColorOverride;
    private Color _ownColor = Color.white;
    private CanvasGroup[] _parentGroups;
    private TMP_Text _label;
    private Color _labelBaseColour;

    private void Awake()
    {
        if (outline == null)
        {
            var child = transform.Find("Outline");
            if (child != null)
                outline = child.gameObject;
        }

        if (outline == null)
        {
            Debug.LogWarning($"SelectionOutline on '{name}' has no Outline child", this);
            enabled = false;
            return;
        }

        _outlineGraphic = outline.GetComponent<Graphic>();
        if (_outlineGraphic != null)
        {
            _outlineGraphic.raycastTarget = false;
            _ownColor = _outlineGraphic.color;
        }

        var parentCanvas = GetComponentInParent<Canvas>();
        int baseSortingOrder = parentCanvas != null ? parentCanvas.sortingOrder : 0;

        var canvas = outline.GetComponent<Canvas>();
        if (canvas == null)
            canvas = outline.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = baseSortingOrder + sortingOrderOffset;

        var ownGroup = outline.GetComponent<CanvasGroup>();
        if (ownGroup == null)
            ownGroup = outline.AddComponent<CanvasGroup>();
        ownGroup.ignoreParentGroups = true;
        ownGroup.interactable = false;
        ownGroup.blocksRaycasts = false;

        CacheParentGroups();
        CacheLabel();
        outline.SetActive(false);
    }

    private void OnTransformParentChanged() => CacheParentGroups();

    private void CacheLabel()
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (outline != null && text.transform.IsChildOf(outline.transform)) continue;
            _label = text;
            _labelBaseColour = text.color;
            return;
        }
    }

    private void ApplyLabelColour(bool highlighted)
    {
        if (_label == null || !ignoreUIManager || activeCategoryStyle == null || !activeCategoryStyle.changeText) return;
        _label.color = highlighted ? activeCategoryStyle.textColor : _labelBaseColour;
    }

    private void CacheParentGroups()
    {
        _parentGroups = GetComponentsInParent<CanvasGroup>(true);
    }

    private float ParentAlpha()
    {
        if (_parentGroups == null) return 1f;

        float alpha = 1f;
        foreach (var group in _parentGroups)
        {
            if (group == null || !group.enabled) continue;
            alpha *= group.alpha;
            if (group.ignoreParentGroups) break;
        }
        return alpha;
    }

    public void SetChosen(bool chosen, Color? colorOverride = null)
    {
        _isChosen = chosen;
        _chosenColorOverride = colorOverride;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        _isSelected = false;
        _isHovered = false;
        RefreshVisibility();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        RefreshVisibility();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        RefreshVisibility();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!UIManager.HoverAllowed) return;

        var selectable = GetComponent<Selectable>();
        bool navigable = selectable != null && selectable.navigation.mode != Navigation.Mode.None;
        if (navigable && selectable.IsInteractable() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
            return;
        }

        _isHovered = true;
        RefreshVisibility();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isHovered) return;

        _isHovered = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool visible = _isSelected || _isHovered || _isChosen;
        if (outline != null)
            outline.SetActive(visible);
        ApplyLabelColour(visible);
    }

    private void LateUpdate()
    {
        var eventSystem = EventSystem.current;
        bool selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
        if (selected == _isSelected) return;

        _isSelected = selected;
        RefreshVisibility();
    }

    private void Update()
    {
        if (_outlineGraphic == null || !outline.activeSelf) return;

        var ui = UIManager.Instance;
        bool useOwn = ignoreUIManager || ui == null;

        if (_isChosen && !_isSelected && !_isHovered && !pulseWhenChosen)
        {
            var staticColor = _chosenColorOverride ?? (useOwn ? _ownColor : ui.SelectionChosenColor);
            staticColor.a = maxAlpha * ParentAlpha();
            _outlineGraphic.color = staticColor;
            return;
        }

        float speed = useOwn ? pulseSpeed : ui.SelectionPulseSpeed;
        float pulse = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        var color = _isChosen && !_isSelected && !_isHovered
            ? _chosenColorOverride ?? (useOwn ? _ownColor : ui.SelectionChosenColor)
            : useOwn ? _ownColor : ui.SelectionHighlightColor;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, pulse) * ParentAlpha();
        _outlineGraphic.color = color;
    }
}