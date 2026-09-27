using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndlessRunnerGame : MinigameBase
{
    [Header("References")]
    [SerializeField] private RunnerPlayer player;
    [SerializeField] private RunnerObstacleSpawner spawner;
    [SerializeField] private List<LoopingScroller> scrollers = new List<LoopingScroller>();
    [SerializeField] private TMP_Text distanceText;

    [Header("Difficulty")]
    [SerializeField] private float timeToMaxDifficulty = 90f;
    [SerializeField] private float startSpeed = 7f;
    [SerializeField] private float maxSpeed = 16f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Reward")]
    [SerializeField] private float metresPerPoint = 150f;
    [SerializeField] [Min(0)] private int maxReward = 3;

    public float Speed { get; private set; }
    public float Difficulty { get; private set; }

    private float _elapsed;

    private void Awake()
    {
        player.Crashed += OnCrashed;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Crashed -= OnCrashed;
    }

    private void OnCrashed() => EndRun();

    protected override void OnSetup()
    {
        _elapsed = 0f;
        Difficulty = 0f;
        Speed = startSpeed;
        player.ResetRunner();
        player.SetControlsEnabled(false);
        spawner.Setup(player);
        UpdateDistanceText();
    }

    protected override void OnBegin()
    {
        player.SetControlsEnabled(true);
    }

    protected override void OnEnd()
    {
        player.SetControlsEnabled(false);
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Playing) return;

        float dt = Time.deltaTime;
        _elapsed += dt;

        Difficulty = timeToMaxDifficulty > 0f ? Mathf.Clamp01(_elapsed / timeToMaxDifficulty) : 1f;
        Speed = Mathf.Lerp(startSpeed, maxSpeed, Mathf.Clamp01(speedCurve.Evaluate(Difficulty)));
        Score += Speed * dt;

        spawner.Tick(Speed, dt, Difficulty);
        foreach (var scroller in scrollers)
        {
            if (scroller != null)
                scroller.Tick(Speed, dt);
        }

        UpdateDistanceText();
    }

    private void UpdateDistanceText()
    {
        if (distanceText != null)
            distanceText.text = FormatScore(Score);
    }

    protected override int CalculateReward(float bestScore)
    {
        if (metresPerPoint <= 0f) return 0;
        return Mathf.Min(maxReward, Mathf.FloorToInt(bestScore / metresPerPoint));
    }

    protected override string FormatScore(float score) => $"{Mathf.FloorToInt(score)} m";
}