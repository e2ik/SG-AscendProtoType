using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class RhythmGame : MinigameBase
{
    private enum Grade
    {
        Perfect,
        Good,
        Okay,
        Bad
    }

    [Header("Meter")]
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform okayZone;
    [SerializeField] private RectTransform goodZone;
    [SerializeField] private RectTransform perfectZone;
    [SerializeField] private bool vertical = true;

    [Header("Defaults (used without a config)")]
    [SerializeField] [Min(1)] private int attempts = 3;
    [SerializeField] [Min(0.05f)] private float cyclesPerSecond = 0.8f;
    [SerializeField] [Min(0f)] private float speedIncreasePerAttempt = 0.15f;
    [SerializeField] [Range(0.02f, 1f)] private float okaySize = 0.3f;
    [SerializeField] [Range(0.01f, 1f)] private float goodSize = 0.16f;
    [SerializeField] [Range(0.01f, 1f)] private float perfectSize = 0.06f;
    [SerializeField] private bool randomizeZone = true;
    [SerializeField] [Min(0)] private int maxReward = 3;
    [SerializeField] private List<float> rewardThresholds = new List<float> { 0.3f, 0.6f, 0.9f };

    [Header("Scoring")]
    [SerializeField] [Range(0f, 1f)] private float perfectValue = 1f;
    [SerializeField] [Range(0f, 1f)] private float goodValue = 0.75f;
    [SerializeField] [Range(0f, 1f)] private float okayValue = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float badValue = 0f;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private string perfectText = "PERFECT!";
    [SerializeField] private string goodText = "GOOD";
    [SerializeField] private string okayText = "OKAY";
    [SerializeField] private string badText = "MISS";
    [SerializeField] private float feedbackPause = 0.6f;

    [Header("Attempt Indicators")]
    [SerializeField] private Transform attemptsParent;
    [SerializeField] private Image attemptIndicatorPrefab;
    [SerializeField] private Color pendingColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color perfectColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color goodColor = new Color(0.4f, 0.9f, 0.4f);
    [SerializeField] private Color okayColor = new Color(0.4f, 0.7f, 1f);
    [SerializeField] private Color badColor = new Color(0.9f, 0.3f, 0.3f);

    [Header("Input")]
    [SerializeField] private string pressActionPath = "Rhythm/Press";

    [Header("Intelligence Bonus")]
    [SerializeField] private RhythmZoneBonus zoneBonus;
    [SerializeField] [Min(0)] private int intelligenceOverride;

    [Header("Testing")]
    [SerializeField] private RhythmGameConfig testConfig;

    [Header("Sounds")]
    [SerializeField] private string perfectSound;
    [SerializeField] private string goodSound;
    [SerializeField] private string okaySound;
    [SerializeField] private string badSound;

    private readonly List<Image> _indicators = new List<Image>();
    private readonly List<float> _results = new List<float>();

    private InputAction _pressAction;
    private int _attempts;
    private float _baseSpeed;
    private float _speedIncrease;
    private float _okaySize;
    private float _goodSize;
    private float _perfectSize;
    private bool _randomizeZone;
    private int _maxReward;
    private List<float> _thresholds;

    private int _attemptIndex;
    private float _speed;
    private float _phase;
    private float _zoneCenter;
    private bool _waiting;

    protected override MinigameConfig Config =>
        base.Config != null ? base.Config : testConfig;

    protected override void Start()
    {
        if (InputManager.Instance != null)
            _pressAction = InputManager.Instance.FindAction(pressActionPath, this);

        base.Start();
    }

    protected override void OnSetup()
    {
        StopAllCoroutines();
        ApplyConfig();

        _attemptIndex = 0;
        _results.Clear();
        _speed = _baseSpeed;
        _waiting = false;

        BuildIndicators();
        SetFeedback(null, Color.white);
        PrepareAttempt();
    }

    protected override void OnBegin() { }

    private void ApplyConfig()
    {
        var config = GetConfig<RhythmGameConfig>();

        if (base.Config != null && config == null)
            Debug.LogWarning($"RhythmGame was started with a {base.Config.GetType().Name} - expected a RhythmGameConfig, using defaults", this);

        _attempts = config != null ? config.attempts : attempts;
        _baseSpeed = config != null ? config.cyclesPerSecond : cyclesPerSecond;
        _speedIncrease = config != null ? config.speedIncreasePerAttempt : speedIncreasePerAttempt;
        float bonus = IntelligenceBonus();
        _okaySize = Mathf.Min(1f, (config != null ? config.okaySize : okaySize) + bonus);
        _goodSize = Mathf.Min(_okaySize, (config != null ? config.goodSize : goodSize) + bonus);
        _perfectSize = Mathf.Min(_goodSize, (config != null ? config.perfectSize : perfectSize) + bonus);
        _randomizeZone = config != null ? config.randomizeZone : randomizeZone;
        _maxReward = config != null ? config.maxReward : maxReward;
        _thresholds = config != null && config.rewardThresholds != null && config.rewardThresholds.Count > 0
            ? config.rewardThresholds
            : rewardThresholds;
    }

    private float IntelligenceBonus()
    {
        if (zoneBonus == null) return 0f;

        int intelligence = intelligenceOverride > 0 ? intelligenceOverride : SavedIntelligence();
        return zoneBonus.BonusFor(intelligence);
    }

    private static int SavedIntelligence()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && save.stats != null ? save.stats.intelligence : PlayerStats.StartingValue;
    }

    private void BuildIndicators()
    {
        foreach (var indicator in _indicators)
        {
            if (indicator == null) continue;
            indicator.gameObject.SetActive(false);
            Destroy(indicator.gameObject);
        }
        _indicators.Clear();

        if (attemptsParent == null || attemptIndicatorPrefab == null) return;

        for (int i = 0; i < _attempts; i++)
        {
            var indicator = Instantiate(attemptIndicatorPrefab, attemptsParent);
            indicator.color = pendingColor;
            _indicators.Add(indicator);
        }
    }

    private void PrepareAttempt()
    {
        float half = _okaySize * 0.5f;
        _zoneCenter = _randomizeZone ? Random.Range(half, 1f - half) : 0.5f;
        _phase = Random.value;

        PlaceZone(okayZone, _zoneCenter, _okaySize);
        PlaceZone(goodZone, _zoneCenter, _goodSize);
        PlaceZone(perfectZone, _zoneCenter, _perfectSize);
        PlaceMarker(MarkerPosition());
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Playing || _waiting || Time.timeScale <= 0f) return;

        _phase += Time.deltaTime * _speed * 2f;
        float position = MarkerPosition();
        PlaceMarker(position);

        if (PressedThisFrame())
            Judge(position);
    }

    private float MarkerPosition() => Mathf.PingPong(_phase, 1f);

    private void Judge(float position)
    {
        float distance = Mathf.Abs(position - _zoneCenter);
        var grade = distance <= _perfectSize * 0.5f ? Grade.Perfect
            : distance <= _goodSize * 0.5f ? Grade.Good
            : distance <= _okaySize * 0.5f ? Grade.Okay
            : Grade.Bad;

        _results.Add(ValueOf(grade));

        if (_attemptIndex < _indicators.Count && _indicators[_attemptIndex] != null)
            _indicators[_attemptIndex].color = ColorOf(grade);

        SetFeedback(TextOf(grade), ColorOf(grade));
        PlaySound(SoundOf(grade));

        StartCoroutine(NextAttempt());
    }

    private IEnumerator NextAttempt()
    {
        _waiting = true;
        yield return new WaitForSeconds(feedbackPause);

        SetFeedback(null, Color.white);
        _attemptIndex++;

        if (_attemptIndex >= _attempts)
        {
            Score = Average();
            EndRun(true);
            yield break;
        }

        _speed += _speedIncrease;
        PrepareAttempt();
        _waiting = false;
    }

    private float Average()
    {
        if (_results.Count == 0) return 0f;

        float total = 0f;
        foreach (float value in _results)
            total += value;
        return total / _results.Count;
    }

    private bool PressedThisFrame()
    {
        if (_pressAction != null) return _pressAction.WasPressedThisFrame();

        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private void PlaceMarker(float normalized)
    {
        if (marker == null || track == null) return;

        float length = TrackLength();
        var position = marker.anchoredPosition;
        float offset = Mathf.Lerp(-length * 0.5f, length * 0.5f, normalized);

        if (vertical) position.y = offset;
        else position.x = offset;

        marker.anchoredPosition = position;
    }

    private void PlaceZone(RectTransform zone, float center, float size)
    {
        if (zone == null || track == null) return;

        float length = TrackLength();
        var position = zone.anchoredPosition;
        var sizeDelta = zone.sizeDelta;
        float offset = Mathf.Lerp(-length * 0.5f, length * 0.5f, center);

        if (vertical)
        {
            position.y = offset;
            sizeDelta.y = size * length;
        }
        else
        {
            position.x = offset;
            sizeDelta.x = size * length;
        }

        zone.anchoredPosition = position;
        zone.sizeDelta = sizeDelta;
    }

    private float TrackLength() => vertical ? track.rect.height : track.rect.width;

    private void SetFeedback(string text, Color color)
    {
        if (feedbackText == null) return;

        bool show = !string.IsNullOrEmpty(text);
        feedbackText.gameObject.SetActive(show);
        if (!show) return;

        feedbackText.text = text;
        feedbackText.color = color;
    }

    private float ValueOf(Grade grade) => grade switch
    {
        Grade.Perfect => perfectValue,
        Grade.Good => goodValue,
        Grade.Okay => okayValue,
        _ => badValue
    };

    private Color ColorOf(Grade grade) => grade switch
    {
        Grade.Perfect => perfectColor,
        Grade.Good => goodColor,
        Grade.Okay => okayColor,
        _ => badColor
    };

    private string TextOf(Grade grade) => grade switch
    {
        Grade.Perfect => perfectText,
        Grade.Good => goodText,
        Grade.Okay => okayText,
        _ => badText
    };

    private string SoundOf(Grade grade) => grade switch
    {
        Grade.Perfect => perfectSound,
        Grade.Good => goodSound,
        Grade.Okay => okaySound,
        _ => badSound
    };

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }

    protected override int CalculateReward(float bestAverage)
    {
        int reward = 0;
        foreach (float threshold in _thresholds)
        {
            if (bestAverage >= threshold)
                reward++;
        }
        return Mathf.Min(reward, _maxReward);
    }

    protected override string FormatScore(float average) => $"{Mathf.RoundToInt(average * 100f)}%";

    protected override string BuildInstructions(string template) =>
        (template ?? string.Empty).Replace("{attempts}", _attempts.ToString());
}