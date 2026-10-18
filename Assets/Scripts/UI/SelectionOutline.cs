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
    private Canvas _outlineCanvas;
    private bool _isSelected;
    private bool _isHovered;
    private bool _isChosen;
    private Color? _chosenColorOverride;
    private Color _ownColor = Color.white;
    private CanvasGroup[] _parentGroups;
    private TMP_Text _label;
    private Color _labelBaseColour;
    private bool _awoken;
    private RectTransform _pendingOverlay;
    private bool _usingOverlay;
    private RectTransform _outlineRect;
    private Vector2 _anchorMin;
    private Vector2 _anchorMax;
    private Vector2 _pivot;
    private Vector2 _anchoredPosition;
    private Vector2 _sizeDelta;
    private Vector3 _localScale;
    private Quaternion _localRotation;

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

        foreach (var graphic in outline.GetComponentsInChildren<MaskableGraphic>(true))
            graphic.maskable = false;

        _outlineGraphic = outline.GetComponent<Graphic>();
        if (_outlineGraphic != null)
        {
            _outlineGraphic.raycastTarget = false;
            _ownColor = _outlineGraphic.color;
        }

        _outlineCanvas = outline.GetComponent<Canvas>();
        if (_outlineCanvas == null)
            _outlineCanvas = outline.AddComponent<Canvas>();
        SyncOutlineSorting();

        var ownGroup = outline.GetComponent<CanvasGroup>();
        if (ownGroup == null)
            ownGroup = outline.AddComponent<CanvasGroup>();
        ownGroup.ignoreParentGroups = true;
        ownGroup.interactable = false;
        ownGroup.blocksRaycasts = false;

        CacheParentGroups();
        CacheLabel();
        outline.SetActive(false);

        _awoken = true;
        if (_pendingOverlay != null)
            ApplyOverlay(_pendingOverlay);
    }

    public void UseOverlay(RectTransform overlay)
    {
        if (overlay == null || _usingOverlay) return;
        if (_awoken) ApplyOverlay(overlay);
        else _pendingOverlay = overlay;
    }

    private void ApplyOverlay(RectTransform overlay)
    {
        _pendingOverlay = null;
        if (outline == null || _usingOverlay) return;

        _outlineRect = outline.transform as RectTransform;
        if (_outlineRect == null) return;

        _anchorMin = _outlineRect.anchorMin;
        _anchorMax = _outlineRect.anchorMax;
        _pivot = _outlineRect.pivot;
        _anchoredPosition = _outlineRect.anchoredPosition;
        _sizeDelta = _outlineRect.sizeDelta;
        _localScale = _outlineRect.localScale;
        _localRotation = _outlineRect.localRotation;

        if (_outlineCanvas != null)
        {
            Destroy(_outlineCanvas);
            _outlineCanvas = null;
        }

        _usingOverlay = true;
        _outlineRect.SetParent(overlay, false);
        _outlineRect.anchorMin = new Vector2(0.5f, 0.5f);
        _outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
        _outlineRect.pivot = _pivot;
        FollowButton();
    }

    private void FollowButton()
    {
        if (!_usingOverlay || _outlineRect == null) return;

        var self = (RectTransform)transform;
        var parent = _outlineRect.parent;
        Rect rect = self.rect;

        Vector2 size = Vector2.Scale(rect.size, _anchorMax - _anchorMin) + _sizeDelta;
        Vector2 reference = new Vector2(
            Mathf.Lerp(_anchorMin.x, _anchorMax.x, _pivot.x),
            Mathf.Lerp(_anchorMin.y, _anchorMax.y, _pivot.y));
        Vector2 local = rect.min + Vector2.Scale(rect.size, reference) + _anchoredPosition;

        Vector3 selfScale = self.lossyScale;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        _outlineRect.localScale = new Vector3(
            _localScale.x * SafeDivide(selfScale.x, parentScale.x),
            _localScale.y * SafeDivide(selfScale.y, parentScale.y),
            _localScale.z);
        _outlineRect.sizeDelta = size;
        _outlineRect.rotation = self.rotation * _localRotation;
        _outlineRect.position = self.TransformPoint(local);
    }

    private static float SafeDivide(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    private void OnEnable()
    {
        if (_awoken) RefreshVisibility();
    }

    private void OnDestroy()
    {
        if (_usingOverlay && outline != null)
            Destroy(outline);
    }

    private void OnTransformParentChanged() => CacheParentGroups();

    private void SyncOutlineSorting()
    {
        if (_outlineCanvas == null || _usingOverlay) return;

        var parentCanvas = transform.GetComponentInParent<Canvas>();
        if (parentCanvas == _outlineCanvas)
            parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;

        int layer = parentCanvas != null ? parentCanvas.sortingLayerID : 0;
        int order = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + sortingOrderOffset;

        if (!_outlineCanvas.enabled) _outlineCanvas.enabled = true;
        if (!_outlineCanvas.overrideSorting) _outlineCanvas.overrideSorting = true;
        if (_outlineCanvas.sortingLayerID != layer) _outlineCanvas.sortingLayerID = layer;
        if (_outlineCanvas.sortingOrder != order) _outlineCanvas.sortingOrder = order;
    }

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
        if (_usingOverlay && !isActiveAndEnabled) visible = false;
        if (outline != null)
        {
            outline.SetActive(visible);
            if (visible)
            {
                SyncOutlineSorting();
                if (_usingOverlay)
                {
                    if (_isSelected || _isHovered) outline.transform.SetAsLastSibling();
                    FollowButton();
                }
            }
        }
        ApplyLabelColour(visible);
    }

    private void LateUpdate()
    {
        var eventSystem = EventSystem.current;
        bool selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
        if (_usingOverlay && outline != null && outline.activeSelf) FollowButton();
        if (selected == _isSelected) return;

        _isSelected = selected;
        RefreshVisibility();
    }

    private void Update()
    {
        if (_outlineGraphic == null || !outline.activeSelf) return;

        SyncOutlineSorting();

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