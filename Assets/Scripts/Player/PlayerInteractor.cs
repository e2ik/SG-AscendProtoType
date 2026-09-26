using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private string interactActionPath = "Player/Interact";

    public event Action<IInteractable> OnInteractableChanged;

    public IInteractable Current => IsAlive(_current) ? _current : null;

    public IInteractable Available
    {
        get
        {
            var current = Current;
            return current != null && current.CanInteract && IsGrounded ? current : null;
        }
    }

    private bool IsGrounded => player == null || player.Movement == null || player.Movement.IsGrounded;
    public Vector2 CurrentCenter => _currentCollider != null ? (Vector2)_currentCollider.bounds.center : (Vector2)transform.position;

    private readonly Collider2D[] _hits = new Collider2D[16];
    private InputAction _interactAction;
    private IInteractable _current;
    private Collider2D _currentCollider;

    private void Start()
    {
        _interactAction = InputManager.Instance.FindAction(interactActionPath, this);
    }

    private void Update()
    {
        DetectNearest();

        var target = Available;
        if (target != null && _interactAction != null && _interactAction.WasPressedThisFrame())
            target.Interact(player);
    }

    private void DetectNearest()
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = interactableLayer, useTriggers = true };
        int count = Physics2D.OverlapCircle(transform.position, interactRadius, filter, _hits);

        IInteractable nearest = null;
        Collider2D nearestCollider = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (!_hits[i].TryGetComponent<IInteractable>(out var interactable)) continue;

            float dist = ((Vector2)_hits[i].bounds.center - (Vector2)transform.position).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = interactable;
                nearestCollider = _hits[i];
            }
        }

        _currentCollider = nearestCollider;

        if (nearest == _current) return;

        _current = nearest;
        OnInteractableChanged?.Invoke(_current);
    }

    private void OnDisable()
    {
        _current = null;
        _currentCollider = null;
    }

    private static bool IsAlive(IInteractable interactable) =>
        interactable != null && !(interactable is UnityEngine.Object obj && obj == null);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}