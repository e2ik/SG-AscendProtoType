using System;
using System.Collections;
using System.Collections.Generic;
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

    [SerializeField] private GameObject background;
    [SerializeField] [Min(0.01f)] private float backgroundStepDuration = 0.3f;

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
    [SerializeField] private string beepSound = "CountdownBeep";
    [SerializeField] [Min(0.1f)] private float beepStartPitch = 1f;
    [SerializeField] private float beepPitchStep = 0.12f;
    [SerializeField] private string goSound = "CountdownGo";
    [SerializeField] [Min(0.01f)] private float numberPopScale = 2f;
    [SerializeField] [Min(0.01f)] private float numberRestScale = 1f;
    [SerializeField] [Min(0f)] private float growPerStep = 0.2f;
    [SerializeField] [Min(0.01f)] private float goPopScale = 3.2f;
    [SerializeField] [Min(0.01f)] private float goRestScale = 1.6f;
    [SerializeField] [Min(0.01f)] private float shrinkDuration = 0.35f;
    [SerializeField] [Range(0f, 3f)] private float settleOvershoot = 1.7f;
    [SerializeField] private float popRotation = 8f;
    [SerializeField] private bool colourEachStep = true;
    [SerializeField] private List<Color> stepColours = new List<Color>
    {
        new Color32(0xFF, 0x6B, 0x6B, 0xFF),
        new Color32(0xFF, 0xB3, 0x47, 0xFF),
        new Color32(0xFF, 0xE0, 0x66, 0xFF)
    };
    [SerializeField] private Color goColour = new Color32(0x7C, 0xFC, 0x9A, 0xFF);
    [SerializeField] [Range(0f, 1f)] private float goPulseBrighten = 0.6f;
    [SerializeField] [Min(0f)] private float goPulsesPerSecond = 4f;

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
    private int _countdownStep;
    private bool _introShown;
    private bool _countdownShown;
    private bool _resultsShown;
    private CanvasGroup _backgroundGroup;
    private Coroutine _backgroundRoutine;
    private float _backgroundProgress;
    private int _countdownStart;
    private Coroutine _countdownRoutine;
    private Vector3 _countdownBaseScale = Vector3.one;

    public bool HasIntro => introPanel != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (background != null)
        {
            _backgroundGroup = background.GetComponent<CanvasGroup>();
            if (_backgroundGroup == null)
                _backgroundGroup = background.AddComponent<CanvasGroup>();
            background.SetActive(false);
        }
        if (countdownRoot != null)
            countdownRoot.SetActive(false);
        if (countdownText != null)
            _countdownBaseScale = countdownText.rectTransform.localScale;
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

        SetBackgroundProgress(0f, true);
        UIPanelAnimator.SetVisible(introPanel, true);
        _introShown = true;
        RefreshBackground();
    }

    public void HideIntro()
    {
        _onStart = null;
        _onCancel = null;

        if (introPanel != null)
            UIPanelAnimator.SetVisible(introPanel, false);
        _introShown = false;
        RefreshBackground();
    }

    private void SetBackgroundProgress(float progress, bool instant, float duration = -1f)
    {
        if (background == null) return;

        if (_backgroundRoutine != null)
        {
            StopCoroutine(_backgroundRoutine);
            _backgroundRoutine = null;
        }

        if (instant || !isActiveAndEnabled)
        {
            ApplyBackgroundProgress(progress);
            return;
        }

        _backgroundRoutine = StartCoroutine(BackgroundRoutine(progress, duration > 0f ? duration : backgroundStepDuration));
    }

    private IEnumerator BackgroundRoutine(float target, float duration)
    {
        float start = _backgroundProgress;
        bool linear = duration > backgroundStepDuration;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            float eased = linear ? p : 1f - (1f - p) * (1f - p);
            ApplyBackgroundProgress(Mathf.Lerp(start, target, eased));
            yield return null;
        }

        ApplyBackgroundProgress(target);
        _backgroundRoutine = null;
    }

    private void ApplyBackgroundProgress(float progress)
    {
        _backgroundProgress = progress;
        if (_backgroundGroup != null)
            _backgroundGroup.alpha = 1f - progress;
    }

    private void RefreshBackground()
    {
        if (background != null)
            UIPanelAnimator.SetVisible(background, _countdownShown || _resultsShown);
    }

    public void PrepareCountdown()
    {
        _countdownStep = 0;
        SetBackgroundProgress(0f, true);
        _countdownShown = true;
        RefreshBackground();
    }

    public void ShowCountdown(string text, float stepDuration = -1f)
    {
        if (countdownRoot == null) return;

        bool isNumber = int.TryParse(text, out int number);
        countdownText.text = text;
        UIPanelAnimator.SetVisible(countdownRoot, true);
        _countdownShown = true;
        RefreshBackground();

        string sound = isNumber ? beepSound : goSound;
        float pitch = isNumber ? beepStartPitch + _countdownStep * beepPitchStep : 1f;
        if (!string.IsNullOrEmpty(sound) && ASpawner.Instance != null)
            ASpawner.Play(sound, 1f, pitch);

        if (_countdownStep == 0)
        {
            _countdownStart = isNumber ? Mathf.Max(1, number) : 1;
            SetBackgroundProgress(0f, true);
        }

        float totalSteps = _countdownStart + 1f;
        bool synced = stepDuration > 0f;
        float progress = isNumber
            ? (_countdownStart - number + (synced ? 1f : 0f)) / totalSteps
            : synced ? 1f : _countdownStart / totalSteps;
        SetBackgroundProgress(progress, false, synced ? stepDuration : -1f);

        if (colourEachStep)
        {
            var colour = isNumber && stepColours.Count > 0
                ? stepColours[Mathf.Min(_countdownStep, stepColours.Count - 1)]
                : goColour;
            colour.a = countdownText.color.a;
            countdownText.color = colour;
        }

        float growth = 1f + _countdownStep * growPerStep;
        float pop = isNumber ? numberPopScale * growth : goPopScale;
        float rest = isNumber ? numberRestScale * growth : goRestScale;
        float rotation = (_countdownStep % 2 == 0 ? 1f : -1f) * popRotation;
        _countdownStep++;

        if (_countdownRoutine != null)
            StopCoroutine(_countdownRoutine);
        _countdownRoutine = StartCoroutine(PopRoutine(pop, rest, rotation, !isNumber));
    }

    private IEnumerator PopRoutine(float pop, float rest, float rotation, bool pulse)
    {
        var rect = countdownText.rectTransform;
        var colour = countdownText.color;

        for (float t = 0f; t < shrinkDuration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / shrinkDuration);
            float q = p - 1f;
            float eased = 1f + (settleOvershoot + 1f) * q * q * q + settleOvershoot * q * q;
            float scale = Mathf.LerpUnclamped(pop, rest, eased);

            rect.localScale = _countdownBaseScale * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation * (1f - Mathf.Clamp01(eased)));
            colour.a = Mathf.Clamp01(p * 4f);
            countdownText.color = colour;
            yield return null;
        }

        rect.localScale = _countdownBaseScale * rest;
        rect.localRotation = Quaternion.identity;
        colour.a = 1f;
        countdownText.color = colour;

        if (pulse && goPulsesPerSecond > 0f)
        {
            var bright = Color.Lerp(colour, Color.white, goPulseBrighten);
            bright.a = colour.a;

            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float wave = (Mathf.Sin(t * goPulsesPerSecond * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
                countdownText.color = Color.Lerp(colour, bright, wave);
                yield return null;
            }
        }

        _countdownRoutine = null;
    }

    public void HideCountdown()
    {
        _countdownStep = 0;

        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        if (countdownText != null)
            countdownText.rectTransform.localRotation = Quaternion.identity;

        if (countdownRoot != null)
            UIPanelAnimator.SetVisible(countdownRoot, false);
        _countdownShown = false;
        RefreshBackground();
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

        SetBackgroundProgress(0f, true);
        UIPanelAnimator.SetVisible(resultsPanel, true);
        _resultsShown = true;
        RefreshBackground();
    }

    public void HideResults()
    {
        _onRetry = null;
        _onReturn = null;
        UIPanelAnimator.SetVisible(resultsPanel, false);
        _resultsShown = false;
        RefreshBackground();
    }
}