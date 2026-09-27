using System.Collections;
using UnityEngine;

public enum UIPanelEffect
{
    None,
    Fade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    Pop
}

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelAnimator : MonoBehaviour
{
    [SerializeField] private UIPanelEffect showEffect = UIPanelEffect.Fade;
    [SerializeField] private UIPanelEffect hideEffect = UIPanelEffect.Fade;
    [SerializeField] [Min(0f)] private float duration = 0.2f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Slide")]
    [SerializeField] [Min(0f)] private float slideDistance;
    [SerializeField] private bool fadeWhileSliding = true;

    [Header("Pop")]
    [SerializeField] [Range(0f, 1f)] private float popScale = 0.8f;

    public bool IsHiding { get; private set; }

    private RectTransform _rect;
    private CanvasGroup _group;
    private UIPanel _panel;
    private Vector2 _restPosition;
    private Vector3 _restScale;
    private bool _captured;
    private Coroutine _routine;

    public static void SetVisible(GameObject target, bool visible)
    {
        if (target == null) return;

        var animator = target.GetComponent<UIPanelAnimator>();

        if (visible)
        {
            if (animator != null && target.activeSelf)
                animator.Show();
            else
                target.SetActive(true);
            return;
        }

        if (animator != null)
            animator.Hide();
        else
            target.SetActive(false);
    }

    private void Awake()
    {
        _rect = transform as RectTransform;
        _group = GetComponent<CanvasGroup>();
        _panel = GetComponent<UIPanel>();
        CaptureRest();
    }

    private void CaptureRest()
    {
        if (_captured || _rect == null) return;
        _restPosition = _rect.anchoredPosition;
        _restScale = _rect.localScale;
        _captured = true;
    }

    private void OnEnable()
    {
        CaptureRest();
        IsHiding = false;

        if (showEffect == UIPanelEffect.None || duration <= 0f)
        {
            ApplyRest();
            return;
        }

        Play(true);
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        IsHiding = false;
        ApplyRest();
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        if (!IsHiding) return;

        IsHiding = false;
        if (_panel != null)
            _panel.Register();

        Play(true);
    }

    public void Hide()
    {
        if (!isActiveAndEnabled || hideEffect == UIPanelEffect.None || duration <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        if (IsHiding) return;

        IsHiding = true;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        if (_panel != null)
            _panel.Unregister();

        Play(false);
    }

    private void Play(bool show)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(show));
    }

    private IEnumerator Animate(bool show)
    {
        var effect = show ? showEffect : hideEffect;

        if (show)
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            Apply(effect, show ? 1f - progress : progress);
            yield return null;
        }

        Apply(effect, show ? 0f : 1f);
        _routine = null;

        if (!show)
        {
            IsHiding = false;
            gameObject.SetActive(false);
        }
    }

    private void Apply(UIPanelEffect effect, float away)
    {
        if (_rect == null) return;

        _rect.anchoredPosition = _restPosition;
        _rect.localScale = _restScale;
        _group.alpha = 1f;

        switch (effect)
        {
            case UIPanelEffect.Fade:
                _group.alpha = 1f - away;
                break;

            case UIPanelEffect.SlideLeft:
            case UIPanelEffect.SlideRight:
            case UIPanelEffect.SlideUp:
            case UIPanelEffect.SlideDown:
                _rect.anchoredPosition = _restPosition + SlideDirection(effect) * SlideAmount(effect) * away;
                if (fadeWhileSliding)
                    _group.alpha = 1f - away;
                break;

            case UIPanelEffect.Pop:
                _rect.localScale = _restScale * Mathf.Lerp(1f, popScale, away);
                _group.alpha = 1f - away;
                break;
        }
    }

    private void ApplyRest()
    {
        if (_rect != null && _captured)
        {
            _rect.anchoredPosition = _restPosition;
            _rect.localScale = _restScale;
        }

        if (_group != null)
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
    }

    private static Vector2 SlideDirection(UIPanelEffect effect) => effect switch
    {
        UIPanelEffect.SlideLeft => Vector2.left,
        UIPanelEffect.SlideRight => Vector2.right,
        UIPanelEffect.SlideUp => Vector2.up,
        _ => Vector2.down
    };

    private float SlideAmount(UIPanelEffect effect)
    {
        if (slideDistance > 0f) return slideDistance;

        bool horizontal = effect == UIPanelEffect.SlideLeft || effect == UIPanelEffect.SlideRight;
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;

        if (canvasRect != null)
            return horizontal ? canvasRect.rect.width : canvasRect.rect.height;

        return horizontal ? _rect.rect.width : _rect.rect.height;
    }
}