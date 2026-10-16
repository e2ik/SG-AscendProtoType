using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StylePicker : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private RectTransform contentParent;
    [SerializeField] private Button styleButtonPrefab;
    [SerializeField] private Color iconColour = Color.white;

    [Header("While Colour Picker Is Up")]
    [SerializeField] private List<GameObject> hideWhileCovered = new List<GameObject>();
    [SerializeField] private CanvasGroup dimTarget;
    [SerializeField] [Range(0f, 1f)] private float dimAlpha = 0.5f;
    [SerializeField] [Min(0f)] private float dimDuration = 0.15f;

    public event Action OnClosed;

    public bool IsOpen => panel.activeSelf && (_animator == null || !_animator.IsHiding);
    public Transform PanelRoot => panel.transform;

    private readonly List<Button> _buttons = new List<Button>();
    private Action<int> _onSelected;
    private Button _chosenButton;
    private bool _chosenVisible = true;
    private bool _covered;
    private UIPanelAnimator _animator;
    private Coroutine _focusRoutine;
    private Coroutine _dimRoutine;

    private void Awake()
    {
        _animator = panel.GetComponent<UIPanelAnimator>();
        panel.SetActive(false);

        if (uiPanel == null)
            uiPanel = panel.GetComponent<UIPanel>();

        if (dimTarget == null && contentParent != null)
        {
            dimTarget = contentParent.GetComponent<CanvasGroup>();
            if (dimTarget == null)
                dimTarget = contentParent.gameObject.AddComponent<CanvasGroup>();
        }

        if (uiPanel != null)
            uiPanel.OnBack += () => OnClosed?.Invoke();
    }

    public void Open(AppearanceStyleSet set, int current, Action<int> onSelected)
    {
        _onSelected = onSelected;
        SetCovered(false, true);
        Build(set, current);
        UIPanelAnimator.SetVisible(panel, true);
        Select(_chosenButton);
    }

    public void Hide()
    {
        if (IsOpen)
            UIPanelAnimator.SetVisible(panel, false);
    }

    public void Close()
    {
        if (!IsOpen) return;

        UIPanelAnimator.SetVisible(panel, false);
        OnClosed?.Invoke();
    }

    private void Update()
    {
        if (!IsOpen || (_animator != null && _animator.IsHiding)) return;

        var ui = UIManager.Instance;
        var top = ui != null ? ui.TopLayerRoot : null;
        bool covered = top != null && top != panel.transform && !top.IsChildOf(panel.transform) && !panel.transform.IsChildOf(top);

        if (covered == _covered) return;

        SetCovered(covered, false);
        if (!covered)
            Focus();
    }

    private void SetCovered(bool covered, bool instant)
    {
        _covered = covered;
        _chosenVisible = !covered;
        SetOutlineChosen(_chosenButton, _chosenVisible);

        foreach (var target in hideWhileCovered)
        {
            if (target != null)
                target.SetActive(!covered);
        }

        if (dimTarget == null) return;

        if (_dimRoutine != null)
        {
            StopCoroutine(_dimRoutine);
            _dimRoutine = null;
        }

        float alpha = covered ? dimAlpha : 1f;
        if (instant || dimDuration <= 0f || !isActiveAndEnabled)
            dimTarget.alpha = alpha;
        else
            _dimRoutine = StartCoroutine(DimRoutine(alpha));
    }

    private IEnumerator DimRoutine(float target)
    {
        float start = dimTarget.alpha;
        for (float t = 0f; t < dimDuration; t += Time.unscaledDeltaTime)
        {
            dimTarget.alpha = Mathf.Lerp(start, target, t / dimDuration);
            yield return null;
        }

        dimTarget.alpha = target;
        _dimRoutine = null;
    }

    public void Focus()
    {
        if (!isActiveAndEnabled) return;

        if (_focusRoutine != null)
            StopCoroutine(_focusRoutine);
        _focusRoutine = StartCoroutine(FocusNextFrame());
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null;
        _focusRoutine = null;

        if (IsOpen)
            Select(_chosenButton != null ? _chosenButton : _buttons.Count > 0 ? _buttons[0] : null);
    }

    private static void Select(Button button)
    {
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void Build(AppearanceStyleSet set, int current)
    {
        Clear();
        if (set == null || contentParent == null || styleButtonPrefab == null) return;

        for (int i = 0; i < set.Count; i++)
        {
            int index = i;
            var button = Instantiate(styleButtonPrefab, contentParent);
            var iconTransform = button.transform.Find("Icon");
            var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : button.image;

            if (icon != null)
            {
                icon.sprite = set.IconFor(i);
                icon.preserveAspect = true;
                icon.color = iconColour;
            }

            button.onClick.AddListener(() => Choose(index, button));
            _buttons.Add(button);

            if (i == current)
                SetChosenButton(button);
        }
    }

    private void Clear()
    {
        SetChosenButton(null);

        foreach (var button in _buttons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        _buttons.Clear();
    }

    private void Choose(int index, Button button)
    {
        SetChosenButton(button);
        _onSelected?.Invoke(index);
    }

    private void SetChosenButton(Button button)
    {
        SetOutlineChosen(_chosenButton, false);
        _chosenButton = button;
        SetOutlineChosen(_chosenButton, _chosenVisible);
    }

    private static void SetOutlineChosen(Button button, bool chosen)
    {
        if (button != null && button.TryGetComponent<SelectionOutline>(out var outline))
            outline.SetChosen(chosen);
    }
}