using System;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    [SerializeField] private Selectable defaultSelected;
    [SerializeField] private bool closeOnBack = true;
    [SerializeField] private bool blockKeyboardNavigation;
    [SerializeField] private bool keyboardSelectsOnNavigate;

    public event Action OnBack;

    public bool KeyboardSelectsOnNavigate
    {
        get => keyboardSelectsOnNavigate;
        set => keyboardSelectsOnNavigate = value;
    }

    private bool _registered;

    private void OnEnable() => Register();
    private void OnDisable() => Unregister();

    public void Register()
    {
        if (_registered || UIManager.Instance == null) return;

        _registered = true;
        var selected = defaultSelected != null ? defaultSelected.gameObject : null;
        UIManager.Instance.PushBackHandler(HandleBack, transform, selected, blockKeyboardNavigation, keyboardSelectsOnNavigate);
    }

    public void Unregister()
    {
        if (!_registered) return;

        _registered = false;
        if (UIManager.Instance != null)
            UIManager.Instance.PopBackHandler(HandleBack);
    }

    private void HandleBack()
    {
        OnBack?.Invoke();

        if (closeOnBack)
            UIPanelAnimator.SetVisible(gameObject, false);
    }
}