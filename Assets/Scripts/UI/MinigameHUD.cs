using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public struct MinigameResult
{
    public string Score;
    public string Best;
    public int Reward;
}

public class MinigameHUD : MonoBehaviour
{
    public static MinigameHUD Instance { get; private set; }

    [Header("Intro")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private UIPanel introUIPanel;
    [SerializeField] private TMP_Text introTitleText;
    [SerializeField] private TMP_Text introInstructionsText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button cancelButton;

    [Header("Countdown")]
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text countdownText;

    [Header("Results")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private UIPanel resultsUIPanel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button returnButton;

    private Action _onRetry;
    private Action _onReturn;
    private Action _onStart;
    private Action _onCancel;

    public bool HasIntro => introPanel != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        resultsPanel.SetActive(false);

        if (introPanel != null)
        {
            introPanel.SetActive(false);
            startButton.onClick.AddListener(() => _onStart?.Invoke());
            cancelButton.onClick.AddListener(() => _onCancel?.Invoke());

            if (introUIPanel != null)
                introUIPanel.OnBack += () => _onCancel?.Invoke();
        }

        retryButton.onClick.AddListener(() => _onRetry?.Invoke());
        returnButton.onClick.AddListener(() => _onReturn?.Invoke());

        if (resultsUIPanel != null)
            resultsUIPanel.OnBack += () => _onReturn?.Invoke();
    }

    public void ShowIntro(string title, string instructions, Action onStart, Action onCancel)
    {
        if (introPanel == null) return;

        _onStart = onStart;
        _onCancel = onCancel;

        if (introTitleText != null)
            introTitleText.text = title;

        if (introInstructionsText != null)
        {
            introInstructionsText.text = instructions ?? string.Empty;
            introInstructionsText.gameObject.SetActive(!string.IsNullOrEmpty(instructions));
        }

        UIPanelAnimator.SetVisible(introPanel, true);
    }

    public void HideIntro()
    {
        _onStart = null;
        _onCancel = null;

        if (introPanel != null)
            UIPanelAnimator.SetVisible(introPanel, false);
    }

    public void ShowCountdown(string text)
    {
        if (countdownRoot == null) return;
        countdownText.text = text;
        UIPanelAnimator.SetVisible(countdownRoot, true);
    }

    public void HideCountdown()
    {
        if (countdownRoot != null)
            UIPanelAnimator.SetVisible(countdownRoot, false);
    }

    public void ShowResults(MinigameResult result, Action onRetry, Action onReturn)
    {
        _onRetry = onRetry;
        _onReturn = onReturn;

        scoreText.text = result.Score;
        bestText.text = result.Best;

        rewardText.text = result.Reward > 0
            ? $"+{result.Reward} stat point{(result.Reward == 1 ? "" : "s")}"
            : string.Empty;

        UIPanelAnimator.SetVisible(resultsPanel, true);
    }

    public void HideResults()
    {
        _onRetry = null;
        _onReturn = null;
        UIPanelAnimator.SetVisible(resultsPanel, false);
    }
}