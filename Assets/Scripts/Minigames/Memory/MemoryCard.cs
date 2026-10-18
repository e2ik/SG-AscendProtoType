using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MemoryCard : MonoBehaviour, IPointerEnterHandler
{
    public enum CardState
    {
        Hidden,
        Revealed,
        Matched
    }

    [SerializeField] private GameObject back;
    [SerializeField] private GameObject front;
    [SerializeField] private Image faceImage;
    [SerializeField] private GameObject matchedOverlay;
    [SerializeField] private float flipDuration = 0.15f;

    [Header("Highlight")]
    [SerializeField] [Min(1f)] private float highlightScale = 1.08f;
    [SerializeField] [Min(0.01f)] private float highlightDuration = 0.12f;
    [SerializeField] [Min(1f)] private float revealedScale = 1.05f;
    [SerializeField] private bool matchedCardsStayScaled = true;

    [Header("Draw Order")]
    [SerializeField] private bool raiseWhenSelected = true;
    [SerializeField] private int selectedSortingOffset = 2;
    [SerializeField] private bool raiseWhenRevealed = true;
    [SerializeField] private int revealedSortingOffset = 1;
    [SerializeField] [Min(0f)] private float shovesPerSecond = 8f;
    [SerializeField] private Vector2 cardShoveAmount = new Vector2(1f, 1f);
    [SerializeField] [Min(0f)] private float cardShoveRotation = 1.5f;

    [Header("Shader Tint")]
    [SerializeField] private Material flashMaterial;
    [SerializeField] [Min(0.01f)] private float tintFadeDuration = 0.1f;
    [SerializeField] private Color selectOverlayColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] [Range(0f, 1f)] private float selectOverlayAmount = 0.35f;
    [SerializeField] private Color selectEdgeColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color revealOverlayColor = new Color(0.5f, 0.85f, 1f, 1f);
    [SerializeField] [Range(0f, 1f)] private float revealOverlayAmount = 0.25f;
    [SerializeField] private Color revealEdgeColor = new Color(0.4f, 0.8f, 1f, 1f);
    [SerializeField] private Color matchedOverlayColor = new Color(0.55f, 1f, 0.55f, 1f);
    [SerializeField] [Range(0f, 1f)] private float matchedOverlayAmount = 0.2f;
    [SerializeField] private Color matchedEdgeColor = new Color(0.45f, 0.95f, 0.45f, 1f);

    [Header("Reveal Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] [Min(0.01f)] private float flashDuration = 0.18f;
    [SerializeField] [Min(0)] private int flashSteps = 3;
    [SerializeField] private bool flashOnMatch = true;
    [SerializeField] private Color matchFlashColor = new Color(0.6f, 1f, 0.6f, 1f);

    [Header("Wrong Flash")]
    [SerializeField] private Color wrongFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] [Min(1)] private int wrongFlashCount = 3;
    [SerializeField] [Min(0.01f)] private float wrongFlashOnTime = 0.08f;
    [SerializeField] [Min(0.01f)] private float wrongFlashOffTime = 0.06f;
    [SerializeField] private bool wrongFlashChecker = true;

    public int PairId { get; private set; }
    public CardState State { get; private set; }
    public Button Button { get; private set; }

    public event Action<MemoryCard> Clicked;

    public bool IsFlipping => _flip != null;

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashCheckerId = Shader.PropertyToID("_FlashChecker");
    private static readonly int OverlayColorId = Shader.PropertyToID("_OverlayColor");
    private static readonly int OverlayAmountId = Shader.PropertyToID("_OverlayAmount");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeAmountId = Shader.PropertyToID("_EdgeAmount");

    private Coroutine _flip;
    private Coroutine _flash;
    private RectTransform _rect;
    private RectTransform _backRect;
    private RectTransform _frontRect;
    private Vector2 _backBasePosition;
    private Vector2 _frontBasePosition;
    private Material _flashInstance;
    private Material _frameInstance;
    private readonly List<Graphic> _flashGraphics = new List<Graphic>();
    private Color _overlayColor;
    private float _overlayAmount;
    private Color _edgeColor;
    private float _edgeAmount;

    private float _highlight;
    private float _revealedBlend;
    private Canvas _sortCanvas;
    private Canvas _parentCanvas;
    private int _appliedOffset = int.MinValue;
    private float _shoveTimer;
    private Vector2 _cardShove;
    private float _cardShoveAngle;

    private void Awake()
    {
        Button = GetComponent<Button>();
        _rect = (RectTransform)transform;
        Button.onClick.AddListener(() => Clicked?.Invoke(this));
        SetupSortCanvas();

        if (back != null) _backRect = back.transform as RectTransform;
        if (front != null) _frontRect = front.transform as RectTransform;
        if (_backRect != null) _backBasePosition = _backRect.anchoredPosition;
        if (_frontRect != null) _frontBasePosition = _frontRect.anchoredPosition;

        SetupFlash();
    }

    private void OnDestroy()
    {
        if (_flashInstance != null)
            Destroy(_flashInstance);
        if (_frameInstance != null)
            Destroy(_frameInstance);
    }

    private void OnDisable()
    {
        _highlight = 0f;
        _revealedBlend = 0f;
        ApplyHighlight();
        SetFlash(0f);
    }

    private void SetupFlash()
    {
        var template = flashMaterial;
        if (template == null)
        {
            var shader = Shader.Find("Custom/UI Flash");
            if (shader == null) return;
            template = new Material(shader);
        }

        _flashInstance = new Material(template);
        _frameInstance = new Material(template);

        foreach (var root in new[] { back, front })
        {
            if (root == null) continue;

            var frame = root.GetComponent<Image>();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is not Image) continue;
                graphic.material = graphic == frame ? _frameInstance : _flashInstance;
                _flashGraphics.Add(graphic);
            }
        }

        foreach (var material in new[] { _flashInstance, _frameInstance })
        {
            material.SetFloat(OverlayAmountId, 0f);
            material.SetFloat(EdgeAmountId, 0f);
        }

        SetFlash(0f);
    }

    public void Setup(int pairId, Sprite face, Color tint)
    {
        PairId = pairId;
        State = CardState.Hidden;

        if (faceImage != null)
        {
            faceImage.sprite = face;
            faceImage.color = tint;
        }

        if (matchedOverlay != null)
            matchedOverlay.SetActive(false);

        ShowFace(false);
        transform.localScale = Vector3.one;
        SetFlash(0f);
        _overlayAmount = 0f;
        _edgeAmount = 0f;
        ApplyTint();
    }

    public void Reveal()
    {
        if (State != CardState.Hidden) return;
        State = CardState.Revealed;
        Flip(true);
    }

    public void Conceal()
    {
        if (State != CardState.Revealed) return;
        State = CardState.Hidden;
        Flip(false);
    }

    public void MarkMatched()
    {
        State = CardState.Matched;

        if (matchedOverlay != null)
            matchedOverlay.SetActive(true);

        if (flashOnMatch)
            Flash(matchFlashColor);
    }

    public void FlashWrong()
    {
        if (_flashInstance == null || !isActiveAndEnabled) return;

        if (_flash != null)
            StopCoroutine(_flash);
        _flash = StartCoroutine(WrongFlashRoutine());
    }

    private IEnumerator WrongFlashRoutine()
    {
        SetFlashColour(wrongFlashColor, wrongFlashChecker);

        for (int i = 0; i < wrongFlashCount; i++)
        {
            SetFlash(1f);
            yield return new WaitForSeconds(wrongFlashOnTime);
            SetFlash(0f);
            yield return new WaitForSeconds(wrongFlashOffTime);
        }

        _flash = null;
    }

    private void Update()
    {
        bool highlighted = IsHighlighted();
        float target = highlighted ? 1f : 0f;
        _highlight = Mathf.MoveTowards(_highlight, target, Time.unscaledDeltaTime / highlightDuration);

        bool enlarged = FaceShowing && (State == CardState.Revealed || (matchedCardsStayScaled && State == CardState.Matched));
        _revealedBlend = Mathf.MoveTowards(_revealedBlend, enlarged ? 1f : 0f, Time.unscaledDeltaTime / highlightDuration);

        UpdateShove(highlighted);
        ApplyHighlight();
        UpdateTint();
        UpdateSorting(highlighted, enlarged);
    }

    private bool FaceShowing => front != null && front.activeSelf;

    private void SetupSortCanvas()
    {
        if (!raiseWhenSelected && !raiseWhenRevealed) return;

        _sortCanvas = GetComponent<Canvas>();
        if (_sortCanvas == null)
            _sortCanvas = gameObject.AddComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _sortCanvas.overrideSorting = false;
    }

    private void UpdateSorting(bool highlighted, bool enlarged)
    {
        if (_sortCanvas == null) return;

        int offset = 0;
        if (raiseWhenSelected && highlighted)
            offset = selectedSortingOffset;
        else if (raiseWhenRevealed && enlarged)
            offset = revealedSortingOffset;

        if (offset == _appliedOffset) return;
        _appliedOffset = offset;

        if (offset == 0)
        {
            _sortCanvas.overrideSorting = false;
            return;
        }

        if (_parentCanvas == null && transform.parent != null)
            _parentCanvas = transform.parent.GetComponentInParent<Canvas>();

        _sortCanvas.overrideSorting = true;
        if (_parentCanvas != null)
        {
            _sortCanvas.sortingLayerID = _parentCanvas.sortingLayerID;
            _sortCanvas.sortingOrder = _parentCanvas.sortingOrder + offset;
        }
        else
        {
            _sortCanvas.sortingOrder = offset;
        }
    }

    private void UpdateTint()
    {
        Color targetOverlay;
        Color targetEdge;
        float targetOverlayAmount;
        float targetEdgeAmount;

        if (State == CardState.Matched && FaceShowing)
        {
            targetOverlay = matchedOverlayColor;
            targetEdge = matchedEdgeColor;
            targetOverlayAmount = matchedOverlayAmount;
            targetEdgeAmount = 1f;
        }
        else if (State == CardState.Revealed && FaceShowing)
        {
            targetOverlay = revealOverlayColor;
            targetEdge = revealEdgeColor;
            targetOverlayAmount = revealOverlayAmount;
            targetEdgeAmount = 1f;
        }
        else
        {
            targetOverlay = selectOverlayColor;
            targetEdge = selectEdgeColor;
            targetOverlayAmount = selectOverlayAmount * _highlight;
            targetEdgeAmount = _highlight;
        }

        float step = Time.unscaledDeltaTime / tintFadeDuration;
        float colourLerp = Mathf.Clamp01(step * 1.5f);
        _overlayColor = Color.Lerp(_overlayColor, targetOverlay, colourLerp);
        _edgeColor = Color.Lerp(_edgeColor, targetEdge, colourLerp);
        _overlayAmount = Mathf.MoveTowards(_overlayAmount, targetOverlayAmount, step);
        _edgeAmount = Mathf.MoveTowards(_edgeAmount, targetEdgeAmount, step);

        ApplyTint();
    }

    private void ApplyTint()
    {
        if (_frameInstance == null) return;
        _frameInstance.SetColor(OverlayColorId, _overlayColor);
        _frameInstance.SetFloat(OverlayAmountId, _overlayAmount);
        _frameInstance.SetColor(EdgeColorId, _edgeColor);
        _frameInstance.SetFloat(EdgeAmountId, _edgeAmount);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!UIManager.HoverAllowed || Button == null || !Button.IsInteractable()) return;
        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != gameObject)
            eventSystem.SetSelectedGameObject(gameObject);
    }

    private bool IsHighlighted()
    {
        if (!Button.IsInteractable()) return false;
        var eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
    }

    private void UpdateShove(bool highlighted)
    {
        if (!highlighted || shovesPerSecond <= 0f) return;

        _shoveTimer += Time.unscaledDeltaTime;
        if (_shoveTimer < 1f / shovesPerSecond) return;
        _shoveTimer = 0f;

        _cardShove = new Vector2(UnityEngine.Random.Range(-cardShoveAmount.x, cardShoveAmount.x), UnityEngine.Random.Range(-cardShoveAmount.y, cardShoveAmount.y));
        _cardShove = new Vector2(Mathf.Round(_cardShove.x), Mathf.Round(_cardShove.y));
        _cardShoveAngle = UnityEngine.Random.Range(-cardShoveRotation, cardShoveRotation);
    }

    private void ApplyHighlight()
    {
        float eased = 1f - (1f - _highlight) * (1f - _highlight);
        float revealEased = 1f - (1f - _revealedBlend) * (1f - _revealedBlend);
        float scale = Mathf.Max(Mathf.Lerp(1f, highlightScale, eased), Mathf.Lerp(1f, revealedScale, revealEased));
        var shove = _cardShove * _highlight;
        var rotation = Quaternion.Euler(0f, 0f, _cardShoveAngle * _highlight);

        if (_backRect != null)
        {
            _backRect.localScale = new Vector3(scale, scale, 1f);
            _backRect.anchoredPosition = _backBasePosition + shove;
            _backRect.localRotation = rotation;
        }

        if (_frontRect != null)
        {
            _frontRect.localScale = new Vector3(scale, scale, 1f);
            _frontRect.anchoredPosition = _frontBasePosition + shove;
            _frontRect.localRotation = rotation;
        }
    }

    private void Flash(Color colour)
    {
        if (_flashInstance == null || !isActiveAndEnabled) return;

        if (_flash != null)
            StopCoroutine(_flash);
        _flash = StartCoroutine(FlashRoutine(colour));
    }

    private void SetFlashColour(Color colour, bool checker)
    {
        foreach (var material in new[] { _flashInstance, _frameInstance })
        {
            if (material == null) continue;
            material.SetColor(FlashColorId, colour);
            material.SetFloat(FlashCheckerId, checker ? 1f : 0f);
        }
    }

    private IEnumerator FlashRoutine(Color colour)
    {
        SetFlashColour(colour, false);

        for (float t = 0f; t < flashDuration; t += Time.deltaTime)
        {
            float p = 1f - t / flashDuration;
            if (flashSteps > 0)
                p = Mathf.Ceil(p * flashSteps) / flashSteps;
            SetFlash(p);
            yield return null;
        }

        SetFlash(0f);
        _flash = null;
    }

    private void SetFlash(float amount)
    {
        if (_flashInstance != null)
            _flashInstance.SetFloat(FlashAmountId, amount);
        if (_frameInstance != null)
            _frameInstance.SetFloat(FlashAmountId, amount);
    }

    private void Flip(bool faceUp)
    {
        if (_flip != null)
            StopCoroutine(_flip);

        if (!isActiveAndEnabled || flipDuration <= 0f)
        {
            ShowFace(faceUp);
            if (faceUp) Flash(flashColor);
            return;
        }

        _flip = StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool faceUp)
    {
        float half = flipDuration * 0.5f;

        yield return ScaleX(1f, 0f, half);
        ShowFace(faceUp);
        if (faceUp) Flash(flashColor);
        yield return ScaleX(0f, 1f, half);

        _flip = null;
    }

    private IEnumerator ScaleX(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var scale = transform.localScale;
            scale.x = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            transform.localScale = scale;
            yield return null;
        }

        var final = transform.localScale;
        final.x = to;
        transform.localScale = final;
    }

    public void ShowFaceInstant(bool faceUp) => ShowFace(faceUp);

    private void ShowFace(bool faceUp)
    {
        if (front != null) front.SetActive(faceUp);
        if (back != null) back.SetActive(!faceUp);
    }
}