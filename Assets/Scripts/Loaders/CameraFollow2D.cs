using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private PlayerSpawner spawner;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Rect bounds = new Rect(-20f, -20f, 40f, 40f);

    private Camera _camera;
    private Vector3 _velocity;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (spawner == null) return;

        spawner.PlayerSpawned += OnPlayerSpawned;
        if (spawner.Player != null)
            SetTarget(spawner.Player.transform);
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.PlayerSpawned -= OnPlayerSpawned;
    }

    private void OnPlayerSpawned(PlayerController player) => SetTarget(player.transform);

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        if (snap && target != null)
        {
            transform.position = GetDesiredPosition();
            _velocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (target == null || !_camera.enabled) return;

        var desired = GetDesiredPosition();
        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime)
            : desired;
    }

    private Vector3 GetDesiredPosition()
    {
        var desired = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        return useBounds ? ClampToBounds(desired) : desired;
    }

    private Vector3 ClampToBounds(Vector3 position)
    {
        if (!_camera.orthographic) return position;

        float halfHeight = _camera.orthographicSize;
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