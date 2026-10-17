using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class ConfirmationStyle
{
    public Color panelColour = Color.white;
    public Color secondaryColour = Color.white;
    public Color textColour = Color.black;
}

public class ConfirmationDialogueController : MonoBehaviour
{
    public static ConfirmationDialogueController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject background;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private Image panelImage;
    [SerializeField] private Image secondaryImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private string popSound = "ConfirmPop";

    private Action _onConfirm;
    private Action _onCancel;
    private TMP_Text _noLabel;
    private string _defaultNoText;
    private Color _defaultPanelColour = Color.white;
    private Color _defaultSecondaryColour = Color.white;
    private Color _defaultTextColour = Color.black;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel.SetActive(false);
        if (background != null)
            background.SetActive(false);

        yesButton.onClick.AddListener(Confirm);
        noButton.onClick.AddListener(Cancel);
        uiPanel.OnBack += Cancel;

        if (panelImage != null)
            _defaultPanelColour = panelImage.color;
        if (secondaryImage != null)
            _defaultSecondaryColour = secondaryImage.color;
        if (messageText != null)
            _defaultTextColour = messageText.color;

        _noLabel = noButton.GetComponentInChildren<TMP_Text>(true);
        if (_noLabel != null)
            _defaultNoText = _noLabel.text;
    }

    public void Show(string message, Action onConfirm, Action onCancel = null, bool focusCancel = false, bool allowConfirm = true, string cancelText = null, ConfirmationStyle style = null)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        messageText.text = message;
        messageText.color = style != null ? style.textColour : _defaultTextColour;

        if (panelImage != null)
            panelImage.color = style != null ? style.panelColour : _defaultPanelColour;

        if (secondaryImage != null)
            secondaryImage.color = style != null ? style.secondaryColour : _defaultSecondaryColour;

        yesButton.gameObject.SetActive(allowConfirm);

        if (_noLabel != null)
            _noLabel.text = string.IsNullOrEmpty(cancelText) ? _defaultNoText : cancelText;

        UIPanelAnimator.SetVisible(background, true);
        UIPanelAnimator.SetVisible(panel, true);

        if (!string.IsNullOrEmpty(popSound) && ASpawner.Instance != null)
            ASpawner.Play(popSound);

        if ((focusCancel || !allowConfirm) && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    private void Confirm()
    {
        if (!yesButton.gameObject.activeSelf) return;
        Close(_onConfirm);
    }

    private void Cancel() => Close(_onCancel);

    private void Close(Action callback)
    {
        _onConfirm = null;
        _onCancel = null;
        UIPanelAnimator.SetVisible(panel, false);
        UIPanelAnimator.SetVisible(background, false);
        callback?.Invoke();
    }
}