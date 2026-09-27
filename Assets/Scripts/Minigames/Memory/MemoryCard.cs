using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MemoryCard : MonoBehaviour
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

    public int PairId { get; private set; }
    public CardState State { get; private set; }
    public Button Button { get; private set; }

    public event Action<MemoryCard> Clicked;

    private SelectionOutline _outline;
    private Coroutine _flip;

    private void Awake()
    {
        Button = GetComponent<Button>();
        _outline = GetComponent<SelectionOutline>();
        Button.onClick.AddListener(() => Clicked?.Invoke(this));
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
        SetChosen(false);
    }

    public void Reveal()
    {
        if (State != CardState.Hidden) return;
        State = CardState.Revealed;
        SetChosen(true);
        Flip(true);
    }

    public void Conceal()
    {
        if (State != CardState.Revealed) return;
        State = CardState.Hidden;
        SetChosen(false);
        Flip(false);
    }

    public void MarkMatched()
    {
        State = CardState.Matched;
        SetChosen(false);

        if (matchedOverlay != null)
            matchedOverlay.SetActive(true);
    }

    private void SetChosen(bool chosen)
    {
        if (_outline != null)
            _outline.SetChosen(chosen);
    }

    private void Flip(bool faceUp)
    {
        if (_flip != null)
            StopCoroutine(_flip);

        if (!isActiveAndEnabled || flipDuration <= 0f)
        {
            ShowFace(faceUp);
            return;
        }

        _flip = StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool faceUp)
    {
        float half = flipDuration * 0.5f;

        yield return ScaleX(1f, 0f, half);
        ShowFace(faceUp);
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

    private void ShowFace(bool faceUp)
    {
        if (front != null) front.SetActive(faceUp);
        if (back != null) back.SetActive(!faceUp);
    }
}