using System;
using System.Collections;
using UnityEngine;

public abstract class MinigameBase : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        Intro,
        Countdown,
        Playing,
        Ended
    }

    [Header("Intro")]
    [SerializeField] private string displayName = "Minigame";
    [SerializeField] [TextArea(2, 4)] private string instructions;
    [SerializeField] private bool showIntro = true;

    [Header("Countdown")]
    [SerializeField] [Min(0)] private int countdownSeconds = 3;
    [SerializeField] [Min(0f)] private float countdownDelay = 1f;
    [SerializeField] [Min(0.1f)] private float countdownStepDuration = 1f;
    [SerializeField] [Min(0f)] private float goHoldDuration;
    [SerializeField] [Min(0f)] private float resultsDelay = 1.5f;
    [SerializeField] [Min(0)] private int minimumReward = 1;

    [Header("Play Area")]
    [SerializeField] private CanvasGroup playArea;

    public static MinigameBase Current { get; private set; }

    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    public float Score { get; protected set; }
    public float BestScore { get; private set; }
    public bool HasBest { get; private set; }

    protected virtual bool HigherIsBetter => true;
    protected virtual bool FailedRunsCount => true;

    public event Action GoShown;
    public event Action Began;
    public event Action Ended;

    private Coroutine _countdown;
    private Coroutine _results;
    private bool _completedRun;

    protected virtual void OnEnable()
    {
        Current = this;
    }

    protected virtual void OnDisable()
    {
        if (Current == this)
            Current = null;
    }

    protected virtual void Start()
    {
        StartRun(showIntro);
    }

    private void StartRun(bool withIntro)
    {
        if (_countdown != null)
        {
            StopCoroutine(_countdown);
            _countdown = null;
        }

        if (_results != null)
        {
            StopCoroutine(_results);
            _results = null;
        }

        Score = 0f;
        SetPlayAreaInteractable(false);
        OnSetup();

        var hud = MinigameHUD.Instance;
        if (withIntro && hud != null && hud.HasIntro)
        {
            CurrentPhase = Phase.Intro;
            var config = Config;
            string title = config != null && !string.IsNullOrEmpty(config.displayNameOverride) ? config.displayNameOverride : displayName;
            string text = config != null && !string.IsNullOrEmpty(config.instructionsOverride) ? config.instructionsOverride : instructions;
            hud.ShowIntro(title, BuildInstructions(text), BeginCountdown, CancelToWorld);
            return;
        }

        BeginCountdown();
    }

    private void BeginCountdown()
    {
        if (MinigameHUD.Instance != null)
            MinigameHUD.Instance.HideIntro();

        CurrentPhase = Phase.Countdown;
        _countdown = StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        var hud = MinigameHUD.Instance;
        if (hud != null) hud.PrepareCountdown();

        if (countdownDelay > 0f)
            yield return new WaitForSeconds(countdownDelay);

        for (int i = countdownSeconds; i > 0; i--)
        {
            if (hud != null) hud.ShowCountdown(i.ToString(), countdownStepDuration);
            yield return new WaitForSeconds(countdownStepDuration);
        }

        float goHold = goHoldDuration > 0f ? goHoldDuration : countdownStepDuration;
        if (hud != null) hud.ShowCountdown("GO!", goHold);
        GoShown?.Invoke();
        yield return new WaitForSeconds(goHold);

        if (hud != null) hud.HideCountdown();

        CurrentPhase = Phase.Playing;
        SetPlayAreaInteractable(true);
        OnBegin();
        Began?.Invoke();
        TutorialEvents.Raise(TutorialEvents.MinigameBegan, StateName);

        _countdown = null;
    }

    protected void EndRun(bool success = true)
    {
        if (CurrentPhase != Phase.Playing) return;

        CurrentPhase = Phase.Ended;
        _completedRun = true;
        SetPlayAreaInteractable(false);

        bool counts = success || FailedRunsCount;
        bool better = !HasBest || (HigherIsBetter ? Score > BestScore : Score < BestScore);
        if (counts && better)
        {
            BestScore = Score;
            HasBest = true;
        }

        OnEnd();
        Ended?.Invoke();
        TutorialEvents.Raise(success ? TutorialEvents.MinigameSucceeded : TutorialEvents.MinigameFailed, StateName);

        var hud = MinigameHUD.Instance;
        string runText = FormatRunResult(Score, success);
        string bestText = HasBest ? FormatScore(BestScore) : "-";

        if (hud == null)
        {
            Debug.Log($"{name} ended - {runText}, best {bestText}. No MinigameHUD found, retrying.");
            StartRun(false);
            return;
        }

        hud.HideCountdown();

        var result = new MinigameResult
        {
            Score = runText,
            Best = bestText,
            Reward = CurrentReward()
        };

        if (_results != null)
            StopCoroutine(_results);
        _results = StartCoroutine(ShowResultsAfterDelay(hud, result));
    }

    private IEnumerator ShowResultsAfterDelay(MinigameHUD hud, MinigameResult result)
    {
        if (resultsDelay > 0f)
            yield return new WaitForSeconds(resultsDelay);

        _results = null;
        if (hud != null)
            hud.ShowResults(result, Retry, ReturnToWorld);
    }

    public void Retry()
    {
        if (MinigameHUD.Instance != null)
            MinigameHUD.Instance.HideResults();

        StartRun(false);
    }

    private void CancelToWorld()
    {
        if (MinigameHUD.Instance != null)
            MinigameHUD.Instance.HideIntro();

        ReturnToWorld();
    }

    public void ReturnToWorld()
    {
        var hud = MinigameHUD.Instance;
        if (hud != null)
        {
            hud.HideIntro();
            hud.HideResults();
            hud.HideCountdown();
        }

        int reward = CurrentReward();

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteMinigame(reward > 0, reward);
        else
            Debug.Log($"{name} finished with reward {reward}. No GameManager - playing this scene on its own?");
    }

    private int CurrentReward()
    {
        if (!_completedRun) return 0;
        int earned = HasBest ? CalculateReward(BestScore) : 0;
        return Mathf.Max(minimumReward, earned);
    }

    private static string StateName => GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : null;

    protected virtual MinigameConfig Config =>
        GameManager.Instance != null ? GameManager.Instance.ActiveMinigameConfig : null;

    protected T GetConfig<T>() where T : MinigameConfig => Config as T;

    private void SetPlayAreaInteractable(bool interactable)
    {
        if (playArea == null) return;
        playArea.interactable = interactable;
        playArea.blocksRaycasts = interactable;
    }

    protected virtual string BuildInstructions(string template) => template;
    protected virtual string FormatRunResult(float score, bool success) => FormatScore(score);

    protected abstract void OnSetup();
    protected abstract void OnBegin();
    protected virtual void OnEnd() { }
    protected virtual int CalculateReward(float bestScore) => 0;
    protected virtual string FormatScore(float score) => Mathf.FloorToInt(score).ToString();
}