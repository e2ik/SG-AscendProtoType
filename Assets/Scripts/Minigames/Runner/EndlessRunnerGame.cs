using System.Collections;
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

    [Header("Sounds")]
    [SerializeField] private string jumpSound = "RunnerJump";
    [SerializeField] private string crashSound = "RunnerCrash";

    [Header("Distance Milestones")]
    [SerializeField] [Min(1f)] private float milestoneDistance = 50f;
    [SerializeField] private string milestoneSound = "RunnerClear";
    [SerializeField] private Color milestoneColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] [Min(1f)] private float milestonePulseScale = 1.4f;
    [SerializeField] [Min(0.05f)] private float milestonePulseDuration = 0.45f;
    [SerializeField] [Min(1)] private int milestoneFlashes = 2;

    [Header("Reward")]
    [SerializeField] private float metresPerPoint = 150f;
    [SerializeField] [Min(0)] private int maxReward = 3;

    public float Speed { get; private set; }
    public float Difficulty { get; private set; }

    private float _elapsed;
    private int _milestonesReached;
    private Coroutine _milestoneRoutine;
    private Vector3 _distanceBaseScale = Vector3.one;
    private Color _distanceBaseColor = Color.white;
    private readonly List<ParticleSystem> _scrollingEffects = new List<ParticleSystem>();
    private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[64];

    private void Awake()
    {
        player.Crashed += OnCrashed;
        player.Jumped += OnJumped;
        player.GroundEffectSpawned += OnGroundEffectSpawned;

        if (distanceText != null)
        {
            _distanceBaseScale = distanceText.rectTransform.localScale;
            _distanceBaseColor = distanceText.color;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.Crashed -= OnCrashed;
            player.Jumped -= OnJumped;
            player.GroundEffectSpawned -= OnGroundEffectSpawned;
        }
    }

    private void OnGroundEffectSpawned(ParticleSystem effect)
    {
        if (effect != null && !_scrollingEffects.Contains(effect))
            _scrollingEffects.Add(effect);
    }

    private void ScrollEffects(float distance)
    {
        for (int i = _scrollingEffects.Count - 1; i >= 0; i--)
        {
            var effect = _scrollingEffects[i];
            if (effect == null || !effect.gameObject.activeInHierarchy)
            {
                _scrollingEffects.RemoveAt(i);
                continue;
            }

            effect.transform.position += Vector3.left * distance;

            foreach (var system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) continue;

                int count = system.particleCount;
                if (count == 0) continue;
                if (_particleBuffer.Length < count)
                    _particleBuffer = new ParticleSystem.Particle[count];

                count = system.GetParticles(_particleBuffer, count);
                for (int p = 0; p < count; p++)
                    _particleBuffer[p].position += Vector3.left * distance;
                system.SetParticles(_particleBuffer, count);
            }
        }
    }

    private void OnCrashed()
    {
        PlaySound(crashSound);
        EndRun();
    }

    private void OnJumped()
    {
        if (CurrentPhase == Phase.Playing)
            PlaySound(jumpSound);
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }

    protected override void OnSetup()
    {
        _elapsed = 0f;
        _milestonesReached = 0;
        Difficulty = 0f;
        Speed = startSpeed;
        player.ResetRunner();
        player.SetControlsEnabled(false);
        spawner.Setup(player);
        ResetDistanceText();
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
        ScrollEffects(Speed * dt);
        foreach (var scroller in scrollers)
        {
            if (scroller != null)
                scroller.Tick(Speed, dt);
        }

        UpdateDistanceText();
        CheckMilestone();
    }

    private void CheckMilestone()
    {
        int reached = Mathf.FloorToInt(Score / milestoneDistance);
        if (reached <= _milestonesReached) return;

        _milestonesReached = reached;
        PlaySound(milestoneSound);

        if (distanceText == null) return;

        if (_milestoneRoutine != null)
            StopCoroutine(_milestoneRoutine);
        _milestoneRoutine = StartCoroutine(MilestonePulse());
    }

    private IEnumerator MilestonePulse()
    {
        var rect = distanceText.rectTransform;

        for (float t = 0f; t < milestonePulseDuration; t += Time.deltaTime)
        {
            float p = t / milestonePulseDuration;
            float pulse = Mathf.Sin(p * Mathf.PI);
            rect.localScale = _distanceBaseScale * Mathf.Lerp(1f, milestonePulseScale, pulse);

            float flash = Mathf.Abs(Mathf.Sin(p * Mathf.PI * milestoneFlashes));
            distanceText.color = Color.Lerp(_distanceBaseColor, milestoneColor, flash);
            yield return null;
        }

        _milestoneRoutine = null;
        ResetDistanceText();
    }

    private void ResetDistanceText()
    {
        if (_milestoneRoutine != null)
        {
            StopCoroutine(_milestoneRoutine);
            _milestoneRoutine = null;
        }

        if (distanceText == null) return;
        distanceText.rectTransform.localScale = _distanceBaseScale;
        distanceText.color = _distanceBaseColor;
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