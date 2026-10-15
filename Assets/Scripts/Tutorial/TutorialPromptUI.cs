using UnityEngine;
using TMPro;

public class TutorialPromptUI : MonoBehaviour
{
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject successPanel;
    [SerializeField] private TMP_Text successText;

    private bool _promptVisible;
    private bool _successVisible;

    private void Awake()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
    }

    public void SetPromptText(string text)
    {
        if (promptText != null)
            promptText.text = text;
    }

    public void SetPromptVisible(bool visible)
    {
        if (visible == _promptVisible) return;
        _promptVisible = visible;
        UIPanelAnimator.SetVisible(promptPanel, visible);
    }

    public void SetSuccessText(string text)
    {
        if (successText != null)
            successText.text = text;
    }

    public void SetSuccessVisible(bool visible)
    {
        if (visible == _successVisible) return;
        _successVisible = visible;
        UIPanelAnimator.SetVisible(successPanel, visible);
    }

    public void HideAll()
    {
        SetPromptVisible(false);
        SetSuccessVisible(false);
    }
}
