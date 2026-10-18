using System.Collections.Generic;
using UnityEngine;

public class LoopingScroller : MonoBehaviour
{
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private float segmentWidth = 20f;
    [SerializeField] private bool autoWidth = true;
    [SerializeField] private bool autoArrange = true;
    [SerializeField] private bool autoFillScreen = true;
    [SerializeField] private float overlap;
    [SerializeField] private Camera targetCamera;

    private readonly List<Transform> _segments = new List<Transform>();
    private float _width;
    private float _leftOffset;
    private bool _prepared;

    private void Start() => Prepare();

    private void Prepare()
    {
        if (_prepared) return;
        _prepared = true;

        if (targetCamera == null)
            targetCamera = Camera.main;

        _segments.Clear();
        for (int i = 0; i < transform.childCount; i++)
            _segments.Add(transform.GetChild(i));
        if (_segments.Count == 0) return;

        _segments.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));

        var first = _segments[0];
        if (autoWidth && TryMeasure(first, out float width, out float leftOffset))
        {
            _width = width;
            _leftOffset = leftOffset;
        }
        else
        {
            _width = segmentWidth;
            _leftOffset = -segmentWidth * 0.5f;
        }
        _width -= overlap;
        if (_width <= 0f) return;

        if (autoArrange)
        {
            float startX = first.localPosition.x;
            for (int i = 0; i < _segments.Count; i++)
                SetX(_segments[i], startX + _width * i);
        }

        if (autoFillScreen)
            FillScreen();
    }

    private void FillScreen()
    {
        if (!TryGetCameraRange(out float left, out float right)) return;

        float needed = right - left + _width * 2f;
        int required = Mathf.CeilToInt(needed / _width) + 1;
        var template = _segments[0];

        while (_segments.Count < required)
        {
            var last = _segments[_segments.Count - 1];
            var copy = Instantiate(template, transform);
            copy.name = template.name;
            copy.localRotation = template.localRotation;
            copy.localScale = template.localScale;
            var position = last.localPosition;
            position.x += _width;
            copy.localPosition = position;
            _segments.Add(copy);
        }
    }

    private bool TryMeasure(Transform segment, out float width, out float leftOffset)
    {
        width = 0f;
        leftOffset = 0f;

        bool found = false;
        var bounds = new Bounds();
        foreach (var r in segment.GetComponentsInChildren<Renderer>())
        {
            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        if (!found) return false;

        float minLocal = transform.InverseTransformPoint(bounds.min).x;
        float maxLocal = transform.InverseTransformPoint(bounds.max).x;
        width = Mathf.Abs(maxLocal - minLocal);
        leftOffset = Mathf.Min(minLocal, maxLocal) - segment.localPosition.x;
        return width > 0.0001f;
    }

    private bool TryGetCameraRange(out float left, out float right)
    {
        left = right = 0f;
        if (targetCamera == null || !targetCamera.orthographic) return false;

        float halfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        var centre = targetCamera.transform.position;
        float a = transform.InverseTransformPoint(new Vector3(centre.x - halfWidth, centre.y, 0f)).x;
        float b = transform.InverseTransformPoint(new Vector3(centre.x + halfWidth, centre.y, 0f)).x;
        left = Mathf.Min(a, b);
        right = Mathf.Max(a, b);
        return true;
    }

    private static void SetX(Transform segment, float x)
    {
        var position = segment.localPosition;
        position.x = x;
        segment.localPosition = position;
    }

    public void Tick(float speed, float deltaTime)
    {
        if (!_prepared) Prepare();
        if (_segments.Count == 0 || _width <= 0f) return;

        float distance = speed * speedMultiplier * deltaTime;
        float rightmost = float.MinValue;

        foreach (var segment in _segments)
        {
            if (segment == null) continue;
            SetX(segment, segment.localPosition.x - distance);
            rightmost = Mathf.Max(rightmost, segment.localPosition.x);
        }

        bool hasCamera = TryGetCameraRange(out float cameraLeft, out _);

        foreach (var segment in _segments)
        {
            if (segment == null) continue;

            float rightEdge = segment.localPosition.x + _leftOffset + _width;
            bool offLeft = hasCamera ? rightEdge < cameraLeft : segment.localPosition.x < rightmost - _width * (_segments.Count - 1) - _width * 0.5f;
            if (!offLeft) continue;

            rightmost += _width;
            SetX(segment, rightmost);
        }
    }
}