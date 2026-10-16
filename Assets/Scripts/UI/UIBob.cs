using UnityEngine;

public class UIBob : MonoBehaviour
{
    [SerializeField] private Vector2 amplitude = new Vector2(0f, 6f);
    [SerializeField] [Min(0.01f)] private float cyclesPerSecond = 0.25f;
    [SerializeField] [Min(0f)] private float stepsPerSecond;
    [SerializeField] private bool snapToPixels = true;
    [SerializeField] [Min(0.01f)] private float pixelSize = 1f;
    [SerializeField] private bool useUnscaledTime = true;

    private RectTransform _rect;
    private Vector3 _rest;
    private bool _captured;
    private float _time;
    private float _stepTimer;
    private Vector2 _offset;

    private void Awake()
    {
        _rect = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (!_captured)
        {
            _rest = _rect != null ? (Vector3)_rect.anchoredPosition : transform.localPosition;
            _captured = true;
        }

        _time = 0f;
        _stepTimer = 0f;
        _offset = Vector2.zero;
    }

    private void OnDisable()
    {
        if (!_captured) return;

        if (_rect != null)
            _rect.anchoredPosition = _rest;
        else
            transform.localPosition = _rest;
    }

    private void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _time += dt;

        bool stepped = stepsPerSecond > 0f;
        if (stepped)
        {
            _stepTimer += dt;
            if (_stepTimer >= 1f / stepsPerSecond)
            {
                _stepTimer = 0f;
                _offset = Evaluate();
            }
        }
        else
        {
            _offset = Evaluate();
        }

        if (_rect != null)
            _rect.anchoredPosition = (Vector2)_rest + _offset;
        else
            transform.localPosition = _rest + (Vector3)_offset;
    }

    private Vector2 Evaluate()
    {
        float wave = Mathf.Sin(_time * cyclesPerSecond * Mathf.PI * 2f);
        var offset = amplitude * wave;

        if (snapToPixels)
            offset = new Vector2(Mathf.Round(offset.x / pixelSize) * pixelSize, Mathf.Round(offset.y / pixelSize) * pixelSize);

        return offset;
    }
}