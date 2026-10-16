using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(2000)]
[RequireComponent(typeof(RectTransform))]
public class ChoicesFollowPlayer : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Vector2 screenOffset = new Vector2(12f, 0f);
    [SerializeField] [Range(0f, 1f)] private float playerHeightPoint = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float verticalPivot = 0.5f;
    [SerializeField] private bool sideFacingSpeaker = true;
    [SerializeField] private bool keepOnScreen = true;
    [SerializeField] [Min(0f)] private float screenMargin = 8f;
    [SerializeField] private bool snapToPixels = true;

    private RectTransform _rect;
    private RectTransform _parent;
    private Canvas _canvas;
    private ArcVerticalLayoutGroup _arc;
    private LayoutGroup _layout;
    private bool _lastOnRight = true;

    public bool OnRight => _lastOnRight;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _parent = _rect.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        _arc = GetComponent<ArcVerticalLayoutGroup>();
        _layout = GetComponent<LayoutGroup>();
    }

    private void LateUpdate()
    {
        var target = ResolvePlayer();
        var camera = ActiveCamera();
        if (target == null || camera == null || _parent == null) return;

        bool onRight = true;
        if (sideFacingSpeaker && DialogueManager.Instance != null)
            onRight = DialogueManager.Instance.SpeakerSide == DialogueSide.Right;

        ApplySide(onRight);

        var bounds = BoundsOf(target);
        var world = new Vector3(onRight ? bounds.max.x : bounds.min.x, Mathf.Lerp(bounds.min.y, bounds.max.y, playerHeightPoint), bounds.center.z);
        var screen = (Vector2)camera.WorldToScreenPoint(world);

        var uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, uiCamera, out var local)) return;

        local += new Vector2(onRight ? screenOffset.x : -screenOffset.x, screenOffset.y);

        if (keepOnScreen)
            local = ClampInside(local);

        if (snapToPixels)
            local = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));

        _rect.localPosition = new Vector3(local.x, local.y, _rect.localPosition.z);
    }

    private void ApplySide(bool onRight)
    {
        var pivot = new Vector2(onRight ? 0f : 1f, verticalPivot);
        if (_rect.pivot != pivot)
            _rect.pivot = pivot;

        if (onRight == _lastOnRight) return;
        _lastOnRight = onRight;

        if (_arc != null)
            _arc.FlipDirection = !onRight;

        if (_layout != null)
            _layout.childAlignment = onRight ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
    }

    private Rect ScreenAreaInParent()
    {
        var root = _canvas != null ? _canvas.rootCanvas.transform as RectTransform : null;
        if (root == null) return _parent.rect;

        var corners = new Vector3[4];
        root.GetWorldCorners(corners);
        var min = (Vector2)_parent.InverseTransformPoint(corners[0]);
        var max = (Vector2)_parent.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    private Vector2 ClampInside(Vector2 local)
    {
        var area = ScreenAreaInParent();
        area.xMin += screenMargin;
        area.xMax -= screenMargin;
        area.yMin += screenMargin;
        area.yMax -= screenMargin;
        var size = _rect.rect.size;
        float minX = area.xMin + size.x * _rect.pivot.x;
        float maxX = area.xMax - size.x * (1f - _rect.pivot.x);
        float minY = area.yMin + size.y * _rect.pivot.y;
        float maxY = area.yMax - size.y * (1f - _rect.pivot.y);

        local.x = minX > maxX ? local.x : Mathf.Clamp(local.x, minX, maxX);
        local.y = minY > maxY ? local.y : Mathf.Clamp(local.y, minY, maxY);
        return local;
    }

    private Transform ResolvePlayer()
    {
        if (player != null) return player;

        var controller = FindFirstObjectByType<PlayerController>();
        if (controller != null)
            player = controller.transform;
        return player;
    }

    private static Camera ActiveCamera()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled) return main;

        foreach (var cam in Camera.allCameras)
        {
            if (cam.isActiveAndEnabled && cam.orthographic)
                return cam;
        }
        return main;
    }

    private static Bounds BoundsOf(Transform target)
    {
        bool found = false;
        var bounds = new Bounds(target.position, Vector3.zero);

        foreach (var renderer in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }
}