using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public enum DialogueSide
{
    Left,
    Right
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject background;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private TMP_Text speakerText;

    [Header("Portrait Backgrounds")]
    [SerializeField] private Image portraitLeftBackground;
    [SerializeField] private Image portraitRightBackground;
    [SerializeField] [Range(0f, 1f)] private float portraitBackgroundDarken = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float portraitBackgroundAlpha = 0.7f;

    [Header("Speaker Name Box")]
    [SerializeField] private RectTransform speakerContainer;
    [SerializeField] private Image speakerBackground;
    [SerializeField] [Range(0f, 1f)] private float speakerBackgroundDarken = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float speakerNameLighten = 0.4f;
    [SerializeField] private DialogueSide speakerContainerSide = DialogueSide.Left;
    [SerializeField] private bool flipSpeakerBackground = true;
    [FormerlySerializedAs("portraitImage")]
    [SerializeField] private Image portraitLeft;
    [SerializeField] private Image portraitRight;
    [SerializeField] private bool flipRightPortrait;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TextEffectPlayer textEffects;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject continueIndicator;
    [SerializeField] private Transform choicesParent;
    [SerializeField] private Button choiceButtonPrefab;
    [SerializeField] private float charactersPerSecond = 40f;
    [SerializeField] private bool continueBeforeChoices = true;
    [SerializeField] [Min(0f)] private float choicesDelay = 0.4f;

    [Header("Typing Blips")]
    [SerializeField] private string defaultBlipKey = "NPCblip";
    [SerializeField] [Min(1)] private int blipEveryNCharacters = 2;

    public bool IsActive => _dialogue != null;
    public event Action<DialogueData> DialogueStarted;
    public event Action<DialogueData> DialogueEnded;
    public event Action<bool> TypingChanged;
    public event Action<string> ExpressionChanged;
    public event Action<bool> ChoosingChanged;

    public bool IsTyping { get; private set; }
    public bool IsChoosing { get; private set; }
    public bool IsWritingLine => IsActive && _typing != null;
    public DialogueSide SpeakerSide => _defaultSide;
    public bool WaitingForContinue => IsActive && _typing == null && _choicesDelayRoutine == null && _choiceButtons.Count == 0;
    public string CurrentExpression { get; private set; }

    private readonly List<Button> _choiceButtons = new List<Button>();
    private readonly List<DialogueAction> _pendingActions = new List<DialogueAction>();

    private DialogueData _startDialogue;
    private DialogueData _dialogue;
    private List<DialogueLine> _sequence;
    private DialogueChoice _respondingTo;
    private int _lineIndex;
    private CharacterProfile _defaultSpeaker;
    private DialogueSide _defaultSide = DialogueSide.Right;
    private CharacterProfile _lineSpeaker;
    private DialogueSide _lineSide;
    private int _nextExpression;
    private int _blipCounter;
    private Action _onComplete;
    private Coroutine _typing;
    private Coroutine _choicesDelayRoutine;
    private int _openedFrame = -1;
    private Vector2 _speakerAnchorMin;
    private Vector2 _speakerAnchorMax;
    private Vector2 _speakerPivot;
    private Vector2 _speakerPosition;
    private Vector3 _speakerScale;
    private float _speakerBackgroundAlpha = 1f;

    private bool IsLastLine => _sequence != null && _lineIndex >= _sequence.Count - 1;
    private bool ChoicesDueAfterSequence => _respondingTo == null && _dialogue.HasChoices;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (speakerContainer != null)
        {
            _speakerAnchorMin = speakerContainer.anchorMin;
            _speakerAnchorMax = speakerContainer.anchorMax;
            _speakerPivot = speakerContainer.pivot;
            _speakerPosition = speakerContainer.anchoredPosition;
            _speakerScale = speakerContainer.localScale;
        }

        if (speakerBackground == null && speakerContainer != null)
            speakerBackground = speakerContainer.GetComponent<Image>();
        if (speakerBackground != null)
            _speakerBackgroundAlpha = speakerBackground.color.a;

        panel.SetActive(false);
        if (background != null)
            background.SetActive(false);
        if (portraitLeftBackground != null)
            portraitLeftBackground.gameObject.SetActive(false);
        if (portraitRightBackground != null)
            portraitRightBackground.gameObject.SetActive(false);
        continueButton.onClick.AddListener(Advance);

        if (portraitRight != null)
        {
            var scale = portraitRight.rectTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flipRightPortrait ? -1f : 1f);
            portraitRight.rectTransform.localScale = scale;
        }

        if (textEffects == null)
            textEffects = bodyText.GetComponent<TextEffectPlayer>();
        if (textEffects == null)
            textEffects = bodyText.gameObject.AddComponent<TextEffectPlayer>();

        if (uiPanel != null)
            uiPanel.OnBack += EndDialogue;
    }

    public bool StartDialogue(DialogueData dialogue, Action onComplete = null, CharacterProfile defaultSpeaker = null,
        DialogueSide defaultSpeakerSide = DialogueSide.Right)
    {
        if (IsActive || dialogue == null || (!dialogue.HasLines && !dialogue.HasChoices)) return false;

        _startDialogue = dialogue;
        _onComplete = onComplete;
        _defaultSpeaker = defaultSpeaker;
        _defaultSide = defaultSpeakerSide;
        _pendingActions.Clear();
        _openedFrame = Time.frameCount;

        textEffects.SetText(string.Empty);
        ShowSpeaker(defaultSpeaker);
        UIPanelAnimator.SetVisible(background, true);
        UIPanelAnimator.SetVisible(panel, true);

        DialogueStarted?.Invoke(dialogue);
        PlayDialogue(dialogue);
        return true;
    }

    private void PlayDialogue(DialogueData dialogue)
    {
        _dialogue = dialogue;
        PlaySequence(dialogue.lines, null);
    }

    private void PlaySequence(List<DialogueLine> lines, DialogueChoice respondingTo)
    {
        _sequence = lines;
        _respondingTo = respondingTo;
        ShowLine(0);
    }

    private void ShowLine(int index)
    {
        CancelPendingChoices();
        ClearChoices();

        if (_sequence == null || index >= _sequence.Count)
        {
            OnSequenceFinished();
            return;
        }

        _lineIndex = index;
        var line = _sequence[index];

        ShowSpeaker(line.speaker != null ? line.speaker : _defaultSpeaker);
        SetIndicatorVisible(false);
        Select(continueButton.gameObject);

        if (_typing != null)
            StopCoroutine(_typing);
        SetTyping(true);
        _typing = StartCoroutine(TypeLine(line.text));
    }

    private void ShowSpeaker(CharacterProfile speaker)
    {
        _lineSpeaker = speaker;
        _lineSide = speaker == null || speaker == _defaultSpeaker ? _defaultSide : Opposite(_defaultSide);

        bool hasName = speaker != null && !string.IsNullOrEmpty(speaker.displayName);
        var nameRoot = speakerContainer != null ? speakerContainer.gameObject : speakerText.gameObject;
        nameRoot.SetActive(hasName);

        if (hasName)
        {
            speakerText.text = speaker.displayName;
            speakerText.color = Lighten(speaker.nameColor);

            if (speakerBackground != null)
                speakerBackground.color = Darken(speaker.nameColor);

            ApplySpeakerSide(_lineSide);
        }

        SetPortrait(null);
    }

    private Color Lighten(Color colour)
    {
        var light = Color.Lerp(colour, Color.white, speakerNameLighten);
        light.a = colour.a;
        return light;
    }

    private Color Darken(Color colour)
    {
        Color.RGBToHSV(colour, out float h, out float s, out float v);
        var dark = Color.HSVToRGB(h, s, v * (1f - speakerBackgroundDarken));
        dark.a = _speakerBackgroundAlpha;
        return dark;
    }

    private void ApplySpeakerSide(DialogueSide side)
    {
        if (speakerContainer == null) return;

        bool mirrored = side != speakerContainerSide;

        var anchorMin = _speakerAnchorMin;
        var anchorMax = _speakerAnchorMax;
        var pivot = _speakerPivot;
        var position = _speakerPosition;
        var scale = _speakerScale;

        if (mirrored)
        {
            anchorMin.x = 1f - _speakerAnchorMax.x;
            anchorMax.x = 1f - _speakerAnchorMin.x;
            position.x = -_speakerPosition.x;

            if (flipSpeakerBackground)
                scale.x = -Mathf.Abs(_speakerScale.x);
            else
                pivot.x = 1f - _speakerPivot.x;
        }

        speakerContainer.anchorMin = anchorMin;
        speakerContainer.anchorMax = anchorMax;
        speakerContainer.pivot = pivot;
        speakerContainer.anchoredPosition = position;
        speakerContainer.localScale = scale;

        float childSign = Mathf.Sign(scale.x);
        foreach (Transform child in speakerContainer)
        {
            var childScale = child.localScale;
            childScale.x = Mathf.Abs(childScale.x) * childSign;
            child.localScale = childScale;
        }
    }

    private void SetPortrait(string expression)
    {
        SetExpression(expression);

        var sprite = _lineSpeaker != null ? _lineSpeaker.GetPortrait(expression) : null;
        var active = _lineSide == DialogueSide.Left ? portraitLeft : portraitRight;
        var inactive = _lineSide == DialogueSide.Left ? portraitRight : portraitLeft;

        if (inactive != null)
            inactive.gameObject.SetActive(false);

        if (active != null)
        {
            active.sprite = sprite;
            active.gameObject.SetActive(sprite != null);
        }

        UpdatePortraitBackgrounds(sprite != null);
    }

    private void UpdatePortraitBackgrounds(bool hasPortrait)
    {
        var activeBackground = _lineSide == DialogueSide.Left ? portraitLeftBackground : portraitRightBackground;
        var inactiveBackground = _lineSide == DialogueSide.Left ? portraitRightBackground : portraitLeftBackground;

        if (inactiveBackground != null)
            UIPanelAnimator.SetVisible(inactiveBackground.gameObject, false);

        if (activeBackground == null) return;

        if (hasPortrait && _lineSpeaker != null)
        {
            Color.RGBToHSV(_lineSpeaker.nameColor, out float h, out float sat, out float v);
            var colour = Color.HSVToRGB(h, sat, v * (1f - portraitBackgroundDarken));
            colour.a = portraitBackgroundAlpha;
            activeBackground.color = colour;
        }

        UIPanelAnimator.SetVisible(activeBackground.gameObject, hasPortrait);
    }

    private void HidePortraitBackgrounds()
    {
        if (portraitLeftBackground != null)
            UIPanelAnimator.SetVisible(portraitLeftBackground.gameObject, false);
        if (portraitRightBackground != null)
            UIPanelAnimator.SetVisible(portraitRightBackground.gameObject, false);
    }

    private static DialogueSide Opposite(DialogueSide side) =>
        side == DialogueSide.Left ? DialogueSide.Right : DialogueSide.Left;

    private void ApplyExpressionsUpTo(int visibleCharacters)
    {
        var changes = textEffects.Expressions;
        while (_nextExpression < changes.Count && changes[_nextExpression].Index <= visibleCharacters)
        {
            SetPortrait(changes[_nextExpression].Expression);
            _nextExpression++;
        }
    }

    private void TryPlayBlip(int characterIndex)
    {
        if (ASpawner.Instance == null) return;

        var info = bodyText.textInfo;
        if (characterIndex < 0 || characterIndex >= info.characterCount) return;
        if (!char.IsLetterOrDigit(info.characterInfo[characterIndex].character)) return;

        if (_blipCounter++ % blipEveryNCharacters != 0) return;

        string key = _lineSpeaker != null && !string.IsNullOrEmpty(_lineSpeaker.blipKey) ? _lineSpeaker.blipKey : defaultBlipKey;
        if (string.IsNullOrEmpty(key)) return;

        float pitch = _lineSpeaker != null ? _lineSpeaker.blipPitch : 1f;
        ASpawner.Play(key, 1f, pitch);
    }

    private void OnSequenceFinished()
    {
        if (_respondingTo != null)
        {
            var next = _respondingTo.next;
            _respondingTo = null;

            if (next != null)
                PlayDialogue(next);
            else
                EndDialogue();
            return;
        }

        QueueFinishFlag(_dialogue);

        if (_dialogue.HasChoices)
            ShowChoices(_dialogue.choices);
        else
            EndDialogue();
    }

    private void QueueFinishFlag(DialogueData dialogue)
    {
        if (dialogue == null || string.IsNullOrEmpty(dialogue.setFlagOnFinish)) return;

        foreach (var pending in _pendingActions)
        {
            if (pending.type == DialogueActionType.SetFlag && pending.flag == dialogue.setFlagOnFinish)
                return;
        }

        _pendingActions.Add(new DialogueAction { type = DialogueActionType.SetFlag, flag = dialogue.setFlagOnFinish });
    }

    private IEnumerator TypeLine(string text)
    {
        Predicate<string> isExpression = _lineSpeaker != null ? _lineSpeaker.HasExpression : (Predicate<string>)null;
        textEffects.SetText(text, isExpression);
        _nextExpression = 0;
        _blipCounter = 0;

        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;

        ApplyExpressionsUpTo(0);

        if (charactersPerSecond > 0f)
        {
            var pauses = textEffects.Pauses;
            int nextPause = 0;
            int visible = 0;
            float budget = 0f;

            while (visible < total)
            {
                if (nextPause < pauses.Count && pauses[nextPause].Index <= visible)
                {
                    float wait = pauses[nextPause].Duration;
                    nextPause++;
                    budget = 0f;
                    SetTyping(false);

                    while (wait > 0f)
                    {
                        wait -= Time.unscaledDeltaTime;
                        yield return null;
                    }

                    SetTyping(true);
                    continue;
                }

                budget += Time.unscaledDeltaTime;

                while (visible < total)
                {
                    float secondsPerCharacter = 1f / (charactersPerSecond * textEffects.GetSpeedMultiplier(visible));
                    if (budget < secondsPerCharacter) break;

                    budget -= secondsPerCharacter;
                    ApplyExpressionsUpTo(visible);
                    TryPlayBlip(visible);
                    visible++;
                    bodyText.maxVisibleCharacters = visible;

                    if (nextPause < pauses.Count && pauses[nextPause].Index <= visible) break;
                }

                yield return null;
            }
        }

        _typing = null;
        FinishTyping();
    }

    private void FinishTyping()
    {
        if (_typing != null)
        {
            StopCoroutine(_typing);
            _typing = null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        ApplyExpressionsUpTo(int.MaxValue);
        SetTyping(false);

        if (IsLastLine && ChoicesDueAfterSequence && !continueBeforeChoices)
        {
            QueueFinishFlag(_dialogue);
            CancelPendingChoices();

            if (choicesDelay > 0f)
                _choicesDelayRoutine = StartCoroutine(ShowChoicesAfterDelay());
            else
                ShowChoices(_dialogue.choices);
        }
        else
            SetIndicatorVisible(true);
    }

    private IEnumerator ShowChoicesAfterDelay()
    {
        float end = Time.unscaledTime + choicesDelay;
        while (Time.unscaledTime < end)
            yield return null;

        _choicesDelayRoutine = null;
        ShowChoices(_dialogue.choices);
    }

    private void CancelPendingChoices()
    {
        if (_choicesDelayRoutine == null) return;

        StopCoroutine(_choicesDelayRoutine);
        _choicesDelayRoutine = null;
    }

    private void Advance()
    {
        if (!IsActive || Time.frameCount == _openedFrame) return;

        if (_typing != null)
        {
            FinishTyping();
            return;
        }

        if (_choicesDelayRoutine != null)
        {
            CancelPendingChoices();
            ShowChoices(_dialogue.choices);
            return;
        }

        if (_choiceButtons.Count > 0) return;

        ShowLine(_lineIndex + 1);
    }

    private void ShowChoices(List<DialogueChoice> choices)
    {
        SetIndicatorVisible(false);
        continueButton.gameObject.SetActive(false);

        foreach (var choice in choices)
        {
            var button = Instantiate(choiceButtonPrefab, choicesParent);
            var reveal = button.GetComponent<ChoiceReveal>();
            var label = reveal != null && reveal.Label != null ? reveal.Label : button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = choice.text;

            var captured = choice;
            button.onClick.AddListener(() => Choose(captured));
            _choiceButtons.Add(button);
        }

        if (choicesParent is RectTransform choicesRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesRect);

        if (_choiceButtons.Count > 0)
        {
            Select(_choiceButtons[0].gameObject);
            SetChoosing(true);
        }
    }

    private void Choose(DialogueChoice choice)
    {
        if (!IsActive) return;

        if (choice.action != null && choice.action.type != DialogueActionType.None)
            _pendingActions.Add(choice.action);

        if (choice.HasResponse)
        {
            PlaySequence(choice.responseLines, choice);
            return;
        }

        ClearChoices();

        if (choice.next != null)
            PlayDialogue(choice.next);
        else
            EndDialogue();
    }

    private void ClearChoices()
    {
        foreach (var button in _choiceButtons)
        {
            button.gameObject.SetActive(false);
            Destroy(button.gameObject);
        }
        _choiceButtons.Clear();
        SetChoosing(false);

        continueButton.gameObject.SetActive(true);
    }

    private void EndDialogue()
    {
        if (!IsActive) return;

        CancelPendingChoices();

        if (_typing != null)
        {
            StopCoroutine(_typing);
            _typing = null;
        }

        ClearChoices();

        var started = _startDialogue;
        var onComplete = _onComplete;
        var actions = new List<DialogueAction>(_pendingActions);

        _dialogue = null;
        _startDialogue = null;
        _sequence = null;
        _respondingTo = null;
        _onComplete = null;
        _defaultSpeaker = null;
        _lineSpeaker = null;
        _pendingActions.Clear();

        SetTyping(false);
        SetExpression(null);
        UIPanelAnimator.SetVisible(panel, false);
        UIPanelAnimator.SetVisible(background, false);
        HidePortraitBackgrounds();

        onComplete?.Invoke();
        DialogueEnded?.Invoke(started);

        foreach (var action in actions)
            action.Execute();
    }

    private void SetExpression(string expression)
    {
        if (CurrentExpression == expression) return;

        CurrentExpression = expression;
        ExpressionChanged?.Invoke(expression);
    }

    private void SetChoosing(bool choosing)
    {
        if (IsChoosing == choosing) return;

        IsChoosing = choosing;
        ChoosingChanged?.Invoke(choosing);
    }

    private void SetTyping(bool typing)
    {
        if (IsTyping == typing) return;

        IsTyping = typing;
        TypingChanged?.Invoke(typing);
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (continueIndicator != null)
            continueIndicator.SetActive(visible);
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null && target != null)
            EventSystem.current.SetSelectedGameObject(target);
    }
}