using System.Collections;
using UnityEngine;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Transform scaleRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] [Min(0f)] private float charactersPerSecond = 40f;
    [SerializeField] [Min(0f)] private float baseDuration = 1.5f;
    [SerializeField] [Min(0f)] private float durationPerCharacter = 0.05f;

    [Header("Pop")]
    [SerializeField] [Min(0.01f)] private float popInDuration = 0.12f;
    [SerializeField] [Min(0.01f)] private float popOutDuration = 0.1f;
    [SerializeField] [Min(1f)] private float popOvershoot = 1.15f;

    [Header("Draw Order")]
    [SerializeField] private bool textOnTop = true;
    [SerializeField] private bool sortAboveSource = true;
    [SerializeField] private int sortingOffset = 10;

    [Header("Shove")]
    [SerializeField] private RectTransform shoveTarget;
    [SerializeField] [Min(0f)] private float shovesPerSecond = 8f;
    [SerializeField] private Vector2 shoveAmount = new Vector2(1f, 1f);
    [SerializeField] [Min(0f)] private float shoveRotation = 2f;
    [SerializeField] [Min(0f)] private float textShoveAmount = 1f;

    [Header("Follow")]
    [SerializeField] private bool snapToPixels = true;
    [SerializeField] [Min(0f)] private float pixelsPerUnit = 16f;
    [SerializeField] private bool hideDuringDialogue = true;

    public Transform Source { get; private set; }
    public bool IsClosing { get; private set; }

    private Collider2D _collider;
    private Renderer _renderer;
    private float _offsetY;
    private Vector3 _baseScale = Vector3.one;
    private Coroutine _routine;
    private Vector3 _shoveBasePosition;
    private Quaternion _shoveBaseRotation;
    private float _shoveTimer;
    private Vector2 _shoveOffset;
    private float _shoveAngle;
    private int _textSeed;

    private void Awake()
    {
        if (scaleRoot == null) scaleRoot = transform;
        if (text == null) text = GetComponentInChildren<TMP_Text>(true);
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        _baseScale = scaleRoot.localScale;

        if (textOnTop && text != null)
            RaiseText();

        if (shoveTarget == null)
        {
            if (scaleRoot != transform && scaleRoot is RectTransform scaleRect)
                shoveTarget = scaleRect;
            else if (transform.childCount > 0)
                shoveTarget = transform.GetChild(0) as RectTransform;
        }

        if (shoveTarget != null)
        {
            _shoveBasePosition = shoveTarget.localPosition;
            _shoveBaseRotation = shoveTarget.localRotation;
        }
    }

    private void SortAbove(Transform source)
    {
        var bubbleCanvas = GetComponentInChildren<Canvas>(true);
        if (bubbleCanvas == null) return;

        bool found = false;
        int layer = bubbleCanvas.sortingLayerID;
        int highest = int.MinValue;

        foreach (var spriteRenderer in source.GetComponentsInChildren<Renderer>())
        {
            if (!found || SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID) > SortingLayer.GetLayerValueFromID(layer))
            {
                layer = spriteRenderer.sortingLayerID;
                highest = spriteRenderer.sortingOrder;
                found = true;
            }
            else if (spriteRenderer.sortingLayerID == layer)
            {
                highest = Mathf.Max(highest, spriteRenderer.sortingOrder);
            }
        }

        var sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas != null)
        {
            if (sourceCanvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                Debug.LogWarning($"'{source.name}' is on a Screen Space - Overlay canvas, which always draws over world-space speech bubbles - use Screen Space - Camera instead", this);

            if (!found || SortingLayer.GetLayerValueFromID(sourceCanvas.sortingLayerID) >= SortingLayer.GetLayerValueFromID(layer))
            {
                if (!found || sourceCanvas.sortingLayerID != layer)
                    highest = sourceCanvas.sortingOrder;
                else
                    highest = Mathf.Max(highest, sourceCanvas.sortingOrder);
                layer = sourceCanvas.sortingLayerID;
                found = true;
            }
        }

        if (!found) return;

        bubbleCanvas.sortingLayerID = layer;
        bubbleCanvas.sortingOrder = highest + sortingOffset;

        if (textOnTop && text != null)
            RaiseText();
    }

    private void RaiseText()
    {
        var parentCanvas = text.transform.parent != null ? text.transform.parent.GetComponentInParent<Canvas>() : null;
        var textCanvas = text.GetComponent<Canvas>();
        if (textCanvas == null)
            textCanvas = text.gameObject.AddComponent<Canvas>();

        textCanvas.overrideSorting = true;
        if (parentCanvas != null)
        {
            textCanvas.sortingLayerID = parentCanvas.sortingLayerID;
            textCanvas.sortingOrder = parentCanvas.sortingOrder + 1;
        }
    }

    public static SpeechBubble Spawn(SpeechBubble prefab, Transform source, string message, float offsetY, float duration = -1f)
    {
        if (prefab == null || source == null || string.IsNullOrEmpty(message)) return null;

        var bubble = Instantiate(prefab);
        bubble.Show(source, message, offsetY, duration);
        return bubble;
    }

    public void Show(Transform source, string message, float offsetY, float duration = -1f)
    {
        Source = source;
        _offsetY = offsetY;
        _collider = source.GetComponentInChildren<Collider2D>();
        _renderer = _collider == null ? source.GetComponentInChildren<Renderer>() : null;

        if (sortAboveSource)
            SortAbove(source);

        UpdatePosition();

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(Run(message, duration));
    }

    public void Close()
    {
        if (IsClosing) return;
        IsClosing = true;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PopOut());
    }

    private IEnumerator Run(string message, float duration)
    {
        IsClosing = false;
        text.text = message;
        text.maxVisibleCharacters = charactersPerSecond > 0f ? 0 : int.MaxValue;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;

        yield return Pop(0f, 1f, popInDuration, true);

        if (charactersPerSecond > 0f)
        {
            float shown = 0f;
            while (shown < total)
            {
                shown += charactersPerSecond * Time.deltaTime;
                text.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            text.maxVisibleCharacters = int.MaxValue;
        }

        float hold = duration > 0f ? duration : baseDuration + total * durationPerCharacter;
        yield return new WaitForSeconds(hold);

        IsClosing = true;
        yield return PopOut();
    }

    private IEnumerator PopOut()
    {
        yield return Pop(1f, 0f, popOutDuration, false);
        Destroy(gameObject);
    }

    private IEnumerator Pop(float from, float to, float duration, bool overshoot)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float p = t / duration;
            float s = overshoot
                ? (p < 0.7f ? Mathf.Lerp(from, popOvershoot, p / 0.7f) : Mathf.Lerp(popOvershoot, to, (p - 0.7f) / 0.3f))
                : Mathf.Lerp(from, to, p);
            scaleRoot.localScale = _baseScale * s;
            yield return null;
        }
        scaleRoot.localScale = _baseScale * to;
    }

    private void LateUpdate()
    {
        if (Source == null)
        {
            if (!IsClosing) Close();
            return;
        }

        UpdatePosition();
        UpdateShove();

        if (canvasGroup != null)
        {
            bool hidden = hideDuringDialogue && DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
            canvasGroup.alpha = hidden ? 0f : 1f;
        }
    }

    private void UpdateShove()
    {
        if (shovesPerSecond > 0f)
        {
            _shoveTimer += Time.deltaTime;
            if (_shoveTimer >= 1f / shovesPerSecond)
            {
                _shoveTimer = 0f;
                _shoveOffset = new Vector2(Mathf.Round(Random.Range(-shoveAmount.x, shoveAmount.x)), Mathf.Round(Random.Range(-shoveAmount.y, shoveAmount.y)));
                _shoveAngle = Random.Range(-shoveRotation, shoveRotation);
                _textSeed = Random.Range(0, int.MaxValue);
            }
        }

        if (shoveTarget != null)
        {
            shoveTarget.localPosition = _shoveBasePosition + (Vector3)_shoveOffset;
            shoveTarget.localRotation = _shoveBaseRotation * Quaternion.Euler(0f, 0f, _shoveAngle);
        }

        ShoveText();
    }

    private void ShoveText()
    {
        if (text == null || textShoveAmount <= 0f) return;

        text.ForceMeshUpdate();
        var info = text.textInfo;
        var random = new System.Random(_textSeed);

        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            float dx = (float)Mathf.Round((float)(random.NextDouble() * 2.0 - 1.0) * textShoveAmount);
            float dy = (float)Mathf.Round((float)(random.NextDouble() * 2.0 - 1.0) * textShoveAmount);
            if (!character.isVisible) continue;

            var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            var offset = new Vector3(dx, dy, 0f);
            int v = character.vertexIndex;
            vertices[v] += offset;
            vertices[v + 1] += offset;
            vertices[v + 2] += offset;
            vertices[v + 3] += offset;
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private void UpdatePosition()
    {
        if (Source == null) return;

        Vector3 centre;
        if (_collider != null && _collider.enabled)
            centre = _collider.bounds.center;
        else if (_renderer != null)
            centre = _renderer.bounds.center;
        else
            centre = Source.position;

        var position = new Vector3(centre.x, centre.y + _offsetY, transform.position.z);

        if (snapToPixels && pixelsPerUnit > 0f)
        {
            position.x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
            position.y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position = position;
    }
}