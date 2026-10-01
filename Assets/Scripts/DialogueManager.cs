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
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private TMP_Text speakerText;
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

    [Header("Typing Blips")]
    [SerializeField] private string defaultBlipKey = "NPCblip";
    [SerializeField] [Min(1)] private int blipEveryNCharacters = 2;

    public bool IsActive => _dialogue != null;
    public event Action<DialogueData> DialogueStarted;
    public event Action<DialogueData> DialogueEnded;

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
    private int _openedFrame = -1;

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

        panel.SetActive(false);
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
        _typing = StartCoroutine(TypeLine(line.text));
    }

    private void ShowSpeaker(CharacterProfile speaker)
    {
        _lineSpeaker = speaker;
        _lineSide = speaker == null || speaker == _defaultSpeaker ? _defaultSide : Opposite(_defaultSide);

        bool hasName = speaker != null && !string.IsNullOrEmpty(speaker.displayName);
        speakerText.gameObject.SetActive(hasName);
        if (hasName)
        {
            speakerText.text = speaker.displayName;
            speakerText.color = speaker.nameColor;
        }

        SetPortrait(null);
    }

    private void SetPortrait(string expression)
    {
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

                    while (wait > 0f)
                    {
                        wait -= Time.unscaledDeltaTime;
                        yield return null;
                    }
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

        if (IsLastLine && ChoicesDueAfterSequence)
        {
            QueueFinishFlag(_dialogue);
            ShowChoices(_dialogue.choices);
        }
        else
            SetIndicatorVisible(true);
    }

    private void Advance()
    {
        if (!IsActive || Time.frameCount == _openedFrame) return;

        if (_typing != null)
        {
            FinishTyping();
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
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = choice.text;

            var captured = choice;
            button.onClick.AddListener(() => Choose(captured));
            _choiceButtons.Add(button);
        }

        if (_choiceButtons.Count > 0)
            Select(_choiceButtons[0].gameObject);
    }

    private void Choose(DialogueChoice choice)
    {
        if (!IsActive) return;

        TelemetryManager.Log("dialogue_choice", _dialogue != null ? _dialogue.name : null,
        _dialogue != null ? _dialogue.choices.IndexOf(choice) : -1, choice.text);

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

        continueButton.gameObject.SetActive(true);
    }

    private void EndDialogue()
    {
        if (!IsActive) return;

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

        UIPanelAnimator.SetVisible(panel, false);

        onComplete?.Invoke();
        DialogueEnded?.Invoke(started);

        foreach (var action in actions)
            action.Execute();
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