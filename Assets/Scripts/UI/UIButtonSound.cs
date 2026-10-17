using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIButtonSound : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private string moveSound = "UIclick";
    [SerializeField] private string confirmSound = "UIconfirm";
    [SerializeField] private bool playOnHover = true;
    [SerializeField] [Min(0f)] private float minRepeatInterval = 0.08f;

    private static readonly System.Collections.Generic.Dictionary<string, float> LastPlayed = new System.Collections.Generic.Dictionary<string, float>();

    private Selectable _selectable;
    private Button _button;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _button = _selectable as Button;
    }

    private void OnEnable()
    {
        if (_button != null)
            _button.onClick.AddListener(PlayConfirm);
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(PlayConfirm);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is AxisEventData)
            Play(moveSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playOnHover && UIManager.HoverAllowed)
            Play(moveSound);
    }

    private void PlayConfirm() => Play(confirmSound);

    private void Play(string key)
    {
        if (string.IsNullOrEmpty(key) || ASpawner.Instance == null) return;
        if (_selectable != null && !_selectable.IsInteractable()) return;

        float now = Time.unscaledTime;
        if (LastPlayed.TryGetValue(key, out float last) && now - last < minRepeatInterval) return;
        LastPlayed[key] = now;

        ASpawner.Play(key);
    }
}