using System;
using UnityEngine;
using UnityEngine.UI;

public class UIPanel : MonoBehaviour
{
    [SerializeField] private Selectable defaultSelected;
    [SerializeField] private bool closeOnBack = true;
    [SerializeField] private bool blockKeyboardNavigation;

    public event Action OnBack;

    private void OnEnable()
    {
        if (UIManager.Instance == null) return;

        var selected = defaultSelected != null ? defaultSelected.gameObject : null;
        UIManager.Instance.PushBackHandler(HandleBack, transform, selected, blockKeyboardNavigation);
    }

    private void OnDisable()
    {
        if (UIManager.Instance == null) return;
        UIManager.Instance.PopBackHandler(HandleBack);
    }

    private void HandleBack()
    {
        OnBack?.Invoke();

        if (closeOnBack)
            gameObject.SetActive(false);
    }
}