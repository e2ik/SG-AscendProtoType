using UnityEngine;

public class PlayerAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private WorldMovementController movement;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;

    [Header("Parameters")]
    [SerializeField] private string jumpStateName = "P_JumpUp";
    [SerializeField] private string movingParameter = "Moving";
    [SerializeField] private string walkSpeedParameter = "WalkSpeed";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string fallingParameter = "Falling";
    [SerializeField] private string descendingParameter = "Descending";
    [SerializeField] private string landingParameter = "Landing";

    [Header("Walk Speed")]
    [SerializeField] [Min(0.01f)] private float referenceSpeed = 5f;
    [SerializeField] [Min(0f)] private float minWalkMultiplier = 0.5f;
    [SerializeField] [Min(0f)] private float maxWalkMultiplier = 2.5f;
    [SerializeField] [Min(0f)] private float movingThreshold = 0.1f;
    [SerializeField] private bool requireInputToWalk = true;

    [Header("Air")]
    [SerializeField] [Min(0f)] private float apexThreshold = 1f;
    [SerializeField] [Min(0f)] private float minAirTime = 0.06f;
    [SerializeField] private float descendingVelocity = -0.1f;

    [Header("Landing")]
    [SerializeField] [Min(0f)] private float landLeadTime = 0.1f;
    [SerializeField] [Min(0f)] private float minLandDistance = 0.1f;
    [SerializeField] [Min(0.1f)] private float maxProbeDistance = 5f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

    private int _jumpStateHash;
    private int _movingHash;
    private int _walkSpeedHash;
    private int _groundedHash;
    private int _fallingHash;
    private int _descendingHash;
    private int _landingHash;

    private bool _hasMoving;
    private bool _hasWalkSpeed;
    private bool _hasGrounded;
    private bool _hasFalling;
    private bool _hasDescending;
    private bool _hasLanding;
    private bool _parametersCached;

    private float _airTime;
    private bool _landing;
    private bool _wasAirborne;
    private bool _airFromJump;
    private bool _leftGroundSinceJump;
    private float _jumpStartTime;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (movement == null)
            movement = GetComponentInParent<WorldMovementController>();
        if (body == null)
            body = movement != null ? movement.GetComponent<Rigidbody2D>() : GetComponentInParent<Rigidbody2D>();
        if (bodyCollider == null && body != null)
            bodyCollider = body.GetComponent<Collider2D>();

        _jumpStateHash = Animator.StringToHash(jumpStateName);
        _movingHash = Animator.StringToHash(movingParameter);
        _walkSpeedHash = Animator.StringToHash(walkSpeedParameter);
        _groundedHash = Animator.StringToHash(groundedParameter);
        _fallingHash = Animator.StringToHash(fallingParameter);
        _descendingHash = Animator.StringToHash(descendingParameter);
        _landingHash = Animator.StringToHash(landingParameter);
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.JumpStarted += OnJumpStarted;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.JumpStarted -= OnJumpStarted;
    }

    private void OnJumpStarted()
    {
        _airFromJump = true;
        _leftGroundSinceJump = false;
        _jumpStartTime = Time.time;

        if (animator == null || !animator.isActiveAndEnabled) return;

        if (animator.HasState(0, _jumpStateHash))
            animator.Play(_jumpStateHash, 0, 0f);
        else
            Debug.LogWarning($"PlayerAnimationDriver can't find a '{jumpStateName}' state on the Animator", this);
    }

    private void CacheParameters()
    {
        if (_parametersCached || animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
        _parametersCached = true;

        foreach (var parameter in animator.parameters)
        {
            bool isBool = parameter.type == AnimatorControllerParameterType.Bool;

            if (parameter.nameHash == _movingHash && isBool) _hasMoving = true;
            if (parameter.nameHash == _walkSpeedHash && parameter.type == AnimatorControllerParameterType.Float) _hasWalkSpeed = true;
            if (parameter.nameHash == _groundedHash && isBool) _hasGrounded = true;
            if (parameter.nameHash == _fallingHash && isBool) _hasFalling = true;
            if (parameter.nameHash == _descendingHash && isBool) _hasDescending = true;
            if (parameter.nameHash == _landingHash && isBool) _hasLanding = true;
        }
    }

    private void Update()
    {
        if (animator == null || body == null) return;
        CacheParameters();

        var velocity = body.linearVelocity;
        float speed = Mathf.Abs(velocity.x);
        bool grounded = movement == null || movement.IsGrounded;
        bool windingUp = movement != null && movement.IsWindingUp;

        _airTime = grounded ? 0f : _airTime + Time.deltaTime;

        bool rising = !grounded && velocity.y > apexThreshold;
        bool airborne = !grounded && (rising || _airTime >= minAirTime);
        bool falling = airborne && !rising;

        if (airborne)
            _leftGroundSinceJump = true;

        bool jumpFizzled = _airFromJump && !_leftGroundSinceJump && !windingUp && grounded && Time.time - _jumpStartTime > 0.3f;
        if ((!airborne && _wasAirborne && !windingUp) || jumpFizzled)
            _airFromJump = false;
        _wasAirborne = airborne;

        if (grounded)
            _landing = false;
        else if (falling && !_landing)
            _landing = AboutToLand(velocity.y);

        bool hasInput = movement == null || !Mathf.Approximately(movement.MoveInput, 0f);
        bool moving = !airborne && !windingUp && speed > movingThreshold && (!requireInputToWalk || hasInput);
        float multiplier = Mathf.Clamp(speed / referenceSpeed, minWalkMultiplier, maxWalkMultiplier);

        if (_hasMoving) animator.SetBool(_movingHash, moving);
        if (_hasWalkSpeed) animator.SetFloat(_walkSpeedHash, moving ? multiplier : 1f);
        if (_hasGrounded) animator.SetBool(_groundedHash, !airborne && !_airFromJump);
        if (_hasDescending) animator.SetBool(_descendingHash, airborne && velocity.y < descendingVelocity);
        if (_hasFalling) animator.SetBool(_fallingHash, falling && !_airFromJump && !_landing);
        if (_hasLanding) animator.SetBool(_landingHash, _landing);
    }

    private bool AboutToLand(float verticalVelocity)
    {
        if (bodyCollider == null || verticalVelocity >= 0f) return false;

        float leadDistance = Mathf.Max(minLandDistance, -verticalVelocity * landLeadTime);
        return DistanceToGround() <= leadDistance;
    }

    private float DistanceToGround()
    {
        var bounds = bodyCollider.bounds;
        var origin = new Vector2(bounds.center.x, bounds.min.y + 0.05f);
        var size = new Vector2(bounds.size.x * 0.9f, 0.02f);

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
        int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, filter, _hits, maxProbeDistance);

        float closest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = _hits[i];
            if (hit.collider == null || hit.collider.attachedRigidbody == body) continue;
            if (hit.normal.y < 0.7f) continue;
            if (Physics2D.GetIgnoreCollision(bodyCollider, hit.collider)) continue;

            closest = Mathf.Min(closest, hit.distance - 0.05f);
        }

        return closest;
    }
}