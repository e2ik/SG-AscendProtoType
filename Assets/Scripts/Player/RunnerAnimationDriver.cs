using UnityEngine;

public class RunnerAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RunnerPlayer player;
    [SerializeField] private EndlessRunnerGame game;

    [Header("States")]
    [SerializeField] private string jumpStateName = "P_JumpUp";
    [SerializeField] private string idleStateName = "P_Idle";

    [Header("Parameters")]
    [SerializeField] private string movingParameter = "Moving";
    [SerializeField] private string walkSpeedParameter = "WalkSpeed";
    [SerializeField] private string groundedParameter = "Grounded";
    [SerializeField] private string descendingParameter = "Descending";

    [Header("Run Speed")]
    [SerializeField] [Min(0.01f)] private float referenceSpeed = 7f;
    [SerializeField] [Min(0f)] private float minWalkMultiplier = 1f;
    [SerializeField] [Min(0f)] private float maxWalkMultiplier = 2.5f;

    [Header("Air")]
    [SerializeField] private float descendingVelocity = -0.1f;
    [SerializeField] [Min(0.1f)] private float jumpUpSpeed = 1.5f;

    private int _jumpStateHash;
    private int _idleStateHash;
    private int _movingHash;
    private int _walkSpeedHash;
    private int _groundedHash;
    private int _descendingHash;

    private bool _hasMoving;
    private bool _hasWalkSpeed;
    private bool _hasGrounded;
    private bool _hasDescending;
    private bool _parametersCached;
    private MinigameBase.Phase _lastPhase = MinigameBase.Phase.Idle;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (player == null)
            player = GetComponentInParent<RunnerPlayer>();
        if (game == null)
            game = FindFirstObjectByType<EndlessRunnerGame>();

        _jumpStateHash = Animator.StringToHash(jumpStateName);
        _idleStateHash = Animator.StringToHash(idleStateName);
        _movingHash = Animator.StringToHash(movingParameter);
        _walkSpeedHash = Animator.StringToHash(walkSpeedParameter);
        _groundedHash = Animator.StringToHash(groundedParameter);
        _descendingHash = Animator.StringToHash(descendingParameter);
    }

    private void OnEnable()
    {
        if (player != null)
            player.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        if (player != null)
            player.Jumped -= OnJumped;

        if (animator != null)
            animator.speed = 1f;
    }

    private void OnJumped() => PlayState(_jumpStateHash, jumpStateName);

    private void PlayState(int hash, string stateName)
    {
        if (animator == null || !animator.isActiveAndEnabled) return;

        if (animator.HasState(0, hash))
            animator.Play(hash, 0, 0f);
        else
            Debug.LogWarning($"RunnerAnimationDriver can't find a '{stateName}' state on the Animator", this);
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
            if (parameter.nameHash == _descendingHash && isBool) _hasDescending = true;
        }
    }

    private void Update()
    {
        if (animator == null || player == null) return;
        CacheParameters();

        var phase = game != null ? game.CurrentPhase : MinigameBase.Phase.Playing;
        bool running = phase == MinigameBase.Phase.Playing;
        bool grounded = player.IsGrounded;

        if (phase != _lastPhase)
        {
            if (phase == MinigameBase.Phase.Idle || phase == MinigameBase.Phase.Intro || phase == MinigameBase.Phase.Countdown)
                PlayState(_idleStateHash, idleStateName);
            _lastPhase = phase;
        }

        float speed = game != null ? game.Speed : referenceSpeed;
        float multiplier = Mathf.Clamp(speed / referenceSpeed, minWalkMultiplier, maxWalkMultiplier);

        if (_hasMoving) animator.SetBool(_movingHash, running && grounded);
        if (_hasWalkSpeed) animator.SetFloat(_walkSpeedHash, running ? multiplier : 1f);
        if (_hasGrounded) animator.SetBool(_groundedHash, grounded);
        if (_hasDescending) animator.SetBool(_descendingHash, !grounded && player.VelocityY < descendingVelocity && JumpUpFinished());

        animator.speed = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == _jumpStateHash ? jumpUpSpeed : 1f;
    }

    private bool JumpUpFinished()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != _jumpStateHash) return true;
        return !animator.IsInTransition(0) && state.normalizedTime >= 1f;
    }
}