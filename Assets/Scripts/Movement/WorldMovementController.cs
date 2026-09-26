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

    [Header("Ground Control (seconds)")]
    [SerializeField] private float groundAccelTime = 0.18f;
    [SerializeField] private float groundStopTime = 0.12f;
    [SerializeField] private float groundSkidTime = 0.08f;

    [Header("Air Control (seconds)")]
    [SerializeField] private float airAccelTime = 0.22f;
    [SerializeField] private float airTurnTime = 0.18f;
    [SerializeField] private float airStopTime = 1f;

    [Header("Jump (Strength)")]
    [SerializeField] private bool canJump = true;
    [FormerlySerializedAs("baseJumpHeight")]
    [FormerlySerializedAs("jumpHeight")]
    [SerializeField] private float minJumpHeight = 2.5f;
    [SerializeField] private float maxJumpHeight = 5f;
    [SerializeField] private float jumpHeightPerStrength = 0.15f;

    [Header("Jump Feel")]
    [SerializeField] private float timeToApex = 0.3f;
    [SerializeField] private float timeToFall = 0.2f;
    [SerializeField] private float lowJumpMultiplier = 2.5f;
    [SerializeField] private float maxFallSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Stats")]
    [SerializeField] private PlayerStatsController stats;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Facing")]
    [SerializeField] private Transform visualRoot;

    [Header("Input")]
    [SerializeField] private string moveActionPath = "Player/Move";
    [SerializeField] private string jumpActionPath = "Player/Jump";

    public bool IsGrounded { get; private set; }
    public bool IsFacingRight { get; private set; } = true;

    public float CurrentMoveSpeed => Evaluate(GetStat(StatType.Agility), minMoveSpeed, maxMoveSpeed, speedPerAgility);
    public float CurrentJumpHeight => Evaluate(GetStat(StatType.Strength), minJumpHeight, maxJumpHeight, jumpHeightPerStrength);

    private const float GroundStickVelocity = 1f;

    private readonly Collider2D[] _groundHits = new Collider2D[8];
    private Rigidbody2D _body;
    private Collider2D _collider;
    private InputAction _moveAction;
    private InputAction _jumpAction;

    private float _moveInput;
    private bool _jumpHeld;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _lastJumpPressedTime = float.NegativeInfinity;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        _body.gravityScale = 0f;
        _body.freezeRotation = true;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (_collider != null && _collider.sharedMaterial == null)
            _collider.sharedMaterial = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };

        if (stats == null)
            stats = GetComponent<PlayerStatsController>();
    }

    private void OnValidate()
    {
        minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
        maxMoveSpeed = Mathf.Max(minMoveSpeed, maxMoveSpeed);
        speedPerAgility = Mathf.Max(0f, speedPerAgility);

        minJumpHeight = Mathf.Max(0.01f, minJumpHeight);
        maxJumpHeight = Mathf.Max(minJumpHeight, maxJumpHeight);
        jumpHeightPerStrength = Mathf.Max(0f, jumpHeightPerStrength);

        groundAccelTime = Mathf.Max(0f, groundAccelTime);
        groundStopTime = Mathf.Max(0f, groundStopTime);
        groundSkidTime = Mathf.Max(0f, groundSkidTime);
        airAccelTime = Mathf.Max(0f, airAccelTime);
        airTurnTime = Mathf.Max(0f, airTurnTime);
        airStopTime = Mathf.Max(0f, airStopTime);

        timeToApex = Mathf.Max(0.01f, timeToApex);
        timeToFall = Mathf.Max(0.01f, timeToFall);
        lowJumpMultiplier = Mathf.Max(1f, lowJumpMultiplier);
    }

    private float RiseGravity => 2f * minJumpHeight / (timeToApex * timeToApex);
    private float FallGravity => 2f * minJumpHeight / (timeToFall * timeToFall);

    public int GetMaxStat(StatType type) => type switch
    {
        StatType.Agility => MaxStatFor(minMoveSpeed, maxMoveSpeed, speedPerAgility),
        StatType.Strength => MaxStatFor(minJumpHeight, maxJumpHeight, jumpHeightPerStrength),
        _ => int.MaxValue
    };

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

    private void Start()
    {
        _moveAction = InputManager.Instance.FindAction(moveActionPath, this);
        if (canJump)
            _jumpAction = InputManager.Instance.FindAction(jumpActionPath, this);
    }

    private void Update()
    {
        _moveInput = InputEnabled && _moveAction != null ? ReadHorizontal() : 0f;
        _jumpHeld = InputEnabled && _jumpAction != null && _jumpAction.IsPressed();

        if (InputEnabled && _jumpAction != null && _jumpAction.WasPressedThisFrame())
            _lastJumpPressedTime = Time.time;

        UpdateFacing();
    }

    private float ReadHorizontal()
    {
        float x = _moveAction.ReadValue<Vector2>().x;
        return Mathf.Abs(x) >= horizontalDeadzone ? Mathf.Sign(x) : 0f;
    }

    private void FixedUpdate()
    {
        float now = Time.time;

        IsGrounded = CheckGrounded();
        if (IsGrounded)
            _lastGroundedTime = now;

        float riseGravity = RiseGravity;
        float jumpVelocity = Mathf.Sqrt(2f * riseGravity * CurrentJumpHeight);

        var velocity = _body.linearVelocity;
        float targetX = _moveInput * CurrentMoveSpeed;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, GetHorizontalAcceleration(velocity.x, targetX) * Time.fixedDeltaTime);

        bool jumpBuffered = now - _lastJumpPressedTime <= jumpBufferTime;
        bool withinCoyote = now - _lastGroundedTime <= coyoteTime;

        if (canJump && jumpBuffered && withinCoyote)
        {
            velocity.y = jumpVelocity;
            _lastJumpPressedTime = float.NegativeInfinity;
            _lastGroundedTime = float.NegativeInfinity;
        }
        else if (IsGrounded && velocity.y <= 0f)
        {
            velocity.y = -GroundStickVelocity;
        }
        else
        {
            float gravity = velocity.y < 0f
                ? FallGravity
                : riseGravity * (_jumpHeld ? 1f : lowJumpMultiplier);

            velocity.y = Mathf.Max(velocity.y - gravity * Time.fixedDeltaTime, -maxFallSpeed);
        }

        _body.linearVelocity = velocity;
    }

    private float GetHorizontalAcceleration(float current, float target)
    {
        bool hasInput = !Mathf.Approximately(target, 0f);
        bool reversing = hasInput && !Mathf.Approximately(current, 0f) && Mathf.Sign(current) != Mathf.Sign(target);

        float time;
        if (IsGrounded)
            time = !hasInput ? groundStopTime : reversing ? groundSkidTime : groundAccelTime;
        else
            time = !hasInput ? airStopTime : reversing ? airTurnTime : airAccelTime;

        return time <= 0f ? float.PositiveInfinity : CurrentMoveSpeed / time;
    }

    private bool CheckGrounded()
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
        int count = Physics2D.OverlapBox(GetGroundCheckPoint(), groundCheckSize, 0f, filter, _groundHits);

        for (int i = 0; i < count; i++)
        {
            if (_groundHits[i].attachedRigidbody != _body)
                return true;
        }

        return false;
    }

    private Vector2 GetGroundCheckPoint()
    {
        if (groundCheck != null)
            return groundCheck.position;

        if (_collider != null)
        {
            var bounds = _collider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        return transform.position;
    }

    private void UpdateFacing()
    {
        if (visualRoot == null || Mathf.Approximately(_moveInput, 0f)) return;

        bool faceRight = _moveInput > 0f;
        if (faceRight == IsFacingRight) return;

        IsFacingRight = faceRight;
        var scale = visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceRight ? 1f : -1f);
        visualRoot.localScale = scale;
    }

    protected override void StopMovement()
    {
        _moveInput = 0f;
        _jumpHeld = false;
        _lastJumpPressedTime = float.NegativeInfinity;

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

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(GetGroundCheckPoint(), groundCheckSize);
    }
}