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

    public Phase CurrentPhase { get; private set; } = Phase.Idle;
    public float Score { get; protected set; }
    public float BestScore { get; private set; }

    public event Action Began;
    public event Action Ended;

    private Coroutine _countdown;

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

        Score = 0f;
        OnSetup();

        var hud = MinigameHUD.Instance;
        if (withIntro && hud != null && hud.HasIntro)
        {
            CurrentPhase = Phase.Intro;
            hud.ShowIntro(displayName, instructions, BeginCountdown, CancelToWorld);
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

        for (int i = countdownSeconds; i > 0; i--)
        {
            if (hud != null) hud.ShowCountdown(i.ToString());
            yield return new WaitForSecondsRealtime(1f);
        }

        if (hud != null) hud.ShowCountdown("GO!");

        CurrentPhase = Phase.Playing;
        OnBegin();
        Began?.Invoke();

        yield return new WaitForSecondsRealtime(0.5f);

        if (hud != null) hud.HideCountdown();
        _countdown = null;
    }

    protected void EndRun()
    {
        if (CurrentPhase != Phase.Playing) return;

        CurrentPhase = Phase.Ended;
        BestScore = Mathf.Max(BestScore, Score);

        OnEnd();
        Ended?.Invoke();

        var hud = MinigameHUD.Instance;
        if (hud == null)
        {
            Debug.Log($"{name} ended - score {FormatScore(Score)}, best {FormatScore(BestScore)}. No MinigameHUD found, retrying.");
            StartRun(false);
            return;
        }

        hud.HideCountdown();
        hud.ShowResults(new MinigameResult
        {
            Score = FormatScore(Score),
            Best = FormatScore(BestScore),
            Reward = CalculateReward(BestScore)
        }, Retry, ReturnToWorld);
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

        int reward = CalculateReward(BestScore);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteMinigame(reward > 0, reward);
        else
            Debug.Log($"{name} finished with reward {reward}. No GameManager - playing this scene on its own?");
    }

    protected abstract void OnSetup();
    protected abstract void OnBegin();
    protected virtual void OnEnd() { }
    protected virtual int CalculateReward(float bestScore) => 0;
    protected virtual string FormatScore(float score) => Mathf.FloorToInt(score).ToString();
}