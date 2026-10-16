using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ClickToAction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private InputActionReference action;
    [SerializeField] private Graphic graphic;
    [SerializeField] private string clickSound = "UIclick";
    [SerializeField] [Min(1f)] private float hoverScale = 1.1f;
    [SerializeField] [Range(0f, 1f)] private float hoverBrighten = 0.2f;
    [SerializeField] private UnityEvent onClicked;

    private Vector3 _baseScale = Vector3.one;
    private bool _hovered;
    private Color _baseColour = Color.white;

    private void Awake()
    {
        _baseScale = transform.localScale;
        if (graphic == null)
            graphic = GetComponent<Graphic>();
    }

    private void OnDisable()
    {
        SetHovered(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (!string.IsNullOrEmpty(clickSound) && ASpawner.Instance != null)
            ASpawner.Play(clickSound);

        onClicked?.Invoke();
        ActionPresser.Press(ActionPresser.Resolve(action), this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIManager.HoverAllowed)
            SetHovered(true);
    }
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    private void SetHovered(bool hovered)
    {
        if (hovered == _hovered) return;

        if (hovered && graphic != null)
            _baseColour = graphic.color;

        _hovered = hovered;
        transform.localScale = hovered ? _baseScale * hoverScale : _baseScale;

        if (graphic == null) return;

        if (!hovered)
        {
            graphic.color = _baseColour;
            return;
        }

        var bright = Color.Lerp(_baseColour, Color.white, hoverBrighten);
        bright.a = _baseColour.a;
        graphic.color = bright;
    }
}