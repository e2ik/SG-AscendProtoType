using System;
using UnityEngine;
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

    public void Show(string message, Action onConfirm, Action onCancel = null)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        messageText.text = message;
        panel.SetActive(true);
    }

    private void Confirm() => Close(_onConfirm);

    private void Cancel() => Close(_onCancel);

    private void Close(Action callback)
    {
        _onConfirm = null;
        _onCancel = null;
        panel.SetActive(false);
        callback?.Invoke();
    }
}