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

    [Header("Speed")]
    [SerializeField] private float startSpeed = 7f;
    [SerializeField] private float maxSpeed = 16f;
    [SerializeField] private float acceleration = 0.25f;

    [Header("Reward")]
    [SerializeField] private float metresPerPoint = 150f;
    [SerializeField] [Min(0)] private int maxReward = 3;

    public float Speed { get; private set; }

    private void Awake()
    {
        player.Crashed += EndRun;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.Crashed -= EndRun;
    }

    protected override void OnSetup()
    {
        Speed = startSpeed;
        player.ResetRunner();
        player.SetControlsEnabled(false);
        spawner.Setup(player.JumpHeight);
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
        Speed = Mathf.Min(maxSpeed, Speed + acceleration * dt);
        Score += Speed * dt;

        spawner.Tick(Speed, dt, player.AirTime);
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