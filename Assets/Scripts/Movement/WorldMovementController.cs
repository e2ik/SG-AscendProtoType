using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class WorldMovementController : MovementController
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private string moveActionPath = "Gameplay/Move";

    private Rigidbody2D _body;
    private InputAction _moveAction;
    private Vector2 _input;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.gravityScale = 0f;
        _body.freezeRotation = true;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        _moveAction = InputManager.Instance.FindAction(moveActionPath);
    }

    private void Update()
    {
        _input = InputEnabled && _moveAction != null
            ? Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f)
            : Vector2.zero;
    }

    private void FixedUpdate()
    {
        _body.linearVelocity = _input * moveSpeed;
    }

    protected override void StopMovement()
    {
        _input = Vector2.zero;
        if (_body != null)
            _body.linearVelocity = Vector2.zero;
    }
}