using UnityEngine;
using TMPro;

public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.5f);
    [SerializeField] private bool showInteractionText = true;

    [Header("Hover")]
    [SerializeField] private bool hover = true;
    [SerializeField] private float hoverHeight = 0.08f;
    [SerializeField] [Min(0.01f)] private float hoverSpeed = 1.5f;
    [SerializeField] [Min(0f)] private float hoverPixelsPerUnit;

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Collider2D playerCollider;

    private GameObject _prompt;
    private TMP_Text _label;
    private Transform _hoverTarget;
    private Vector3 _hoverRest;
    private SpriteRenderer[] _outlineRenderers;
    private MaterialPropertyBlock _block;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        if (promptPrefab == null) return;

        _prompt = Instantiate(promptPrefab, transform);
        _label = _prompt.GetComponentInChildren<TMP_Text>(true);
        _outlineRenderers = _prompt.GetComponentsInChildren<SpriteRenderer>(true);
        _block = new MaterialPropertyBlock();

        _hoverTarget = FindHoverTarget();
        if (_hoverTarget != null)
            _hoverRest = _hoverTarget.localPosition;

        _prompt.SetActive(false);
    }

    private void Start()
    {
        var manager = InteractableHighlighter.Instance;
        if (manager != null && manager.PromptSettings != null)
            OutlineDriver.AssignMaterial(_outlineRenderers, manager.OutlineMaterial);
    }

    private Transform FindHoverTarget()
    {
        var icon = _prompt.GetComponentInChildren<InputPromptIcon>(true);
        if (icon != null && icon.transform != _prompt.transform) return icon.transform;

        foreach (var sprite in _outlineRenderers)
        {
            if (sprite.transform != _prompt.transform)
                return sprite.transform;
        }

        return null;
    }

    private void LateUpdate()
    {
        if (_prompt == null) return;

        var target = interactor != null ? interactor.Available : null;
        bool canShow = target != null && !IsPlayerFrozen();

        if (!canShow)
        {
            if (_prompt.activeSelf)
                _prompt.SetActive(false);
            return;
        }

        float top = playerCollider != null ? playerCollider.bounds.max.y : transform.position.y;
        _prompt.transform.position = new Vector3(interactor.CurrentCenter.x + offset.x, top + offset.y, transform.position.z);

        if (showInteractionText && _label != null && _label.text != target.InteractionPrompt)
            _label.text = target.InteractionPrompt;

        if (!_prompt.activeSelf)
            _prompt.SetActive(true);

        UpdateHover();
        UpdateOutline();
    }

    private void UpdateHover()
    {
        if (_hoverTarget == null) return;

        if (!hover)
        {
            _hoverTarget.localPosition = _hoverRest;
            return;
        }

        float y = Mathf.Sin(Time.time * hoverSpeed * Mathf.PI * 2f) * hoverHeight;
        if (hoverPixelsPerUnit > 0f)
            y = Mathf.Round(y * hoverPixelsPerUnit) / hoverPixelsPerUnit;

        _hoverTarget.localPosition = _hoverRest + new Vector3(0f, y, 0f);
    }

    private void UpdateOutline()
    {
        var manager = InteractableHighlighter.Instance;
        var settings = manager != null ? manager.PromptSettings : null;
        if (settings == null) return;

        OutlineDriver.Apply(_outlineRenderers, _block, settings, settings.color, OutlineDriver.Evaluate(settings));
    }

    private bool IsPlayerFrozen() =>
        player != null && player.Movement != null && !player.Movement.InputEnabled;

    private void OnDrawGizmosSelected()
    {
        var col = playerCollider != null ? playerCollider : GetComponent<Collider2D>();
        if (col == null) return;

        var point = new Vector3(col.bounds.center.x + offset.x, col.bounds.max.y + offset.y, transform.position.z);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(point, 0.1f);
    }
}