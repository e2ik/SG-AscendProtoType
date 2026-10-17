using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class WorldMovementController : MovementController, IStatCapProvider
{
    [Header("Move Speed (Agility)")]
    [FormerlySerializedAs("baseMoveSpeed")]
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float minMoveSpeed = 5f;
    [SerializeField] private float maxMoveSpeed = 10f;
    [SerializeField] private float speedPerAgility = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float horizontalDeadzone = 0.2f;

    [Header("Acceleration")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 8f;
    [SerializeField] private float velocityPower = 1.2f;
    [SerializeField] private float friction = 0.2f;

    [Header("Jump (Strength)")]
    [SerializeField] private bool canJump = true;
    [FormerlySerializedAs("baseJumpHeight")]
    [FormerlySerializedAs("jumpHeight")]
    [SerializeField] private float minJumpHeight = 2.5f;
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float jumpHeightPerStrength = 0.15f;

    [Header("Jump Feel")]
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Jump Windup")]
    [SerializeField] [Min(0f)] private float jumpDelay = 0.15f;
    [SerializeField] [Min(0f)] private float windupBrake = 25f;
    [SerializeField] private string jumpSound = "RunnerJump";
    [SerializeField] private string jumpDustEffect = "JumpDust";

    [Header("Air Control")]
    [SerializeField] [Range(0f, 1f)] private float airSpeedMultiplier = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float airAccelerationMultiplier = 0.6f;

    [Header("Landing")]
    [SerializeField] [Range(0f, 1f)] private float landSpeedMultiplier = 0.4f;
    [SerializeField] [Min(0f)] private float landSlowDuration = 0.2f;
    [SerializeField] [Min(0f)] private float minFallSpeedForLandSlow = 2f;

    [Header("Gravity")]
    [SerializeField] private float normGravity = 3f;
    [SerializeField] private float jumpGravity = 2.5f;
    [SerializeField] private float fallGravity = 4.5f;

    [Header("Stats")]
    [SerializeField] private PlayerStatsController stats;

    [Header("One-Way Platforms")]
    [SerializeField] private bool allowDropThrough = true;
    [SerializeField] [Range(0.1f, 1f)] private float dropDownThreshold = 0.5f;
    [SerializeField] private float dropDuration = 0.25f;
    [SerializeField] private float dropKickVelocity = 3f;
    [SerializeField] [Min(0f)] private float oneWayTolerance = 0.05f;

    [Header("Ground Check")]
    [SerializeField] [Range(0.1f, 1f)] private float groundCheckWidth = 0.95f;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] [Range(0f, 1f)] private float minGroundNormalY = 0.7f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Facing")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool spritesFaceRight = true;

    [Header("Input")]
    [SerializeField] private string moveActionPath = "Player/Move";
    [SerializeField] private string jumpActionPath = "Player/Jump";

    public event System.Action JumpStarted;

    public override bool IsGrounded => _isGrounded;
    public bool IsWindingUp => _windingUp;
    public bool IsFacingRight { get; private set; } = true;
    public float MoveInput => _moveInput;
    public float MoveInputY => _moveInputY;

    public float CurrentMoveSpeed => Evaluate(GetStat(StatType.Agility), minMoveSpeed, maxMoveSpeed, speedPerAgility);
    public float CurrentJumpHeight => Evaluate(GetStat(StatType.Strength), minJumpHeight, maxJumpHeight, jumpHeightPerStrength);

    private const float GroundCastStartOffset = 0.05f;
    private const float GroundCastThickness = 0.02f;
    private const float GravityVelocityThreshold = 0.1f;

    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
    private Rigidbody2D _body;
    private Collider2D _collider;
    private InputAction _moveAction;
    private InputAction _jumpAction;

    private bool _isGrounded;
    private float _moveInput;
    private float _moveInputY;
    private bool _dropRequested;
    private readonly List<Collider2D> _standingOneWay = new List<Collider2D>();
    private readonly Dictionary<Collider2D, float> _droppingThrough = new Dictionary<Collider2D, float>();
    private readonly List<Collider2D> _restoreBuffer = new List<Collider2D>();
    private bool _wasMoving;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private bool _windingUp;
    private float _windupEndTime;
    private bool _wasGroundedFixed = true;
    private float _peakFallSpeed;
    private float _landTime = float.NegativeInfinity;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        _body.gravityScale = normGravity;
        _body.freezeRotation = true;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (_collider != null && _collider.sharedMaterial == null)
            _collider.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        if (stats == null)
            stats = GetComponent<PlayerStatsController>();

        if (visualRoot == null)
            Debug.LogWarning($"WorldMovementController on '{name}' has no Visual Root, so the character won't turn to face movement", this);
    }

    private void OnValidate()
    {
        minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        maxMoveSpeed = Mathf.Max(minMoveSpeed, maxMoveSpeed);
        speedPerAgility = Mathf.Max(0f, speedPerAgility);

        minJumpHeight = Mathf.Max(0.01f, minJumpHeight);
        maxJumpHeight = Mathf.Max(minJumpHeight, maxJumpHeight);
        jumpHeightPerStrength = Mathf.Max(0f, jumpHeightPerStrength);

        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        velocityPower = Mathf.Max(0.01f, velocityPower);
        friction = Mathf.Max(0f, friction);

        normGravity = Mathf.Max(0f, normGravity);
        jumpGravity = Mathf.Max(0.01f, jumpGravity);
        fallGravity = Mathf.Max(0f, fallGravity);
    }

    public int GetMaxStat(StatType type) => type switch
    {
        StatType.Agility => MaxStatFor(minMoveSpeed, maxMoveSpeed, speedPerAgility),
        StatType.Strength => MaxStatFor(minJumpHeight, maxJumpHeight, jumpHeightPerStrength),
        _ => int.MaxValue
    };

    public float MoveSpeedAt(int agility) => Evaluate(agility, minMoveSpeed, maxMoveSpeed, speedPerAgility);
    public float JumpHeightAt(int strength) => Evaluate(strength, minJumpHeight, maxJumpHeight, jumpHeightPerStrength);

    private static int MaxStatFor(float min, float max, float perPoint)
    {
        if (perPoint <= 0f) return int.MaxValue;
        return PlayerStats.StartingValue + Mathf.FloorToInt((max - min) / perPoint + 0.0001f);
    }

    private static float Evaluate(int stat, float min, float max, float perPoint)
    {
        float value = min + Mathf.Max(0, stat - PlayerStats.StartingValue) * perPoint;
        return Mathf.Clamp(value, min, max);
    }

    private int GetStat(StatType type) => stats != null ? stats.Get(type) : PlayerStats.StartingValue;

    private float JumpVelocity
    {
        get
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * jumpGravity;
            return Mathf.Sqrt(2f * gravity * CurrentJumpHeight);
        }
    }

    private void Start()
    {
        _moveAction = InputManager.Instance.FindAction(moveActionPath, this);
        if (canJump)
            _jumpAction = InputManager.Instance.FindAction(jumpActionPath, this);

        ApplyFacing(IsFacingRight);
    }

    private void Update()
    {
        var move = InputEnabled && _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        _moveInput = Mathf.Abs(move.x) >= horizontalDeadzone ? Mathf.Sign(move.x) * Mathf.Clamp01(move.magnitude) : 0f;
        _moveInputY = move.y;

        bool moving = !Mathf.Approximately(_moveInput, 0f);
        if (moving && !_wasMoving)
            TutorialEvents.Raise(TutorialEvents.PlayerMoved);
        _wasMoving = moving;

        if (InputEnabled && _jumpAction != null)
        {
            if (_jumpAction.WasPressedThisFrame())
            {
                if (allowDropThrough && _moveInputY <= -dropDownThreshold && _standingOneWay.Count > 0)
                    _dropRequested = true;
                else
                    _lastJumpPressedTime = Time.time;
            }
        }

        UpdateFacing();
    }

    private void FixedUpdate()
    {
        float now = Time.time;

        RestoreFinishedDrops(now);
        _isGrounded = CheckGrounded();
        TrackLanding(now);

        bool dropping = _dropRequested && StartDrop(now);
        _dropRequested = false;

        if (IsGrounded)
            _lastGroundedTime = now;

        HandleMovement(now);

        var velocity = _body.linearVelocity;

        if (_windingUp && windupBrake > 0f)
            velocity.x = Mathf.Lerp(velocity.x, 0f, 1f - Mathf.Exp(-windupBrake * Time.fixedDeltaTime));

        if (dropping)
        {
            velocity.y = -dropKickVelocity;
            TutorialEvents.Raise(TutorialEvents.PlayerDropped);
        }

        bool jumpBuffered = now - _lastJumpPressedTime <= jumpBufferTime;
        bool withinCoyote = now - _lastGroundedTime <= coyoteTime;

        if (canJump && !_windingUp && jumpBuffered && withinCoyote)
        {
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;

            JumpStarted?.Invoke();

            if (jumpDelay <= 0f)
            {
                Launch(ref velocity);
            }
            else
            {
                _windingUp = true;
                _windupEndTime = now + jumpDelay;
            }
        }

        if (_windingUp && now >= _windupEndTime)
            Launch(ref velocity);

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        _body.linearVelocity = velocity;

        UpdateGravity(velocity.y);
    }

    private void Launch(ref Vector2 velocity)
    {
        _windingUp = false;
        velocity.y = JumpVelocity;
        TutorialEvents.Raise(TutorialEvents.PlayerJumped);

        if (!string.IsNullOrEmpty(jumpSound) && ASpawner.Instance != null)
            ASpawner.Play(jumpSound);

        if (!string.IsNullOrEmpty(jumpDustEffect) && PSpawner.Instance != null && _collider != null)
        {
            var bounds = _collider.bounds;
            PSpawner.Spawn(jumpDustEffect, new Vector3(bounds.center.x, bounds.min.y, transform.position.z));
        }
    }

    private void TrackLanding(float now)
    {
        if (!_isGrounded)
        {
            _peakFallSpeed = Mathf.Max(_peakFallSpeed, -_body.linearVelocity.y);
        }
        else if (!_wasGroundedFixed)
        {
            if (_peakFallSpeed >= minFallSpeedForLandSlow)
                _landTime = now;
            _peakFallSpeed = 0f;
        }

        _wasGroundedFixed = _isGrounded;
    }

    private float SpeedMultiplier(float now)
    {
        if (_windingUp) return 0f;
        if (!_isGrounded) return airSpeedMultiplier;

        if (landSlowDuration > 0f)
        {
            float t = (now - _landTime) / landSlowDuration;
            if (t < 1f)
                return Mathf.Lerp(landSpeedMultiplier, 1f, t * t);
        }

        return 1f;
    }

    private void HandleMovement(float now)
    {
        bool hasInput = Mathf.Abs(_moveInput) > 0f;
        float targetSpeed = _moveInput * CurrentMoveSpeed * SpeedMultiplier(now);
        float speedDif = targetSpeed - _body.linearVelocity.x;
        float accelRate = (hasInput ? acceleration : deceleration) * (_isGrounded ? 1f : airAccelerationMultiplier);
        float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velocityPower) * Mathf.Sign(speedDif);

        _body.AddForce(movement * Vector2.right);

        if (IsGrounded && !hasInput)
        {
            float vx = _body.linearVelocity.x;
            float f = Mathf.Min(Mathf.Abs(vx), friction) * Mathf.Sign(vx);
            _body.AddForce(Vector2.right * -f, ForceMode2D.Impulse);
        }
    }

    private void UpdateGravity(float verticalVelocity)
    {
        if (verticalVelocity > GravityVelocityThreshold)
            _body.gravityScale = jumpGravity;
        else if (verticalVelocity < -GravityVelocityThreshold)
            _body.gravityScale = fallGravity;
        else
            _body.gravityScale = normGravity;
    }

    private bool CheckGrounded()
    {
        if (_collider == null) return false;

        GetGroundCast(out var origin, out var size, out float distance);

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
        int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, filter, _groundHits, distance);

        _standingOneWay.Clear();
        bool grounded = false;
        float feetY = _collider.bounds.min.y;

        for (int i = 0; i < count; i++)
        {
            var hit = _groundHits[i];
            var col = hit.collider;

            if (col.attachedRigidbody == _body) continue;
            if (_droppingThrough.ContainsKey(col)) continue;
            if (hit.normal.y < minGroundNormalY) continue;

            bool oneWay = col.usedByEffector && col.TryGetComponent<PlatformEffector2D>(out _);
            if (oneWay && _body.linearVelocity.y > 0.01f) continue;
            if (oneWay && (hit.distance <= 0f || hit.point.y > feetY + oneWayTolerance)) continue;

            grounded = true;
            if (oneWay)
                _standingOneWay.Add(col);
        }

        return grounded;
    }

    private bool StartDrop(float now)
    {
        if (_standingOneWay.Count == 0 || _collider == null) return false;

        foreach (var platform in _standingOneWay)
        {
            Physics2D.IgnoreCollision(_collider, platform, true);
            _droppingThrough[platform] = now + dropDuration;
        }

        _standingOneWay.Clear();
        _isGrounded = CheckGrounded();
        _lastGroundedTime = float.NegativeInfinity;
        _lastJumpPressedTime = float.NegativeInfinity;
        return true;
    }

    private void RestoreFinishedDrops(float now)
    {
        if (_droppingThrough.Count == 0) return;

        _restoreBuffer.Clear();
        foreach (var pair in _droppingThrough)
        {
            var platform = pair.Key;
            if (platform == null || (now >= pair.Value && !_collider.bounds.Intersects(platform.bounds)))
                _restoreBuffer.Add(platform);
        }

        foreach (var platform in _restoreBuffer)
        {
            if (platform != null && _collider != null)
                Physics2D.IgnoreCollision(_collider, platform, false);
            _droppingThrough.Remove(platform);
        }
    }

    private void OnDisable()
    {
        foreach (var platform in _droppingThrough.Keys)
        {
            if (platform != null && _collider != null)
                Physics2D.IgnoreCollision(_collider, platform, false);
        }

        _droppingThrough.Clear();
        _standingOneWay.Clear();
        _dropRequested = false;
        _windingUp = false;
    }

    private void GetGroundCast(out Vector2 origin, out Vector2 size, out float distance)
    {
        var bounds = _collider.bounds;
        size = new Vector2(bounds.size.x * groundCheckWidth, GroundCastThickness);
        origin = new Vector2(bounds.center.x, bounds.min.y + GroundCastStartOffset);
        distance = GroundCastStartOffset + groundCheckDistance;
    }

    private void UpdateFacing()
    {
        if (visualRoot == null || Mathf.Approximately(_moveInput, 0f)) return;

        bool faceRight = _moveInput > 0f;
        if (faceRight == IsFacingRight) return;

        ApplyFacing(faceRight);
    }

    private void ApplyFacing(bool faceRight)
    {
        IsFacingRight = faceRight;
        if (visualRoot == null) return;

        var scale = visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceRight == spritesFaceRight ? 1f : -1f);
        visualRoot.localScale = scale;
    }

    protected override void StopMovement()
    {
        _moveInput = 0f;
        _wasMoving = false;
        _lastJumpPressedTime = float.NegativeInfinity;
        _windingUp = false;

        if (_body != null)
        {
            var velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_collider == null)
            _collider = GetComponent<Collider2D>();
        if (_collider == null) return;

        GetGroundCast(out var origin, out var size, out float distance);
        var end = origin + Vector2.down * distance;

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(origin, size);
        Gizmos.DrawWireCube(end, size);
        Gizmos.DrawLine(origin + Vector2.left * size.x * 0.5f, end + Vector2.left * size.x * 0.5f);
        Gizmos.DrawLine(origin + Vector2.right * size.x * 0.5f, end + Vector2.right * size.x * 0.5f);
    }
}