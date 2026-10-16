using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    private static readonly List<CameraFollow2D> Instances = new List<CameraFollow2D>();

    [SerializeField] private PlayerSpawner spawner;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Look Ahead")]
    [SerializeField] private bool useLookAhead = true;
    [SerializeField] [Min(0f)] private float lookAheadDistance = 2f;
    [SerializeField] [Min(0f)] private float lookAheadHoldTime = 0.35f;
    [SerializeField] [Min(0f)] private float lookAheadSmoothTime = 0.6f;
    [SerializeField] private bool keepLookAheadWhenStopped;

    [Header("Look Up / Down")]
    [SerializeField] private bool useVerticalPan = true;
    [SerializeField] [Min(0f)] private float panUpDistance = 3f;
    [SerializeField] [Min(0f)] private float panDownDistance = 3.5f;
    [SerializeField] [Min(0f)] private float panHoldTime = 0.6f;
    [SerializeField] [Min(0f)] private float panSmoothTime = 0.4f;
    [SerializeField] [Range(0.1f, 1f)] private float panInputThreshold = 0.5f;
    [SerializeField] private bool panOnlyWhenStanding = true;

    [Header("Focus Framing")]
    [SerializeField] private bool focusOnDialogue = true;
    [SerializeField] private bool focusNeverZoomsOut = true;
    [SerializeField] [Min(0f)] private float focusPadding = 0.75f;
    [SerializeField] [Min(0.1f)] private float minFocusSize = 1.5f;
    [SerializeField] [Min(0.1f)] private float maxFocusSize = 6f;
    [SerializeField] [Range(-0.5f, 0.5f)] private float focusViewportOffsetY = 0.15f;
    [SerializeField] [Min(0.01f)] private float focusInDuration = 0.6f;
    [SerializeField] [Min(0.01f)] private float focusOutDuration = 0.5f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Rect bounds = new Rect(-20f, -20f, 40f, 40f);

    private Camera _camera;
    private Vector3 _velocity;
    private WorldMovementController _movement;

    private float _defaultSize;
    private float _zoomSize;
    private PixelPerfectCamera _pixelPerfect;
    private int _defaultPixelsPerUnit;
    private int _measureFrame;

    private float _lookAhead;
    private float _lookAheadVelocity;
    private float _holdDirection;
    private float _holdTime;

    private float _pan;
    private float _panVelocity;
    private float _panHoldTime;
    private float _panDirection;

    private readonly List<Transform> _focusTargets = new List<Transform>();
    private bool _transitioning;
    private float _transitionTime;
    private float _transitionDuration;
    private Vector3 _fromPosition;
    private float _fromSize;

    public bool IsFocused => _focusTargets.Count > 0;

    public static void FocusAll(params Transform[] targets)
    {
        foreach (var instance in Instances)
            instance.Focus(targets);
    }

    public static void FocusDialogue(params Transform[] targets)
    {
        foreach (var instance in Instances)
        {
            if (instance.focusOnDialogue)
                instance.Focus(targets);
        }
    }

    public static void ClearFocusAll()
    {
        foreach (var instance in Instances)
            instance.ClearFocus();
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _pixelPerfect = GetComponent<PixelPerfectCamera>();

        if (_pixelPerfect != null)
            _defaultPixelsPerUnit = Mathf.Max(1, _pixelPerfect.assetsPPU);

        _defaultSize = _camera.orthographicSize;
        _zoomSize = _defaultSize;
        _measureFrame = Time.frameCount + 2;
    }

    private void OnEnable()
    {
        Instances.Add(this);

        if (spawner == null) return;

        spawner.PlayerSpawned += OnPlayerSpawned;
        if (spawner.Player != null)
            SetTarget(spawner.Player.transform);
    }

    private void OnDisable()
    {
        Instances.Remove(this);

        if (spawner != null)
            spawner.PlayerSpawned -= OnPlayerSpawned;
    }

    private void OnDestroy()
    {
        if (_pixelPerfect != null && _defaultPixelsPerUnit > 0)
            _pixelPerfect.assetsPPU = _defaultPixelsPerUnit;
    }

    private void OnPlayerSpawned(PlayerController player) => SetTarget(player.transform);

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        _movement = target != null ? target.GetComponentInParent<WorldMovementController>() : null;
        if (_movement == null && target != null)
            _movement = target.GetComponentInChildren<WorldMovementController>();

        ResetOffsets();

        if (snap && target != null)
        {
            transform.position = GetFollowPosition(_zoomSize);
            _velocity = Vector3.zero;
        }
    }

    public void Focus(params Transform[] targets)
    {
        _focusTargets.Clear();
        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t != null && !_focusTargets.Contains(t))
                    _focusTargets.Add(t);
            }
        }

        if (IsFocused)
            BeginTransition(focusInDuration);
    }

    public void ClearFocus()
    {
        if (!IsFocused) return;

        _focusTargets.Clear();
        BeginTransition(focusOutDuration);
    }

    private void BeginTransition(float duration)
    {
        _fromPosition = transform.position;
        _fromSize = _zoomSize;
        _transitionTime = 0f;
        _transitionDuration = duration;
        _transitioning = true;
        _velocity = Vector3.zero;
    }

    private void ResetOffsets()
    {
        _lookAhead = 0f;
        _lookAheadVelocity = 0f;
        _holdTime = 0f;
        _holdDirection = 0f;
        _pan = 0f;
        _panVelocity = 0f;
        _panHoldTime = 0f;
        _panDirection = 0f;
    }

    private void LateUpdate()
    {
        if (!_camera.enabled) return;

        float dt = Time.unscaledDeltaTime;

        if (_focusTargets.RemoveAll(t => t == null) > 0 && !IsFocused)
            BeginTransition(focusOutDuration);

        MeasureDefaultSize();

        if (IsFocused)
        {
            DecayOffsets(dt);
            GetFocusGoal(out var position, out var size);
            Transition(position, size, dt);
            return;
        }

        if (target == null) return;

        if (_transitioning)
        {
            DecayOffsets(dt);
            Transition(GetFollowPosition(_defaultSize), _defaultSize, dt);
            return;
        }

        ApplyZoom(_defaultSize);
        UpdateLookAhead(dt);
        UpdateVerticalPan(dt);

        var desired = GetFollowPosition(_zoomSize);
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime, Mathf.Infinity, dt)
            : desired;
    }

    private void Transition(Vector3 goalPosition, float goalSize, float dt)
    {
        if (!_transitioning)
        {
            ApplyZoom(goalSize);
            transform.position = goalPosition;
            return;
        }

        _transitionTime += dt;
        float t = Mathf.Clamp01(_transitionTime / _transitionDuration);
        float eased = t * t * (3f - 2f * t);

        ApplyZoom(Mathf.Lerp(_fromSize, goalSize, eased));
        transform.position = Vector3.Lerp(_fromPosition, goalPosition, eased);

        if (t >= 1f)
        {
            _transitioning = false;
            _velocity = Vector3.zero;
        }
    }

    private void MeasureDefaultSize()
    {
        if (_pixelPerfect == null || IsFocused || _transitioning) return;
        if (Time.frameCount < _measureFrame || _pixelPerfect.assetsPPU != _defaultPixelsPerUnit) return;

        _defaultSize = _camera.orthographicSize;
        _zoomSize = _defaultSize;
    }

    private void ApplyZoom(float size)
    {
        if (!_camera.orthographic) return;

        _zoomSize = size;

        if (_pixelPerfect == null)
        {
            _camera.orthographicSize = size;
            return;
        }

        float factor = _defaultSize / Mathf.Max(0.01f, size);
        int pixelsPerUnit = Mathf.Max(1, Mathf.RoundToInt(_defaultPixelsPerUnit * factor));

        if (_pixelPerfect.assetsPPU != pixelsPerUnit)
            _pixelPerfect.assetsPPU = pixelsPerUnit;
    }

    private void DecayOffsets(float dt)
    {
        _lookAhead = Mathf.SmoothDamp(_lookAhead, 0f, ref _lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);
        _pan = Mathf.SmoothDamp(_pan, 0f, ref _panVelocity, panSmoothTime, Mathf.Infinity, dt);
        _holdTime = 0f;
        _panHoldTime = 0f;
    }

    private bool HasControl => _movement != null && _movement.InputEnabled && _movement.isActiveAndEnabled;

    private void UpdateLookAhead(float dt)
    {
        if (!useLookAhead)
        {
            _lookAhead = 0f;
            return;
        }

        float input = HasControl ? _movement.MoveInput : 0f;
        float direction = Mathf.Approximately(input, 0f) ? 0f : Mathf.Sign(input);

        if (direction != _holdDirection)
        {
            _holdDirection = direction;
            _holdTime = 0f;
        }
        else
        {
            _holdTime += dt;
        }

        float goal = _lookAhead;
        if (direction != 0f && _holdTime >= lookAheadHoldTime)
            goal = direction * lookAheadDistance;
        else if (direction == 0f && !keepLookAheadWhenStopped)
            goal = 0f;

        _lookAhead = Mathf.SmoothDamp(_lookAhead, goal, ref _lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);
    }

    private void UpdateVerticalPan(float dt)
    {
        if (!useVerticalPan)
        {
            _pan = 0f;
            return;
        }

        float input = HasControl ? _movement.MoveInputY : 0f;
        bool standing = !panOnlyWhenStanding || (_movement != null && _movement.IsGrounded && Mathf.Approximately(_movement.MoveInput, 0f));

        float direction = 0f;
        if (standing && input >= panInputThreshold) direction = 1f;
        else if (standing && input <= -panInputThreshold) direction = -1f;

        if (direction != _panDirection)
        {
            _panDirection = direction;
            _panHoldTime = 0f;
        }
        else
        {
            _panHoldTime += dt;
        }

        float goal = 0f;
        if (direction != 0f && _panHoldTime >= panHoldTime)
            goal = direction > 0f ? panUpDistance : -panDownDistance;

        _pan = Mathf.SmoothDamp(_pan, goal, ref _panVelocity, panSmoothTime, Mathf.Infinity, dt);
    }

    private void GetFocusGoal(out Vector3 position, out float size)
    {
        var area = GetFocusBounds();

        float fitHeight = area.extents.y + focusPadding;
        float fitWidth = (area.extents.x + focusPadding) / Mathf.Max(0.01f, _camera.aspect);
        size = Mathf.Clamp(Mathf.Max(fitHeight, fitWidth), minFocusSize, Mathf.Max(minFocusSize, maxFocusSize));
        if (focusNeverZoomsOut)
            size = Mathf.Min(size, _defaultSize);

        var centre = area.center;
        centre.y -= focusViewportOffsetY * 2f * size;
        position = new Vector3(centre.x, centre.y, transform.position.z);

        if (useBounds)
            position = ClampToBounds(position, size);
    }

    private Bounds GetFocusBounds()
    {
        bool started = false;
        var area = new Bounds();

        foreach (var t in _focusTargets)
        {
            var b = BoundsOf(t);
            if (!started)
            {
                area = b;
                started = true;
            }
            else
            {
                area.Encapsulate(b);
            }
        }

        return area;
    }

    private static Bounds BoundsOf(Transform t)
    {
        bool found = false;
        var b = new Bounds(t.position, Vector3.zero);

        foreach (var renderer in t.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            if (!found)
            {
                b = renderer.bounds;
                found = true;
            }
            else
            {
                b.Encapsulate(renderer.bounds);
            }
        }

        if (found) return b;

        foreach (var collider in t.GetComponents<Collider2D>())
        {
            if (collider.enabled && !collider.isTrigger)
                return collider.bounds;
        }

        return b;
    }

    private Vector3 GetFollowPosition(float size)
    {
        var desired = new Vector3(target.position.x + offset.x + _lookAhead, target.position.y + offset.y + _pan, transform.position.z);
        return useBounds ? ClampToBounds(desired, size) : desired;
    }

    private Vector3 ClampToBounds(Vector3 position, float size)
    {
        if (!_camera.orthographic) return position;

        float halfHeight = size;
        float halfWidth = halfHeight * _camera.aspect;

        float minX = bounds.xMin + halfWidth;
        float maxX = bounds.xMax - halfWidth;
        float minY = bounds.yMin + halfHeight;
        float maxY = bounds.yMax - halfHeight;

        position.x = minX > maxX ? bounds.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? bounds.center.y : Mathf.Clamp(position.y, minY, maxY);
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}