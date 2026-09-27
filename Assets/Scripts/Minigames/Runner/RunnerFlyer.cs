using UnityEngine;

[RequireComponent(typeof(RunnerObstacle))]
public class RunnerFlyer : MonoBehaviour
{
    [Header("Vertical Bob")]
    [SerializeField] [Min(0f)] private float bobAmplitude = 0.5f;
    [SerializeField] [Min(0f)] private float bobCyclesPerSecond = 1.2f;

    [Header("Horizontal Sway")]
    [SerializeField] [Min(0f)] private float swayAmplitude;
    [SerializeField] [Min(0f)] private float swayCyclesPerSecond = 0.8f;

    [Header("Variation")]
    [SerializeField] [Range(0f, 1f)] private float minAmplitudeFraction = 0.4f;
    [SerializeField] [Range(0f, 0.9f)] private float speedVariation = 0.25f;

    [Header("Size")]
    [SerializeField] [Min(0f)] private float halfHeightOverride;

    public float BobAmplitude => bobAmplitude;
    public float SwayAmplitude => swayAmplitude;
    public float HalfHeight => halfHeightOverride > 0f ? halfHeightOverride : MeasureHalfHeight();

    private float _baseX;
    private float _baseY;
    private float _bob;
    private float _bobSpeed;
    private float _bobPhase;
    private float _sway;
    private float _swaySpeed;
    private float _swayPhase;
    private float _time;

    public void Launch(float baseX, float baseY, float maxBob)
    {
        _baseX = baseX;
        _baseY = baseY;

        _bob = maxBob * Random.Range(minAmplitudeFraction, 1f);
        _bobSpeed = bobCyclesPerSecond * Random.Range(1f - speedVariation, 1f + speedVariation);
        _bobPhase = Random.value * Mathf.PI * 2f;

        _sway = swayAmplitude * Random.Range(minAmplitudeFraction, 1f);
        _swaySpeed = swayCyclesPerSecond * Random.Range(1f - speedVariation, 1f + speedVariation);
        _swayPhase = Random.value * Mathf.PI * 2f;

        _time = 0f;
        ApplyPosition();
    }

    public void Tick(float deltaTime, float scrollDistance)
    {
        _time += deltaTime;
        _baseX -= scrollDistance;
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        var position = transform.position;
        position.x = _baseX + Mathf.Sin(_swayPhase + _time * _swaySpeed * Mathf.PI * 2f) * _sway;
        position.y = _baseY + Mathf.Sin(_bobPhase + _time * _bobSpeed * Mathf.PI * 2f) * _bob;
        transform.position = position;
    }

    private float MeasureHalfHeight()
    {
        var box = GetComponentInChildren<BoxCollider2D>();
        if (box != null)
            return box.size.y * Mathf.Abs(box.transform.lossyScale.y) * 0.5f;

        var sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
            return sprite.sprite.bounds.extents.y * Mathf.Abs(sprite.transform.lossyScale.y);

        return 0.5f;
    }
}