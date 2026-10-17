using UnityEngine;
using UnityEngine.UI;

public class LightShaftScroller : MonoBehaviour
{
    [SerializeField] private EndlessRunnerGame runner;
    [SerializeField] private float speedFactor = 1f;
    [SerializeField] private float idleSpeed = 0f;
    [SerializeField] [Min(0f)] private float worldHeightOverride;

    private static readonly int ScrollId = Shader.PropertyToID("_ScrollOffset");

    private SpriteRenderer _sprite;
    private Graphic _graphic;
    private Material _graphicMaterial;
    private MaterialPropertyBlock _block;
    private float _scroll;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _graphic = GetComponent<Graphic>();

        if (_graphic != null && _graphic.material != null)
        {
            _graphicMaterial = new Material(_graphic.material);
            _graphic.material = _graphicMaterial;
        }

        if (runner == null)
            runner = FindFirstObjectByType<EndlessRunnerGame>();
    }

    private void OnDestroy()
    {
        if (_graphicMaterial != null)
            Destroy(_graphicMaterial);
    }

    private void Update()
    {
        float speed = idleSpeed;
        if (runner != null)
            speed = runner.CurrentPhase == MinigameBase.Phase.Playing ? runner.Speed * speedFactor : idleSpeed;

        float height = WorldHeight();
        if (height > 0.0001f)
            _scroll += speed * Time.deltaTime / height;

        Apply();
    }

    private float WorldHeight()
    {
        if (worldHeightOverride > 0f) return worldHeightOverride;
        if (_sprite != null) return _sprite.bounds.size.y;

        var cam = Camera.main;
        if (cam != null && cam.orthographic) return cam.orthographicSize * 2f;
        return 10f;
    }

    private void Apply()
    {
        if (_sprite != null)
        {
            _block ??= new MaterialPropertyBlock();
            _sprite.GetPropertyBlock(_block);
            _block.SetFloat(ScrollId, _scroll);
            _sprite.SetPropertyBlock(_block);
        }
        else if (_graphicMaterial != null)
        {
            _graphicMaterial.SetFloat(ScrollId, _scroll);
        }
    }
}
