using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerPlayer : MonoBehaviour
{
    [Header("Jump Height (Strength)")]
    [SerializeField] private float minJumpHeight = 2f;
    [SerializeField] private float maxJumpHeight = 4f;
    [SerializeField] private float jumpHeightPerStrength = 0.15f;
    [SerializeField] [Min(0)] private int strengthOverride;

    [Header("Jump Feel")]
    [SerializeField] private float timeToApex = 0.3f;
    [SerializeField] private float timeToFall = 0.22f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Collision")]
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] [Range(0.5f, 1f)] private float hitboxScale = 0.85f;
    [SerializeField] private bool debugCollisions;

    [Header("Input")]
    [SerializeField] private string jumpActionPath = "EndlessRunner/Jump";

    [Header("Visuals")]
    [SerializeField] private PlayerAppearance appearance;

    public event Action Crashed;

    public float JumpHeight { get; private set; }
    public float AirTime { get; private set; }
    public float StandingHitboxCenter { get; private set; }
    public float StandingHitboxTop { get; private set; }
    public bool IsGrounded { get; private set; } = true;

    private readonly Collider2D[] _hits = new Collider2D[4];
    private readonly Collider2D[] _debugHits = new Collider2D[16];
    private string _lastDebugSignature;
    private InputAction _jumpAction;
    private float _groundY;
    private float _velocityY;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private bool _controlsEnabled;

    private float RiseGravity => 2f * minJumpHeight / (timeToApex * timeToApex);
    private float FallGravity => 2f * minJumpHeight / (timeToFall * timeToFall);

    private void Awake()
    {
        _groundY = transform.position.y;
        if (hitbox == null)
            hitbox = GetComponent<Collider2D>();
        if (appearance == null)
            appearance = GetComponentInChildren<PlayerAppearance>();
    }

    private void Start()
    {
        if (InputManager.Instance != null)
            _jumpAction = InputManager.Instance.FindAction(jumpActionPath, this);

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && appearance != null)
            appearance.Apply(save.character);
    }

    private void OnValidate()
    {
        minJumpHeight = Mathf.Max(0.01f, minJumpHeight);
        maxJumpHeight = Mathf.Max(minJumpHeight, maxJumpHeight);
        timeToApex = Mathf.Max(0.01f, timeToApex);
        timeToFall = Mathf.Max(0.01f, timeToFall);
        lowJumpMultiplier = Mathf.Max(1f, lowJumpMultiplier);
    }

    public void ResetRunner()
    {
        RecalculateJump();

        var position = transform.position;
        position.y = _groundY;
        transform.position = position;

        _velocityY = 0f;
        IsGrounded = true;
        _lastJumpPressedTime = float.NegativeInfinity;
        _lastDebugSignature = null;

        if (hitbox != null)
        {
            Physics2D.SyncTransforms();
            var bounds = hitbox.bounds;
            StandingHitboxCenter = bounds.center.y;
            StandingHitboxTop = bounds.center.y + bounds.extents.y * hitboxScale;
        }
        else
        {
            StandingHitboxCenter = _groundY + 0.5f;
            StandingHitboxTop = _groundY + 1f;
        }

        if (debugCollisions)
            LogSetup();
    }

    public float HitboxWidth => hitbox != null ? hitbox.bounds.size.x * hitboxScale : 0.5f;

    public JumpArc FullArc => GetArc(float.MaxValue);

    public JumpArc GetArc(float holdTime)
    {
        float rise = RiseGravity;
        float release = RiseGravity * lowJumpMultiplier;
        float fall = FallGravity;
        float launch = Mathf.Sqrt(2f * rise * JumpHeight);
        float fullApexTime = launch / rise;

        var arc = new JumpArc
        {
            LaunchVelocity = launch,
            RiseGravity = rise,
            ReleaseGravity = release,
            FallGravity = fall
        };

        if (holdTime >= fullApexTime)
        {
            arc.ReleaseTime = fullApexTime;
            arc.ReleaseHeight = JumpHeight;
            arc.ReleaseVelocity = 0f;
            arc.ApexTime = fullApexTime;
            arc.Height = JumpHeight;
        }
        else
        {
            float hold = Mathf.Max(0f, holdTime);
            float releaseVelocity = launch - rise * hold;
            float releaseHeight = launch * hold - 0.5f * rise * hold * hold;

            arc.ReleaseTime = hold;
            arc.ReleaseHeight = releaseHeight;
            arc.ReleaseVelocity = releaseVelocity;
            arc.ApexTime = hold + releaseVelocity / release;
            arc.Height = releaseHeight + releaseVelocity * releaseVelocity / (2f * release);
        }

        arc.AirTime = arc.ApexTime + Mathf.Sqrt(2f * arc.Height / fall);
        return arc;
    }

    public JumpArc MinimumArc => GetArc(0f);

    public JumpArc GetArcForHeight(float targetHeight)
    {
        var full = FullArc;
        if (targetHeight >= full.Height) return full;

        float low = 0f;
        float high = full.ApexTime;
        for (int i = 0; i < 16; i++)
        {
            float mid = (low + high) * 0.5f;
            if (GetArc(mid).Height > targetHeight)
                high = mid;
            else
                low = mid;
        }

        return GetArc(low);
    }

    public bool CanClear(JumpArc arc, float obstacleHeight, float obstacleWidth, float speed, float safety = 0.9f)
    {
        if (speed <= 0f) return false;
        float timeNeeded = (obstacleWidth + HitboxWidth) / speed;
        return arc.TimeAbove(obstacleHeight) * safety >= timeNeeded;
    }

    public void SetControlsEnabled(bool enabled)
    {
        _controlsEnabled = enabled;

        if (debugCollisions)
            Debug.Log($"[Runner] Controls {(enabled ? "ENABLED" : "disabled")} - crash checks {(enabled ? "running every frame" : "paused")}", this);
    }

    private void RecalculateJump()
    {
        int strength = strengthOverride > 0 ? strengthOverride : GetSavedStrength();
        float height = minJumpHeight + Mathf.Max(0, strength - PlayerStats.StartingValue) * jumpHeightPerStrength;
        JumpHeight = Mathf.Clamp(height, minJumpHeight, maxJumpHeight);

        AirTime = FullArc.AirTime;
    }

    private static int GetSavedStrength()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && save.stats != null ? save.stats.strength : PlayerStats.StartingValue;
    }

    private void Update()
    {
        if (!_controlsEnabled) return;

        if (JumpPressedThisFrame())
            _lastJumpPressedTime = Time.time;

        float dt = Time.deltaTime;

        if (IsGrounded && Time.time - _lastJumpPressedTime <= jumpBufferTime)
        {
            _velocityY = Mathf.Sqrt(2f * RiseGravity * JumpHeight);
            IsGrounded = false;
            _lastJumpPressedTime = float.NegativeInfinity;
        }

        if (!IsGrounded)
        {
            float gravity = _velocityY < 0f ? FallGravity : RiseGravity * (JumpHeld() ? 1f : lowJumpMultiplier);
            _velocityY -= gravity * dt;

            var position = transform.position;
            position.y += _velocityY * dt;

            if (position.y <= _groundY)
            {
                position.y = _groundY;
                _velocityY = 0f;
                IsGrounded = true;
            }

            transform.position = position;
        }

        CheckCrash();
    }

    private void CheckCrash()
    {
        if (hitbox == null) return;

        Physics2D.SyncTransforms();
        var bounds = hitbox.bounds;

        if (debugCollisions)
            LogOverlaps(bounds);

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayer, useTriggers = true };
        int count = Physics2D.OverlapBox(bounds.center, bounds.size * hitboxScale, 0f, filter, _hits);

        for (int i = 0; i < count; i++)
        {
            if (_hits[i] != hitbox)
            {
                _controlsEnabled = false;
                Crashed?.Invoke();
                return;
            }
        }
    }

    private void LogSetup()
    {
        string hitboxInfo = hitbox == null
            ? "NONE"
            : $"{hitbox.GetType().Name} on '{hitbox.name}' (enabled {hitbox.enabled}, bounds {hitbox.bounds.size})";

        Debug.Log($"[Runner] Setup - hitbox: {hitboxInfo}, checking scale {hitboxScale}, Obstacle Layer = {DescribeMask(obstacleLayer)}", this);
    }

    private void LogOverlaps(Bounds bounds)
    {
        var anyLayer = new ContactFilter2D { useLayerMask = false, useTriggers = true };
        int count = Physics2D.OverlapBox(bounds.center, bounds.size * hitboxScale, 0f, anyLayer, _debugHits);

        var entries = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var col = _debugHits[i];
            if (col == hitbox) continue;

            bool matches = (obstacleLayer.value & (1 << col.gameObject.layer)) != 0;
            entries.Add($"'{col.name}' layer={LayerMask.LayerToName(col.gameObject.layer)} trigger={col.isTrigger} matchesObstacleLayer={matches}");
        }

        string signature = string.Join(" | ", entries);
        if (signature == _lastDebugSignature) return;
        _lastDebugSignature = signature;

        Debug.Log(entries.Count == 0
            ? "[Runner] Overlapping: nothing"
            : $"[Runner] Overlapping {entries.Count}: {signature}", this);
    }

    private static string DescribeMask(LayerMask mask)
    {
        if (mask.value == 0) return "Nothing";

        var names = new StringBuilder();
        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask.value & (1 << layer)) == 0) continue;
            if (names.Length > 0) names.Append(", ");
            string layerName = LayerMask.LayerToName(layer);
            names.Append(string.IsNullOrEmpty(layerName) ? $"#{layer}" : layerName);
        }
        return names.ToString();
    }

    private bool JumpPressedThisFrame()
    {
        if (_jumpAction != null) return _jumpAction.WasPressedThisFrame();

        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    private bool JumpHeld()
    {
        if (_jumpAction != null) return _jumpAction.IsPressed();

        return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
               (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
    }

    private void OnDrawGizmosSelected()
    {
        var col = hitbox != null ? hitbox : GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size * hitboxScale);
    }
}

