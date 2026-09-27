using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ConfirmationDialogueController : MonoBehaviour
{
    public static ConfirmationDialogueController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private Action _onConfirm;
    private Action _onCancel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel.SetActive(false);
        yesButton.onClick.AddListener(Confirm);
        noButton.onClick.AddListener(Cancel);
        uiPanel.OnBack += Cancel;
    }

    public void Show(string message, Action onConfirm, Action onCancel = null, bool focusCancel = false)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        messageText.text = message;
        UIPanelAnimator.SetVisible(panel, true);

        if (focusCancel && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    private void Confirm() => Close(_onConfirm);

    private void Cancel() => Close(_onCancel);

    private void Close(Action callback)
    {
        _onConfirm = null;
        _onCancel = null;
        UIPanelAnimator.SetVisible(panel, false);
        callback?.Invoke();
    }
}