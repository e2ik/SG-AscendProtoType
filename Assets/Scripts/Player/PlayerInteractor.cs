using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private float interactRadius = 1.5f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private string interactActionPath = "Gameplay/Interact";

    public event Action<IInteractable> OnInteractableChanged;

    private readonly Collider2D[] _hits = new Collider2D[16];
    private InputAction _interactAction;
    private IInteractable _current;

    private void Start()
    {
        _interactAction = InputManager.Instance.FindAction(interactActionPath);
    }

    private void Update()
    {
        DetectNearest();

        if (_current != null && _current.CanInteract && _interactAction != null && _interactAction.WasPressedThisFrame())
            _current.Interact(player);
    }

    private void DetectNearest()
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = interactableLayer, useTriggers = true };
        int count = Physics2D.OverlapCircle(transform.position, interactRadius, filter, _hits);

        IInteractable nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (!_hits[i].TryGetComponent<IInteractable>(out var interactable)) continue;

            float dist = ((Vector2)_hits[i].transform.position - (Vector2)transform.position).sqrMagnitude;
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = interactable;
            }
        }

        if (nearest != _current)
        {
            _current = nearest;
            OnInteractableChanged?.Invoke(_current);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}