public struct JumpArc
{
    public float LaunchVelocity;
    public float RiseGravity;
    public float ReleaseGravity;
    public float FallGravity;
    public float ReleaseTime;
    public float ReleaseHeight;
    public float ReleaseVelocity;
    public float ApexTime;
    public float Height;
    public float AirTime;

    public float TimeToReach(float height)
    {
        if (height <= 0f) return 0f;
        if (height >= Height) return ApexTime;

        if (height <= ReleaseHeight)
        {
            float disc = Mathf.Max(0f, LaunchVelocity * LaunchVelocity - 2f * RiseGravity * height);
            return (LaunchVelocity - Mathf.Sqrt(disc)) / RiseGravity;
        }

        float releaseDisc = Mathf.Max(0f, ReleaseVelocity * ReleaseVelocity - 2f * ReleaseGravity * (height - ReleaseHeight));
        return ReleaseTime + (ReleaseVelocity - Mathf.Sqrt(releaseDisc)) / ReleaseGravity;
    }

    public float TimeAbove(float height)
    {
        if (height <= 0f) return AirTime;
        if (height >= Height) return 0f;

        float timeDown = ApexTime + Mathf.Sqrt(2f * (Height - height) / FallGravity);
        return Mathf.Max(0f, timeDown - TimeToReach(height));
    }
}