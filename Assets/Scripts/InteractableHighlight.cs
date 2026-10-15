using System;
using UnityEngine;

public enum HighlightStyle
{
    Steady,
    Pulse,
    Flash
}

public enum OutlineSide
{
    Inside,
    Outside
}

[Serializable]
public class HighlightSettings
{
    public OutlineSide side = OutlineSide.Outside;
    [Range(1, 4)] public int width = 1;
    public HighlightStyle style = HighlightStyle.Pulse;
    [ColorUsage(true, true)] public Color color = Color.white;
    [Range(0f, 1f)] public float minAmount = 0.3f;
    [Range(0f, 1f)] public float maxAmount = 1f;
    [Min(0.01f)] public float speed = 1.5f;
}

public class InteractableHighlight : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private bool useGlobalSettings = true;
    [SerializeField] private HighlightSettings settings = new HighlightSettings();

    private IInteractable _interactable;
    private PlayerController _player;
    private PlayerInteractor _interactor;
    private MaterialPropertyBlock _block;
    private Color _flashColor;
    private float _flashUntil = float.NegativeInfinity;

    private HighlightSettings Settings
    {
        get
        {
            var manager = InteractableHighlighter.Instance;
            return useGlobalSettings && manager != null ? manager.Settings : settings;
        }
    }

    private void Awake()
    {
        _interactable = GetComponentInParent<IInteractable>();
        _block = new MaterialPropertyBlock();

        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        var manager = InteractableHighlighter.Instance;
        if (manager != null)
            OutlineDriver.AssignMaterial(renderers, manager.OutlineMaterial);
    }

    private void OnDisable()
    {
        OutlineDriver.Apply(renderers, _block, Settings, Color.clear, 0f);
    }

    public void Flash(Color colour, float duration)
    {
        _flashColor = colour;
        _flashUntil = Time.time + duration;
    }

    private void LateUpdate()
    {
        var current = Settings;

        if (Time.time < _flashUntil)
        {
            OutlineDriver.Apply(renderers, _block, current, _flashColor, 1f);
            return;
        }

        if (!IsFocused())
        {
            OutlineDriver.Apply(renderers, _block, current, Color.clear, 0f);
            return;
        }

        OutlineDriver.Apply(renderers, _block, current, current.color, OutlineDriver.Evaluate(current));
    }

    private bool IsFocused()
    {
        if (_interactable == null) return false;

        if (_interactor == null)
        {
            var stats = PlayerStatsController.Active;
            _player = stats != null ? stats.GetComponent<PlayerController>() : null;
            _interactor = _player != null ? _player.Interactor : null;
            if (_interactor == null) return false;
        }

        if (!_interactor.isActiveAndEnabled) return false;
        if (_player != null && _player.Movement != null && !_player.Movement.InputEnabled) return false;

        return ReferenceEquals(_interactor.Available, _interactable);
    }
}