using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectionOutline : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject outline;
    [SerializeField] private float minAlpha = 0.4f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private int sortingOrderOffset = 1;

    private Graphic _outlineGraphic;
    private bool _isSelected;
    private bool _isHovered;
    private bool _isChosen;
    private Color? _chosenColorOverride;

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
            _outlineGraphic.raycastTarget = false;

        var parentCanvas = GetComponentInParent<Canvas>();
        int baseSortingOrder = parentCanvas != null ? parentCanvas.sortingOrder : 0;

        var canvas = outline.GetComponent<Canvas>();
        if (canvas == null)
            canvas = outline.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = baseSortingOrder + sortingOrderOffset;

        outline.SetActive(false);
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
        _isHovered = true;
        RefreshVisibility();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (outline != null)
            outline.SetActive(_isSelected || _isHovered || _isChosen);
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
        var ui = UIManager.Instance;
        if (ui == null || _outlineGraphic == null || !outline.activeSelf) return;

        if (_isChosen && !_isSelected && !_isHovered)
        {
            var staticColor = _chosenColorOverride ?? ui.SelectionChosenColor;
            staticColor.a = maxAlpha;
            _outlineGraphic.color = staticColor;
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * ui.SelectionPulseSpeed) + 1f) * 0.5f;
        var color = ui.SelectionHighlightColor;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
        _outlineGraphic.color = color;
    }
}