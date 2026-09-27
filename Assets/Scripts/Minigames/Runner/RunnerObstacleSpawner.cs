using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RunnerObstacleSpawner : MonoBehaviour
{
    private struct ObstacleOption
    {
        public RunnerObstacle Prefab;
        public float Height;
        public float Width;
    }

    private struct AirOption
    {
        public RunnerFlyer Prefab;
        public float Width;
        public float HalfHeight;
        public float BaseY;
        public float Amplitude;
        public float Sway;
        public bool CanFlyHigh;

        public float SpacingWidth(float swayFactor) => Mathf.Max(0f, Width + Sway * 2f * swayFactor);
    }

    private struct LowFlyerPlan
    {
        public RunnerFlyer Prefab;
        public float CenterAboveGround;
        public float Bob;
        public float Height;
        public float Width;
        public float SwayOffset;
        public bool HasCompanion;
        public ObstacleOption Companion;
    }

    private struct ActiveObstacle
    {
        public Transform Transform;
        public RunnerFlyer Flyer;
    }

    private enum QueuedSpawn
    {
        None,
        Air,
        Ground,
        LowFlyer
    }

    [SerializeField] private List<RunnerObstacle> obstaclePrefabs = new List<RunnerObstacle>();
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float despawnDistance = 30f;
    [SerializeField] private float firstObstacleDelay = 1.5f;

    [Header("Spacing (seconds of warning between obstacles)")]
    [SerializeField] private Vector2 easyGapSeconds = new Vector2(1.4f, 2.2f);
    [SerializeField] private Vector2 hardGapSeconds = new Vector2(0.7f, 1.1f);
    [SerializeField] private float reactionTime = 0.15f;

    [Header("Obstacle Height")]
    [SerializeField] [Range(0.3f, 1f)] private float clearanceFactor = 0.8f;
    [SerializeField] [Range(0.1f, 1f)] private float easyHeightFraction = 0.5f;
    [SerializeField] [Range(0.5f, 1f)] private float clearSafety = 0.9f;

    [Header("Tap Jumps")]
    [SerializeField] [Range(0f, 1f)] private float tapStartDifficulty = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float maxTapChance = 0.5f;
    [SerializeField] private float tapHoldTime = 0.08f;

    [Header("Air Obstacles")]
    [SerializeField] private List<RunnerFlyer> airPrefabs = new List<RunnerFlyer>();
    [SerializeField] [Range(0f, 1f)] private float airStartDifficulty = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float maxAirChance = 0.35f;
    [SerializeField] [Range(0.5f, 1.2f)] private float airHeightFactor = 1f;
    [SerializeField] private float airGroundClearance = 0.15f;
    [FormerlySerializedAs("swaySpacing")]
    [SerializeField] [Range(-1f, 1f)] private float swaySpacingEasy = 1f;
    [SerializeField] [Range(-1f, 1f)] private float swaySpacingHard = -1f;

    [Header("Low Flyers")]
    [SerializeField] [Range(0f, 1f)] private float lowFlyerStartDifficulty = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float maxLowFlyerChance = 0.3f;
    [SerializeField] private float lowFlyerMinAltitude = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float lowFlyerCompanionChance = 0.5f;
    [SerializeField] private float lowFlyerCompanionGap = 0.15f;

    [Header("Needle (jump between)")]
    [SerializeField] [Range(0f, 1f)] private float needleStartDifficulty = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float maxNeedleChance = 0.3f;
    [SerializeField] private float needleWindow = 0.4f;
    [SerializeField] private float needleHeadroom = 0.1f;
    [SerializeField] [Range(0.5f, 1f)] private float needleMaxHopFraction = 0.85f;

    [Header("Tolerance")]
    [SerializeField] [Range(0f, 1f)] private float toleranceAtMaxDifficulty = 0f;

    [Header("Combos")]
    [SerializeField] [Range(0f, 1f)] private float comboStartDifficulty = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float maxComboChance = 0.45f;
    [SerializeField] [Range(0f, 1f)] private float maxBirdFollowUpChance = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float maxTapComboChance = 0.35f;
    [SerializeField] private float comboLandingMargin = 0.1f;
    [SerializeField] private float comboReactionTime = 0.12f;

    private readonly List<ActiveObstacle> _active = new List<ActiveObstacle>();
    private readonly List<AirOption> _airOptions = new List<AirOption>();
    private readonly List<AirOption> _highOptions = new List<AirOption>();
    private readonly List<ObstacleOption> _options = new List<ObstacleOption>();
    private readonly List<ObstacleOption> _candidates = new List<ObstacleOption>();
    private readonly List<ObstacleOption> _tapCandidates = new List<ObstacleOption>();
    private readonly List<ObstacleOption> _companionCandidates = new List<ObstacleOption>();

    private RunnerPlayer _player;
    private JumpArc _fullArc;
    private JumpArc _tapArc;
    private bool _tapUseful;
    private float _distanceUntilNext;
    private QueuedSpawn _queued;
    private ObstacleOption _queuedGround;
    private LowFlyerPlan _queuedLowFlyer;
    private bool _lastWasAir;
    private float _tolerance = 1f;
    private float _swayFactor = 1f;

    private float Reaction => reactionTime * _tolerance;
    private float LandingMargin => comboLandingMargin * _tolerance;
    private float ComboReaction => comboReactionTime * _tolerance;
    private float Safety => Mathf.Lerp(1f, clearSafety, _tolerance);
    private float Clearance => Mathf.Lerp(1f, clearanceFactor, _tolerance);

    public void Setup(RunnerPlayer player)
    {
        Clear();

        _player = player;
        _fullArc = player.FullArc;
        _tapArc = player.GetArc(tapHoldTime);
        _tapUseful = _tapArc.AirTime < _fullArc.AirTime * 0.9f;

        _options.Clear();
        foreach (var prefab in obstaclePrefabs)
        {
            if (prefab == null) continue;
            _options.Add(new ObstacleOption { Prefab = prefab, Height = prefab.Height, Width = prefab.Width });
        }

        BuildAirOptions();

        _distanceUntilNext = -1f;
        _queued = QueuedSpawn.None;
        _lastWasAir = false;
        _tolerance = 1f;
        _swayFactor = swaySpacingEasy;
    }

    private void BuildAirOptions()
    {
        _airOptions.Clear();
        _highOptions.Clear();

        float baseY = _player.StandingHitboxCenter + _fullArc.Height * airHeightFactor;

        foreach (var prefab in airPrefabs)
        {
            if (prefab == null) continue;

            float halfHeight = prefab.HalfHeight;
            float lowestSafeCenter = _player.StandingHitboxTop + airGroundClearance + halfHeight;
            float room = baseY - lowestSafeCenter;

            var obstacle = prefab.GetComponent<RunnerObstacle>();
            var option = new AirOption
            {
                Prefab = prefab,
                Width = obstacle != null ? obstacle.Width : 1f,
                HalfHeight = halfHeight,
                BaseY = baseY,
                Amplitude = Mathf.Min(prefab.BobAmplitude, Mathf.Max(0f, room)),
                Sway = prefab.SwayAmplitude,
                CanFlyHigh = room >= 0f
            };

            _airOptions.Add(option);

            if (option.CanFlyHigh)
                _highOptions.Add(option);
            else
                Debug.LogWarning($"Flyer '{prefab.name}' would hit a player standing on the ground if it flew high - it will only appear as a low flyer", this);
        }
    }

    public void Tick(float speed, float deltaTime, float difficulty)
    {
        _tolerance = Mathf.Lerp(1f, toleranceAtMaxDifficulty, difficulty);
        _swayFactor = Mathf.Lerp(swaySpacingEasy, swaySpacingHard, difficulty);

        float distance = speed * deltaTime;
        float despawnX = SpawnPosition.x - despawnDistance;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var entry = _active[i];
            if (entry.Transform == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            if (entry.Flyer != null)
                entry.Flyer.Tick(deltaTime, distance);
            else
                entry.Transform.position += Vector3.left * distance;

            if (entry.Transform.position.x < despawnX)
            {
                Destroy(entry.Transform.gameObject);
                _active.RemoveAt(i);
            }
        }

        if (_player == null || _options.Count == 0) return;

        if (_distanceUntilNext < 0f)
            _distanceUntilNext = speed * firstObstacleDelay;

        _distanceUntilNext -= distance;
        if (_distanceUntilNext > 0f) return;

        var queued = _queued;
        _queued = QueuedSpawn.None;

        switch (queued)
        {
            case QueuedSpawn.Air when CanSpawnHigh:
                SpawnHighAndPlanNext(speed, difficulty);
                return;

            case QueuedSpawn.Ground:
                SpawnGroundAndPlanNext(_queuedGround, false, speed, difficulty);
                return;

            case QueuedSpawn.LowFlyer:
                SpawnLowFlyerAndPlanNext(_queuedLowFlyer, speed, difficulty);
                return;
        }

        if (CanSpawnHigh && Random.value < Ramp(difficulty, airStartDifficulty, maxAirChance))
        {
            if (Random.value < Ramp(difficulty, needleStartDifficulty, maxNeedleChance) && TrySpawnNeedle(speed, difficulty))
                return;

            SpawnHighAndPlanNext(speed, difficulty);
            return;
        }

        if (_airOptions.Count > 0 && Random.value < Ramp(difficulty, lowFlyerStartDifficulty, maxLowFlyerChance))
        {
            var flyer = _airOptions[Random.Range(0, _airOptions.Count)];
            if (TryPlanLowFlyer(flyer, speed, difficulty, out var plan))
            {
                SpawnLowFlyerAndPlanNext(plan, speed, difficulty);
                return;
            }

            if (CanSpawnHigh && flyer.CanFlyHigh)
            {
                SpawnHighAndPlanNext(speed, difficulty, flyer);
                return;
            }
        }

        bool wantTapCombo = _tapUseful && _highOptions.Count > 0 && RollCombo(difficulty, maxTapComboChance);
        bool wantTap = !wantTapCombo && _tapUseful && Random.value < Ramp(difficulty, tapStartDifficulty, maxTapChance);

        var choice = Choose(speed, difficulty, wantTap || wantTapCombo, out bool tapClearable);

        if (wantTapCombo && tapClearable)
        {
            SpawnGroundAndPlanNext(choice, false, speed, difficulty, true);
            return;
        }

        SpawnGroundAndPlanNext(choice, wantTap && tapClearable, speed, difficulty);
    }

    private bool CanSpawnHigh => !_lastWasAir && _highOptions.Count > 0;

    private static float Ramp(float difficulty, float start, float max)
    {
        if (difficulty < start) return 0f;
        float t = start >= 1f ? 1f : (difficulty - start) / (1f - start);
        return Mathf.Lerp(0f, max, t);
    }

    private bool RollCombo(float difficulty, float maxChance) =>
        Random.value < Ramp(difficulty, comboStartDifficulty, maxChance);

    private float AllowedGroundHeight(float difficulty) =>
        _fullArc.Height * Clearance * Mathf.Lerp(easyHeightFraction, 1f, difficulty);

    private void SpawnGroundAndPlanNext(ObstacleOption choice, bool usedTap, float speed, float difficulty, bool tapCombo = false)
    {
        Spawn(choice.Prefab);
        PlanAfterGround(choice.Height, choice.Width, usedTap, tapCombo, speed, difficulty);
    }

    private void SpawnLowFlyerAndPlanNext(LowFlyerPlan plan, float speed, float difficulty)
    {
        SpawnLowFlyer(plan);
        PlanAfterGround(plan.Height, plan.Width, false, false, speed, difficulty);
    }

    private void PlanAfterGround(float height, float width, bool usedTap, bool tapCombo, float speed, float difficulty)
    {
        if (tapCombo)
        {
            _queued = QueuedSpawn.Air;
            _distanceUntilNext = GroundToAirGap(speed, height, _tapArc);
            return;
        }

        if (usedTap)
        {
            _distanceUntilNext = TapGap(speed, width);
            return;
        }

        if (_highOptions.Count > 0 && RollCombo(difficulty, maxComboChance))
        {
            _queued = QueuedSpawn.Air;
            _distanceUntilNext = GroundToAirGap(speed, height, _fullArc);
            return;
        }

        _distanceUntilNext = NormalGap(speed, difficulty, width);
    }

    private void SpawnHighAndPlanNext(float speed, float difficulty, AirOption? forced = null)
    {
        var air = forced ?? _highOptions[Random.Range(0, _highOptions.Count)];
        SpawnHigh(air);

        float airWidth = air.SpacingWidth(_swayFactor);

        if (RollCombo(difficulty, maxBirdFollowUpChance))
        {
            if (_airOptions.Count > 0 && Random.value < Ramp(difficulty, lowFlyerStartDifficulty, maxLowFlyerChance))
            {
                var flyer = _airOptions[Random.Range(0, _airOptions.Count)];
                if (TryPlanLowFlyer(flyer, speed, difficulty, out var plan))
                {
                    _queued = QueuedSpawn.LowFlyer;
                    _queuedLowFlyer = plan;
                    _distanceUntilNext = AirToGroundGap(speed, airWidth, plan.Height);
                    return;
                }
            }

            var next = Choose(speed, difficulty, false, out _);
            _queued = QueuedSpawn.Ground;
            _queuedGround = next;
            _distanceUntilNext = AirToGroundGap(speed, airWidth, next.Height);
            return;
        }

        _distanceUntilNext = NormalGap(speed, difficulty, airWidth);
    }

    private bool TrySpawnNeedle(float speed, float difficulty)
    {
        var air = _highOptions[Random.Range(0, _highOptions.Count)];

        float minHop = _player.MinimumArc.Height + needleWindow;
        float maxHop = _fullArc.Height * needleMaxHopFraction;
        if (maxHop < minHop) return false;

        float hop = Random.Range(minHop, maxHop);
        var hopArc = _player.GetArcForHeight(hop);
        float maxGroundHeight = hopArc.Height - needleWindow;

        _companionCandidates.Clear();
        foreach (var option in _options)
        {
            if (option.Height <= maxGroundHeight && _player.CanClear(hopArc, option.Height, option.Width, speed, Safety))
                _companionCandidates.Add(option);
        }

        if (_companionCandidates.Count == 0) return false;

        var ground = _companionCandidates[Random.Range(0, _companionCandidates.Count)];

        float bob = Mathf.Min(air.Prefab.BobAmplitude, air.Amplitude);
        float birdBottom = _player.StandingHitboxTop + hopArc.Height + needleHeadroom;
        float birdCenter = birdBottom + air.HalfHeight + bob;

        Spawn(ground.Prefab);
        SpawnHighAt(air, birdCenter, bob);

        _distanceUntilNext = NormalGap(speed, difficulty, Mathf.Max(air.SpacingWidth(_swayFactor), ground.Width));
        return true;
    }

    private bool TryPlanLowFlyer(AirOption flyer, float speed, float difficulty, out LowFlyerPlan plan)
    {
        float maxTop = AllowedGroundHeight(difficulty);

        if (Random.value < lowFlyerCompanionChance && TryPickCompanion(flyer, maxTop, out var companion))
        {
            float companionBottom = companion.Height + lowFlyerCompanionGap;
            if (TryBuildLowPlan(flyer, speed, maxTop, companionBottom, companion, true, out plan))
                return true;
        }

        return TryBuildLowPlan(flyer, speed, maxTop, lowFlyerMinAltitude, default, false, out plan);
    }

    private bool TryPickCompanion(AirOption flyer, float maxTop, out ObstacleOption companion)
    {
        _companionCandidates.Clear();

        foreach (var option in _options)
        {
            float lowestBottom = Mathf.Max(lowFlyerMinAltitude, option.Height + lowFlyerCompanionGap);
            if (lowestBottom + flyer.HalfHeight * 2f <= maxTop)
                _companionCandidates.Add(option);
        }

        if (_companionCandidates.Count == 0)
        {
            companion = default;
            return false;
        }

        companion = _companionCandidates[Random.Range(0, _companionCandidates.Count)];
        return true;
    }

    private bool TryBuildLowPlan(AirOption flyer, float speed, float maxTop, float minBottom,
        ObstacleOption companion, bool hasCompanion, out LowFlyerPlan plan)
    {
        plan = default;

        minBottom = Mathf.Max(minBottom, lowFlyerMinAltitude);
        float room = maxTop - minBottom - flyer.HalfHeight * 2f;
        if (room < 0f) return false;

        float bob = Mathf.Min(flyer.Prefab.BobAmplitude, room * 0.5f);
        float lowest = minBottom + flyer.HalfHeight + bob;
        float highest = maxTop - flyer.HalfHeight - bob;
        float center = Random.Range(lowest, Mathf.Max(lowest, highest));
        float top = center + flyer.HalfHeight + bob;

        float flyerClearWidth = flyer.Width + flyer.Sway * 2f * Mathf.Max(0f, _swayFactor);
        float clearWidth = hasCompanion ? Mathf.Max(flyerClearWidth, companion.Width) : flyerClearWidth;
        if (!_player.CanClear(_fullArc, top, clearWidth, speed, Safety)) return false;

        float spacingWidth = flyer.SpacingWidth(_swayFactor);

        plan = new LowFlyerPlan
        {
            Prefab = flyer.Prefab,
            CenterAboveGround = center,
            Bob = bob,
            Height = top,
            Width = hasCompanion ? Mathf.Max(spacingWidth, companion.Width) : spacingWidth,
            SwayOffset = flyer.Sway * _swayFactor,
            HasCompanion = hasCompanion,
            Companion = companion
        };
        return true;
    }

    private ObstacleOption Choose(float speed, float difficulty, bool wantTap, out bool usedTap)
    {
        float allowedHeight = AllowedGroundHeight(difficulty);
        float tapAllowedHeight = _tapArc.Height * Clearance;

        _candidates.Clear();
        _tapCandidates.Clear();

        foreach (var option in _options)
        {
            if (option.Height > allowedHeight || !_player.CanClear(_fullArc, option.Height, option.Width, speed, Safety))
                continue;

            _candidates.Add(option);

            if (wantTap && option.Height <= tapAllowedHeight && _player.CanClear(_tapArc, option.Height, option.Width, speed, Safety))
                _tapCandidates.Add(option);
        }

        if (wantTap && _tapCandidates.Count > 0)
        {
            usedTap = true;
            return _tapCandidates[Random.Range(0, _tapCandidates.Count)];
        }

        usedTap = false;
        return _candidates.Count > 0 ? _candidates[Random.Range(0, _candidates.Count)] : FindEasiest(speed);
    }

    private ObstacleOption FindEasiest(float speed)
    {
        ObstacleOption best = _options[0];
        bool bestClearable = _player.CanClear(_fullArc, best.Height, best.Width, speed, Safety);

        for (int i = 1; i < _options.Count; i++)
        {
            var option = _options[i];
            bool clearable = _player.CanClear(_fullArc, option.Height, option.Width, speed, Safety);

            if ((clearable && !bestClearable) || (clearable == bestClearable && option.Height < best.Height))
            {
                best = option;
                bestClearable = clearable;
            }
        }

        if (!bestClearable)
            Debug.LogWarning($"No obstacle can be cleared with a {_fullArc.Height:0.00} jump at speed {speed:0.0} - spawning the easiest one", this);

        return best;
    }

    private float NormalGap(float speed, float difficulty, float obstacleWidth)
    {
        var range = Vector2.Lerp(easyGapSeconds, hardGapSeconds, difficulty);
        float seconds = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

        float fair = FairDistance(_fullArc, speed, obstacleWidth);
        float spaced = Mathf.Max(seconds * speed, fair);
        return Mathf.Lerp(fair, spaced, _tolerance);
    }

    private float TapGap(float speed, float obstacleWidth)
    {
        return FairDistance(_tapArc, speed, obstacleWidth) * Random.Range(1f, 1f + 0.15f * _tolerance);
    }

    private float FairDistance(JumpArc arc, float speed, float obstacleWidth)
    {
        return speed * (arc.AirTime + Reaction) + obstacleWidth + _player.HitboxWidth;
    }

    private float GroundToAirGap(float speed, float groundHeight, JumpArc arc)
    {
        float landingDelay = arc.AirTime - arc.TimeToReach(groundHeight) + LandingMargin;
        return Mathf.Max(speed * landingDelay, _player.HitboxWidth);
    }

    private float AirToGroundGap(float speed, float airWidth, float groundHeight)
    {
        return airWidth + _player.HitboxWidth + speed * (ComboReaction + _fullArc.TimeToReach(groundHeight));
    }

    public void Clear()
    {
        foreach (var entry in _active)
        {
            if (entry.Transform != null)
                Destroy(entry.Transform.gameObject);
        }
        _active.Clear();
    }

    private void Spawn(RunnerObstacle prefab)
    {
        var instance = Instantiate(prefab, SpawnPosition, Quaternion.identity, transform);
        _active.Add(new ActiveObstacle { Transform = instance.transform });
        _lastWasAir = false;
    }

    private void SpawnHigh(AirOption option) => SpawnHighAt(option, option.BaseY, option.Amplitude);

    private void SpawnHighAt(AirOption option, float centerY, float maxBob)
    {
        var position = SpawnPosition;
        position.x += option.Sway * _swayFactor;
        position.y = centerY;

        var instance = Instantiate(option.Prefab, position, Quaternion.identity, transform);
        instance.Launch(position.x, centerY, maxBob);
        _active.Add(new ActiveObstacle { Transform = instance.transform, Flyer = instance });
        _lastWasAir = true;
    }

    private void SpawnLowFlyer(LowFlyerPlan plan)
    {
        if (plan.HasCompanion && plan.Companion.Prefab != null)
            Spawn(plan.Companion.Prefab);

        var position = SpawnPosition;
        position.x += plan.SwayOffset;
        position.y += plan.CenterAboveGround;

        var instance = Instantiate(plan.Prefab, position, Quaternion.identity, transform);
        instance.Launch(position.x, position.y, plan.Bob);
        _active.Add(new ActiveObstacle { Transform = instance.transform, Flyer = instance });
        _lastWasAir = false;
    }

    private Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    private void OnDrawGizmosSelected()
    {
        var spawn = SpawnPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(spawn + Vector3.down, spawn + Vector3.up * 4f);
        Gizmos.color = Color.red;
        var despawn = spawn + Vector3.left * despawnDistance;
        Gizmos.DrawLine(despawn + Vector3.down, despawn + Vector3.up * 4f);
    }
}