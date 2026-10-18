using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class RhythmGame : MinigameBase
{
    public enum Grade
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

    private enum MeterLayout
    {
        Bar,
        Ring
    }

    [Header("Ring Layout")]
    [SerializeField] private MeterLayout layout = MeterLayout.Bar;
    [SerializeField] [Range(0f, 0.9f)] private float ringMinRadius = 0.15f;
    [SerializeField] [Range(0.005f, 0.3f)] private float ringMarkerThickness = 0.05f;
    [SerializeField] private bool ringLoopInward;

    [Header("Hit Feedback")]
    [SerializeField] private UIRing hitPulsePrefab;
    [SerializeField] [Min(0.05f)] private float pulseDuration = 0.6f;
    [SerializeField] [Min(1f)] private float pulseMaxScale = 1.6f;
    [SerializeField] [Min(0f)] private float pulseTravel = 0.5f;
    [SerializeField] [Min(0.001f)] private float pulseStartThickness = 0.08f;
    [SerializeField] [Min(0.001f)] private float pulseEndThickness = 0.02f;
    [SerializeField] [Min(1)] private int perfectPulseCount = 2;
    [SerializeField] [Min(0f)] private float pulseStagger = 0.1f;
    [SerializeField] private bool pulseOnPress = true;
    [SerializeField] private bool punchOnPress;
    [SerializeField] private bool tintMarkerOnHit = true;
    [SerializeField] private RectTransform swellTarget;
    [SerializeField] [Min(1f)] private float meterPunchScale = 1.06f;
    [SerializeField] [Min(0.01f)] private float meterPunchDuration = 0.15f;

    [Header("Cycle Pop")]
    [SerializeField] private RectTransform cyclePopTarget;
    [SerializeField] [Min(1f)] private float cyclePopScale = 1.12f;
    [SerializeField] [Min(0.01f)] private float cyclePopDuration = 0.3f;

    [Header("Hold")]
    [SerializeField] [Range(0f, 1f)] private float minZoneShift = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float maxZoneShift = 0.6f;
    [SerializeField] [Min(0f)] private float holdRevealDelay = 1f;
    [SerializeField] [Min(0f)] private float zoneFadeOutDuration = 0.15f;
    [SerializeField] [Min(0f)] private float zoneFadeInDuration = 0.4f;
    [SerializeField] [Min(0.05f)] private float holdSpeedMultiplier = 1f;
    [SerializeField] private RectTransform holdTrail;
    [SerializeField] private bool trailAlways = true;
    [SerializeField] [Range(0f, 1f)] private float trailIdleAlpha = 0.5f;
    [SerializeField] [Min(0.1f)] private float trailFollowSpeed = 6f;
    [SerializeField] [Range(0f, 1f)] private float trailMaxLength = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float trailFade = 0.8f;

    [Header("Breathing")]
    [SerializeField] private bool inhaleMode = true;
    [SerializeField] [Min(0.01f)] private float inhaleRate = 0.25f;
    [SerializeField] [Min(0.01f)] private float exhaleRate = 0.5f;
    [SerializeField] [Min(0f)] private float zoneTransitionDuration = 0.5f;
    [SerializeField] private Color breathColor = new Color(0.45f, 0.85f, 0.9f, 1f);
    [SerializeField] [Range(0f, 1f)] private float breathColorStrength = 0.6f;
    [SerializeField] [Min(0.1f)] private float breathBlendSpeed = 3f;
    [SerializeField] [Min(0f)] private float meterSwell = 0.06f;
    [SerializeField] private List<PixelWarpController> calmTargets = new List<PixelWarpController>();
    [SerializeField] [Range(0f, 1f)] private float calmAmplitude = 0.3f;
    [SerializeField] private string holdLoopSound;
    [SerializeField] [Min(0.1f)] private float holdPitchRise = 1.15f;
    [SerializeField] private string breatheOutSound;

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
    [SerializeField] [Min(0f)] private float feedbackFadeIn = 0.25f;
    [SerializeField] [Min(0f)] private float feedbackFadeOut = 0.3f;
    [SerializeField] [Min(1f)] private float feedbackBounceScale = 1.12f;
    [SerializeField] [Min(0.01f)] private float feedbackBounceDuration = 0.2f;

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
    [SerializeField] [Range(0f, 1f)] private float previewMarkerPosition = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float previewZoneCenter = 0.5f;

    [Header("Sounds")]
    [SerializeField] private string perfectSound;
    [SerializeField] private string goodSound;
    [SerializeField] private string okaySound;
    [SerializeField] private string badSound;

    private readonly List<Image> _indicators = new List<Image>();
    private readonly List<float> _results = new List<float>();
    private readonly List<GameObject> _pulses = new List<GameObject>();
    private readonly List<Graphic> _zoneGraphics = new List<Graphic>();
    private readonly List<Color> _zoneBaseColors = new List<Color>();
    private readonly List<float> _calmBaseAmplitudes = new List<float>();

    private Graphic _markerGraphic;
    private Color _markerBaseColor;
    private bool _markerColorCaptured;
    private Vector3 _meterBaseScale;
    private bool _meterScaleCaptured;
    private bool _visualsCaptured;
    private float _punchFactor = 1f;
    private Graphic _trailGraphic;
    private float _trailBaseAlpha = 1f;
    private string _feedbackCurrent;
    private float _feedbackAlpha;
    private Vector3 _feedbackBaseScale = Vector3.one;
    private bool _feedbackScaleCaptured;
    private Coroutine _feedbackFade;
    private Coroutine _feedbackBounce;
    private float _popFactor = 1f;
    private Vector3 _popBaseScale;
    private bool _popScaleCaptured;
    private float _holdBlend;
    private AudioSource _holdLoop;
    private float _holdLoopPitch = 1f;

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
    private float _direction = 1f;
    private bool _exhaling;
    private float _holdDirection = 1f;
    private float _pressPosition;
    private float _zoneCenter;
    private float _sizeScale = 1f;
    private bool _waiting;
    private bool _holding;
    private float _pressValue;
    private float _trailPosition;
    private float _markerPosition;
    private Coroutine _slide;
    private float _zoneAlpha = 1f;
    private float _targetCenter;
    private bool _targetChosen;

    public event System.Action HoldStarted;
    public event System.Action TargetRevealed;
    public event System.Action<Grade> HoldReleased;
    public event System.Action<float> RunFinished;

    public int AttemptIndex => _attemptIndex;
    public int Attempts => _attempts;

    protected override MinigameConfig Config =>
        base.Config != null ? base.Config : testConfig;

    protected override void Start()
    {
        if (InputManager.Instance != null)
            _pressAction = InputManager.Instance.FindAction(pressActionPath, this);

        base.Start();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopHoldLoop();
        _holdBlend = 0f;
        if (_visualsCaptured)
            ApplyBreathVisuals();
    }

    protected override void OnSetup()
    {
        StopAllCoroutines();
        _slide = null;
        CaptureVisuals();
        ClearHitFeedback();
        ApplyConfig();

        _attemptIndex = 0;
        _results.Clear();
        _speed = _baseSpeed;
        _waiting = false;

        BuildIndicators();
        ResetFeedback();
        PrepareAttempt(true);
    }

    protected override void OnBegin() => PopCycle();

    private RectTransform PopTarget => cyclePopTarget != null ? cyclePopTarget : track;

    private void PopCycle()
    {
        if (cyclePopScale > 1f && PopTarget != null)
            StartCoroutine(CyclePopRoutine());
    }

    private IEnumerator CyclePopRoutine()
    {
        for (float t = 0f; t < cyclePopDuration; t += Time.deltaTime)
        {
            float p = t / cyclePopDuration;
            float wave = p < 0.35f
                ? Mathf.Lerp(1f, cyclePopScale, p / 0.35f)
                : 1f + (cyclePopScale - 1f) * Mathf.Cos((p - 0.35f) / 0.65f * Mathf.PI * 1.5f) * (1f - (p - 0.35f) / 0.65f);
            _popFactor = wave;
            yield return null;
        }
        _popFactor = 1f;
    }

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

    private void CaptureVisuals()
    {
        if (_visualsCaptured) return;
        _visualsCaptured = true;

        foreach (var zone in new[] { okayZone, goodZone, perfectZone })
        {
            if (zone != null && zone.TryGetComponent<Graphic>(out var graphic))
            {
                _zoneGraphics.Add(graphic);
                _zoneBaseColors.Add(graphic.color);
            }
        }

        if (holdTrail != null && holdTrail.TryGetComponent(out _trailGraphic))
            _trailBaseAlpha = _trailGraphic.color.a;

        foreach (var target in calmTargets)
            _calmBaseAmplitudes.Add(target != null ? target.AmplitudeMultiplier : 1f);

        var pop = PopTarget;
        if (pop != null && !_popScaleCaptured)
        {
            _popBaseScale = pop.localScale;
            _popScaleCaptured = true;
        }

        var root = swellTarget;
        if (root != null && !_meterScaleCaptured)
        {
            _meterBaseScale = root.localScale;
            _meterScaleCaptured = true;
        }
    }

    private float SpeedScale => _baseSpeed > 0f ? _speed / _baseSpeed : 1f;

    private void PrepareAttempt(bool first)
    {
        float half = _okaySize * 0.5f;
        float high = 1f - half;

        float center = _randomizeZone ? Random.Range(half, high) : Mathf.Clamp(0.5f, half, high);
        _holding = false;

        if (_slide != null)
        {
            StopCoroutine(_slide);
            _slide = null;
        }

        RestoreMarkerColor();
        SetTrailVisible(false);
        PlaceTrackRing();
        _zoneAlpha = 1f;

        if (first || zoneTransitionDuration <= 0f)
        {
            _zoneCenter = center;
            _sizeScale = 1f;
            PlaceZones(_zoneCenter);
        }
        else
        {
            _slide = StartCoroutine(SlideZones(center, 1f, zoneTransitionDuration));
        }

        if (first)
        {
            _markerPosition = 0f;
            _direction = 1f;
            _exhaling = false;
            _trailPosition = 0f;
            PlaceMarker(_markerPosition);
            DrawTrail(0f, 0f);
        }
    }

    private void PlaceZones(float center)
    {
        PlaceZone(okayZone, center, _okaySize * _sizeScale);
        PlaceZone(goodZone, center, _goodSize * _sizeScale);
        PlaceZone(perfectZone, center, _perfectSize * _sizeScale);
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Playing || Time.timeScale <= 0f) return;

        AdvanceMarker(Time.deltaTime);
        PlaceMarker(_markerPosition);
        if (trailAlways || _holding)
            UpdateTrail(_markerPosition);

        if (_waiting) return;

        float position = _markerPosition;

        if (!_holding)
        {
            if (PressedThisFrame())
                OnPress(position);
            return;
        }

        if (_holdLoop != null)
            _holdLoop.pitch = _holdLoopPitch * Mathf.Lerp(1f, holdPitchRise, Mathf.Clamp01(Mathf.Abs(position - _pressPosition) / Mathf.Max(0.01f, maxZoneShift)));

        if (!IsHeld())
            OnRelease(position);
    }

    private void AdvanceMarker(float deltaTime)
    {
        if (_holding && inhaleMode)
        {
            _markerPosition += _holdDirection * inhaleRate * SpeedScale * holdSpeedMultiplier * deltaTime;
            if (_markerPosition > 1f)
            {
                _markerPosition = 2f - _markerPosition;
                _holdDirection = -1f;
            }
            else if (_markerPosition < 0f)
            {
                _markerPosition = -_markerPosition;
                _holdDirection = 1f;
            }
            _markerPosition = Mathf.Clamp01(_markerPosition);
            _direction = _holdDirection;
            return;
        }

        if (_exhaling)
        {
            _markerPosition = Mathf.MoveTowards(_markerPosition, 0f, exhaleRate * SpeedScale * deltaTime);
            _direction = -1f;
            if (_markerPosition <= 0f)
            {
                _exhaling = false;
                _direction = 1f;
            }
            return;
        }

        float rate = _speed * 2f * (_holding ? holdSpeedMultiplier : 1f);

        if (IsRing && ringLoopInward && !inhaleMode)
        {
            _markerPosition = Mathf.Repeat(_markerPosition - rate * 0.5f * deltaTime, 1f);
            return;
        }

        _markerPosition += _direction * rate * deltaTime;
        if (_markerPosition > 1f)
        {
            _markerPosition = 2f - _markerPosition;
            _direction = -1f;
        }
        else if (_markerPosition < 0f)
        {
            _markerPosition = -_markerPosition;
            _direction = 1f;
        }
        _markerPosition = Mathf.Clamp01(_markerPosition);
    }

    private void LateUpdate()
    {
        if (!_visualsCaptured) return;

        float target = _holding && CurrentPhase == Phase.Playing ? 1f : 0f;
        _holdBlend = Mathf.MoveTowards(_holdBlend, target, breathBlendSpeed * Time.deltaTime);
        ApplyBreathVisuals();
    }

    private void ApplyBreathVisuals()
    {
        float tint = _holdBlend * breathColorStrength;
        for (int i = 0; i < _zoneGraphics.Count; i++)
        {
            if (_zoneGraphics[i] == null) continue;
            var baseColor = _zoneBaseColors[i];
            var colour = Color.Lerp(baseColor, new Color(breathColor.r, breathColor.g, breathColor.b, baseColor.a), tint);
            colour.a = baseColor.a * _zoneAlpha;
            _zoneGraphics[i].color = colour;
        }

        var root = swellTarget;
        var pop = PopTarget;
        bool shared = root != null && root == pop;

        if (root != null && _meterScaleCaptured)
            root.localScale = _meterBaseScale * _punchFactor * (1f + meterSwell * _holdBlend * _markerPosition) * (shared ? _popFactor : 1f);

        if (_trailGraphic != null && trailAlways)
        {
            var trailColour = _trailGraphic.color;
            trailColour.a = _trailBaseAlpha * Mathf.Lerp(trailIdleAlpha, 1f, _holdBlend);
            _trailGraphic.color = trailColour;
        }

        if (!shared && pop != null && _popScaleCaptured)
            pop.localScale = _popBaseScale * _popFactor;

        for (int i = 0; i < calmTargets.Count && i < _calmBaseAmplitudes.Count; i++)
        {
            if (calmTargets[i] == null) continue;
            float baseAmplitude = _calmBaseAmplitudes[i];
            calmTargets[i].AmplitudeMultiplier = Mathf.Lerp(baseAmplitude, baseAmplitude * calmAmplitude, _holdBlend);
        }
    }

    private void OnPress(float position)
    {
        var grade = GradeAt(position);
        _pressValue = ValueOf(grade);
        _holding = true;
        _exhaling = false;
        _holdDirection = _direction >= 0f ? 1f : -1f;
        _pressPosition = position;

        if (!inhaleMode)
            SetFeedback(TextOf(grade), ColorOf(grade));

        PlaySound(SoundOf(grade));
        PlayHitFeedback(grade, position, true);
        StartHoldLoop();

        if (!trailAlways)
        {
            _trailPosition = position;
            DrawTrail(position, position);
        }
        SetTrailVisible(true);

        _targetChosen = false;
        if (_slide != null)
            StopCoroutine(_slide);
        _slide = StartCoroutine(RevealZones());
        HoldStarted?.Invoke();
    }

    private IEnumerator RevealZones()
    {
        float pressTime = Time.time;

        yield return FadeZones(0f, zoneFadeOutDuration);

        float wait = holdRevealDelay - (Time.time - pressTime);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        _targetCenter = NextZoneCenter(_markerPosition, 1f, _holdDirection);
        _targetChosen = true;
        _zoneCenter = _targetCenter;
        _sizeScale = 1f;
        PlaceZones(_zoneCenter);
        TargetRevealed?.Invoke();

        yield return FadeZones(1f, zoneFadeInDuration);
        _slide = null;
    }

    private IEnumerator FadeZones(float target, float duration)
    {
        float start = _zoneAlpha;
        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                _zoneAlpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
        }
        _zoneAlpha = target;
    }

    private void OnRelease(float position)
    {
        _holding = false;
        if (_slide != null)
        {
            StopCoroutine(_slide);
            _slide = null;
        }

        if (!_targetChosen)
        {
            _targetCenter = NextZoneCenter(position, 1f, _holdDirection);
            _targetChosen = true;
        }

        _zoneCenter = _targetCenter;
        _sizeScale = 1f;
        PlaceZones(_zoneCenter);
        if (_zoneAlpha < 1f)
            _slide = StartCoroutine(FadeZones(1f, Mathf.Min(zoneFadeInDuration, 0.15f)));

        _exhaling = inhaleMode && _holdDirection > 0f;
        _direction = -_holdDirection;

        var grade = GradeAt(position);
        float combined = (_pressValue + ValueOf(grade)) * 0.5f;
        _results.Add(combined);

        if (_attemptIndex < _indicators.Count && _indicators[_attemptIndex] != null)
            _indicators[_attemptIndex].color = ColorOf(GradeForValue(combined));

        SetTrailVisible(false);
        StopHoldLoop();
        PlaySound(breatheOutSound);
        PlaySound(SoundOf(grade));
        PlayHitFeedback(grade, position, false);
        HoldReleased?.Invoke(grade);

        StartCoroutine(NextAttempt(grade));
    }

    private float NextZoneCenter(float markerPosition, float scale, float direction)
    {
        float half = _okaySize * scale * 0.5f;
        float low = half;
        float high = 1f - half;
        if (high <= low) return 0.5f;

        float shiftMin = Mathf.Min(minZoneShift, maxZoneShift);
        float shiftMax = Mathf.Max(minZoneShift, maxZoneShift);
        float previous = _zoneCenter;

        bool FarEnough(float c) =>
            Mathf.Abs(c - previous) >= shiftMin && Mathf.Abs(c - markerPosition) >= shiftMin;

        if (inhaleMode)
        {
            for (int i = 0; i < 12; i++)
            {
                float offset = Random.Range(shiftMin, shiftMax) * direction;
                float candidate = Mathf.Clamp(markerPosition + offset, low, high);
                if (FarEnough(candidate)) return candidate;
            }
        }

        for (int i = 0; i < 24; i++)
        {
            float candidate = Random.Range(low, high);
            if (FarEnough(candidate)) return candidate;
        }

        float best = low;
        float bestScore = -1f;
        for (int i = 0; i <= 20; i++)
        {
            float candidate = Mathf.Lerp(low, high, i / 20f);
            float score = Mathf.Min(Mathf.Abs(candidate - previous), Mathf.Abs(candidate - markerPosition));
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private IEnumerator SlideZones(float target, float targetScale, float duration)
    {
        float start = _zoneCenter;
        float startScale = _sizeScale;

        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float p = t / duration;
                float eased = p * p * (3f - 2f * p);
                _zoneCenter = Mathf.Lerp(start, target, eased);
                _sizeScale = Mathf.Lerp(startScale, targetScale, eased);
                PlaceZones(_zoneCenter);
                yield return null;
            }
        }

        _zoneCenter = target;
        _sizeScale = targetScale;
        PlaceZones(_zoneCenter);
        _slide = null;
    }

    private void StartHoldLoop()
    {
        StopHoldLoop();
        if (string.IsNullOrEmpty(holdLoopSound) || ASpawner.Instance == null) return;

        _holdLoop = ASpawner.Play(holdLoopSound);
        if (_holdLoop != null)
            _holdLoopPitch = _holdLoop.pitch;
    }

    private void StopHoldLoop()
    {
        if (_holdLoop == null) return;
        _holdLoop.pitch = _holdLoopPitch;
        ASpawner.Stop(_holdLoop);
        _holdLoop = null;
    }

    private void UpdateTrail(float position)
    {
        _trailPosition = Mathf.Lerp(_trailPosition, position, 1f - Mathf.Exp(-trailFollowSpeed * Time.deltaTime));
        float offset = _trailPosition - position;
        if (Mathf.Abs(offset) > trailMaxLength)
            _trailPosition = position + Mathf.Sign(offset) * trailMaxLength;

        DrawTrail(_trailPosition, position);
    }

    private void SetTrailVisible(bool visible)
    {
        visible |= trailAlways;
        if (holdTrail != null && holdTrail.gameObject.activeSelf != visible)
            holdTrail.gameObject.SetActive(visible);
    }

    private void DrawTrail(float tail, float head)
    {
        if (holdTrail == null) return;

        if (IsRing)
        {
            var ring = RingOf(holdTrail);
            if (ring == null) return;

            float tailRadius = RingRadius(tail);
            float headRadius = RingRadius(head);
            float half = ringMarkerThickness * 0.5f;
            ring.SetRadii(Mathf.Min(tailRadius, headRadius) - half, Mathf.Max(tailRadius, headRadius) + half);
            ring.RadialFade = tailRadius <= headRadius ? trailFade : -trailFade;
            return;
        }

        if (track == null || marker == null) return;

        float length = TrackLength();
        float tailOffset = Mathf.Lerp(-length * 0.5f, length * 0.5f, tail);
        float headOffset = Mathf.Lerp(-length * 0.5f, length * 0.5f, head);
        var position = holdTrail.anchoredPosition;
        var size = holdTrail.sizeDelta;

        if (vertical)
        {
            position.y = (tailOffset + headOffset) * 0.5f;
            size.y = Mathf.Abs(headOffset - tailOffset) + marker.rect.height;
        }
        else
        {
            position.x = (tailOffset + headOffset) * 0.5f;
            size.x = Mathf.Abs(headOffset - tailOffset) + marker.rect.width;
        }

        holdTrail.anchoredPosition = position;
        holdTrail.sizeDelta = size;
    }

    private bool IsRing => layout == MeterLayout.Ring;

    private float RingRadius(float normalized) =>
        Mathf.Lerp(ringMinRadius, 1f - ringMarkerThickness * 0.5f, normalized);

    private static UIRing RingOf(RectTransform target) =>
        target != null && target.TryGetComponent<UIRing>(out var ring) ? ring : null;

    private Grade GradeAt(float position)
    {
        float distance = Mathf.Abs(position - _zoneCenter);
        return distance <= _perfectSize * _sizeScale * 0.5f ? Grade.Perfect
            : distance <= _goodSize * _sizeScale * 0.5f ? Grade.Good
            : distance <= _okaySize * _sizeScale * 0.5f ? Grade.Okay
            : Grade.Bad;
    }

    private Grade GradeForValue(float value) =>
        value >= perfectValue ? Grade.Perfect
        : value >= goodValue ? Grade.Good
        : value >= okayValue ? Grade.Okay
        : Grade.Bad;

    private IEnumerator NextAttempt(Grade releaseGrade)
    {
        _waiting = true;

        SetFeedback(TextOf(releaseGrade), ColorOf(releaseGrade));
        yield return new WaitForSeconds(feedbackPause);

        SetFeedback(null, Color.white);
        _attemptIndex++;

        if (_attemptIndex >= _attempts)
        {
            Score = Average();
            RunFinished?.Invoke(Score);
            EndRun(true);
            yield break;
        }

        _speed += _speedIncrease;
        PrepareAttempt(false);
        PopCycle();
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

    private bool IsHeld()
    {
        if (_pressAction != null) return _pressAction.IsPressed();

        return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
    }

    private void PlayHitFeedback(Grade grade, float position, bool isPress)
    {
        var colour = ColorOf(grade);

        if (tintMarkerOnHit && CaptureMarkerColor())
            _markerGraphic.color = new Color(colour.r, colour.g, colour.b, _markerBaseColor.a);

        if (IsRing && (!isPress || pulseOnPress))
        {
            int count = !isPress && grade == Grade.Perfect ? perfectPulseCount : 1;
            float radius = RingRadius(position);
            for (int i = 0; i < count; i++)
                StartCoroutine(PulseRoutine(radius, colour, i * pulseStagger));
        }

        if (!isPress || punchOnPress)
            StartCoroutine(PunchRoutine());
    }

    private bool CaptureMarkerColor()
    {
        if (_markerColorCaptured) return _markerGraphic != null;
        _markerColorCaptured = true;
        if (marker != null && marker.TryGetComponent(out _markerGraphic))
            _markerBaseColor = _markerGraphic.color;
        return _markerGraphic != null;
    }

    private void RestoreMarkerColor()
    {
        if (_markerColorCaptured && _markerGraphic != null)
            _markerGraphic.color = _markerBaseColor;
    }

    private void ClearHitFeedback()
    {
        foreach (var pulse in _pulses)
        {
            if (pulse != null)
                Destroy(pulse);
        }
        _pulses.Clear();

        StopHoldLoop();
        RestoreMarkerColor();

        _punchFactor = 1f;
        _popFactor = 1f;
        _holdBlend = 0f;
        _markerPosition = 0f;
        if (_visualsCaptured)
            ApplyBreathVisuals();
    }

    private IEnumerator PulseRoutine(float startRadius, Color colour, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var template = hitPulsePrefab != null ? hitPulsePrefab : RingOf(marker);
        if (template == null || marker == null) yield break;

        var pulse = Instantiate(template, marker.parent);
        pulse.name = "HitPulse";
        _pulses.Add(pulse.gameObject);

        var rect = (RectTransform)pulse.transform;
        rect.anchorMin = marker.anchorMin;
        rect.anchorMax = marker.anchorMax;
        rect.pivot = marker.pivot;
        rect.anchoredPosition = marker.anchoredPosition;
        rect.sizeDelta = marker.sizeDelta;
        rect.localRotation = marker.localRotation;
        rect.localScale = marker.localScale * pulseMaxScale;
        rect.SetSiblingIndex(marker.GetSiblingIndex() + 1);

        pulse.PixelResolution = template.PixelResolution * pulseMaxScale;
        var graphic = pulse.Graphic;
        graphic.raycastTarget = false;

        for (float t = 0f; t < pulseDuration; t += Time.deltaTime)
        {
            float p = t / pulseDuration;
            float eased = 1f - (1f - p) * (1f - p);
            float radius = Mathf.Lerp(startRadius, startRadius + pulseTravel, eased);
            float thickness = Mathf.Lerp(pulseStartThickness, pulseEndThickness, p);
            pulse.SetBand(radius / pulseMaxScale, thickness / pulseMaxScale);
            graphic.color = new Color(colour.r, colour.g, colour.b, colour.a * (1f - p));
            yield return null;
        }

        _pulses.Remove(pulse.gameObject);
        Destroy(pulse.gameObject);
    }

    private IEnumerator PunchRoutine()
    {
        if (meterPunchScale <= 1f) yield break;

        for (float t = 0f; t < meterPunchDuration; t += Time.deltaTime)
        {
            float p = t / meterPunchDuration;
            _punchFactor = Mathf.Lerp(meterPunchScale, 1f, p * p);
            yield return null;
        }
        _punchFactor = 1f;
    }

    private void PlaceTrackRing()
    {
        if (!IsRing) return;

        var ring = RingOf(track);
        if (ring != null)
            ring.SetRadii(Mathf.Max(0f, ringMinRadius - ringMarkerThickness * 0.5f), 1f);
    }

    private void PlaceMarker(float normalized)
    {
        if (IsRing)
        {
            var ring = RingOf(marker);
            if (ring != null)
                ring.SetBand(RingRadius(normalized), ringMarkerThickness);
            return;
        }

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
        if (IsRing)
        {
            var ring = RingOf(zone);
            if (ring != null)
            {
                float span = 1f - ringMarkerThickness * 0.5f - ringMinRadius;
                ring.SetBand(RingRadius(center), size * span);
            }
            return;
        }

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

    private void ResetFeedback()
    {
        if (feedbackText == null) return;
        CaptureFeedbackScale();

        if (_feedbackFade != null) StopCoroutine(_feedbackFade);
        if (_feedbackBounce != null) StopCoroutine(_feedbackBounce);
        _feedbackFade = null;
        _feedbackBounce = null;

        _feedbackCurrent = null;
        _feedbackAlpha = 0f;
        feedbackText.alpha = 0f;
        feedbackText.rectTransform.localScale = _feedbackBaseScale;
        feedbackText.gameObject.SetActive(false);
    }

    private void CaptureFeedbackScale()
    {
        if (_feedbackScaleCaptured || feedbackText == null) return;
        _feedbackScaleCaptured = true;
        _feedbackBaseScale = feedbackText.rectTransform.localScale;
    }

    private void SetFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        CaptureFeedbackScale();

        if (string.IsNullOrEmpty(text))
        {
            if (_feedbackCurrent == null) return;
            _feedbackCurrent = null;
            FadeFeedback(0f, feedbackFadeOut, true);
            return;
        }

        bool changed = text != _feedbackCurrent;
        bool wasShowing = _feedbackCurrent != null && feedbackText.gameObject.activeSelf;

        if (!feedbackText.gameObject.activeSelf)
        {
            _feedbackAlpha = 0f;
            feedbackText.gameObject.SetActive(true);
        }

        feedbackText.text = text;
        feedbackText.color = color;
        feedbackText.alpha = _feedbackAlpha;
        _feedbackCurrent = text;

        FadeFeedback(1f, wasShowing ? Mathf.Min(feedbackFadeIn, 0.1f) : feedbackFadeIn, false);

        if (changed)
        {
            if (_feedbackBounce != null) StopCoroutine(_feedbackBounce);
            _feedbackBounce = StartCoroutine(BounceFeedback());
        }
    }

    private void FadeFeedback(float target, float duration, bool hideAtEnd)
    {
        if (_feedbackFade != null) StopCoroutine(_feedbackFade);
        _feedbackFade = StartCoroutine(FadeFeedbackRoutine(target, duration, hideAtEnd));
    }

    private IEnumerator FadeFeedbackRoutine(float target, float duration, bool hideAtEnd)
    {
        float start = _feedbackAlpha;
        if (duration > 0f)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                _feedbackAlpha = Mathf.Lerp(start, target, t / duration);
                feedbackText.alpha = _feedbackAlpha;
                yield return null;
            }
        }

        _feedbackAlpha = target;
        feedbackText.alpha = target;
        if (hideAtEnd && target <= 0f && _feedbackCurrent == null)
            feedbackText.gameObject.SetActive(false);
        _feedbackFade = null;
    }

    private IEnumerator BounceFeedback()
    {
        var rect = feedbackText.rectTransform;
        for (float t = 0f; t < feedbackBounceDuration; t += Time.deltaTime)
        {
            float p = t / feedbackBounceDuration;
            float scale = p < 0.4f
                ? Mathf.Lerp(1f, feedbackBounceScale, p / 0.4f)
                : Mathf.Lerp(feedbackBounceScale, 1f, (p - 0.4f) / 0.6f);
            rect.localScale = _feedbackBaseScale * scale;
            yield return null;
        }
        rect.localScale = _feedbackBaseScale;
        _feedbackBounce = null;
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

#if UNITY_EDITOR
    [ContextMenu("Preview Meter")]
    private void PreviewMeter()
    {
        if (Application.isPlaying) return;
        ApplyConfig();

        float half = _okaySize * 0.5f;
        float center = Mathf.Clamp(previewZoneCenter, half, 1f - half);
        DrawPreview(center);
    }

    [ContextMenu("Preview Meter (Random Zone)")]
    private void PreviewMeterRandom()
    {
        if (Application.isPlaying) return;
        ApplyConfig();

        float half = _okaySize * 0.5f;
        previewZoneCenter = Random.Range(half, 1f - half);
        DrawPreview(previewZoneCenter);
    }

    private void DrawPreview(float center)
    {
        var targets = new List<Object>();
        foreach (var rect in new[] { track, marker, okayZone, goodZone, perfectZone })
        {
            if (rect == null) continue;
            targets.Add(rect);
            var ring = RingOf(rect);
            if (ring != null) targets.Add(ring);
        }
        targets.Add(this);
        UnityEditor.Undo.RecordObjects(targets.ToArray(), "Preview Rhythm Meter");

        _zoneCenter = center;
        PlaceTrackRing();
        PlaceZones(center);
        PlaceMarker(previewMarkerPosition);

        foreach (var target in targets)
            UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || !isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying || _okaySize <= 0f) return;
            float half = _okaySize * 0.5f;
            DrawPreviewQuiet(Mathf.Clamp(previewZoneCenter, half, 1f - half));
        };
    }

    private void DrawPreviewQuiet(float center)
    {
        if (!IsRing) return;
        PlaceTrackRing();
        PlaceZones(center);
        PlaceMarker(previewMarkerPosition);
        UnityEditor.SceneView.RepaintAll();
    }
#endif

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