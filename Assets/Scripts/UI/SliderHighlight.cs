using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Slider))]
public class SliderHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Parts to recolour")]
    [SerializeField] private Image fill;
    [SerializeField] private Image handle;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    [Header("Highlight colours")]
    [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color handleColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] private Color labelColor = new Color(1f, 0.85f, 0.2f);

    [SerializeField] private bool highlightOnHover = true;

    private Color _fillNormal;
    private Color _handleNormal;
    private Color _backgroundNormal;
    private Color _labelNormal;
    private bool _isSelected;
    private bool _isHovered;
    private bool _initialized;

    private void Awake()
    {
        var slider = GetComponent<Slider>();
        if (fill == null && slider.fillRect != null)
            fill = slider.fillRect.GetComponent<Image>();
        if (handle == null && slider.handleRect != null)
            handle = slider.handleRect.GetComponent<Image>();

        if (fill != null) _fillNormal = fill.color;
        if (handle != null) _handleNormal = handle.color;
        if (background != null) _backgroundNormal = background.color;
        if (label != null) _labelNormal = label.color;

        _initialized = true;
    }

    private void LateUpdate()
    {
        var eventSystem = EventSystem.current;
        bool selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
        if (selected == _isSelected) return;

        _isSelected = selected;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        Refresh();
    }

    private void OnDisable()
    {
        _isSelected = false;
        _isHovered = false;
        Refresh();
    }

    private void Refresh()
    {
        if (!_initialized) return;

        bool highlighted = _isSelected || (highlightOnHover && _isHovered);

        if (fill != null) fill.color = highlighted ? fillColor : _fillNormal;
        if (handle != null) handle.color = highlighted ? handleColor : _handleNormal;
        if (background != null) background.color = highlighted ? backgroundColor : _backgroundNormal;
        if (label != null) label.color = highlighted ? labelColor : _labelNormal;
    }
}