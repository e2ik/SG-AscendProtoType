using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class TutorialEventTrigger
{
    public string eventKey;
    public TutorialSequence sequence;
    public List<TutorialSequence> requiredSequences = new List<TutorialSequence>();
    public bool waitIfBusy = true;
}

public class TutorialDirector : MonoBehaviour
{
    private const string EnabledPrefKey = "TutorialsEnabled";
    private static readonly Regex ActionToken = new Regex(@"\{([^{}\s]+/[^{}\s]+)\}");

    public static TutorialDirector Instance { get; private set; }

    [SerializeField] private TutorialPromptUI ui;
    [SerializeField] private List<TutorialEventTrigger> eventTriggers = new List<TutorialEventTrigger>();
    [SerializeField] private string continueActionPath = "Player/Jump";
    [SerializeField] private string keyFormat = "<b>[{0}]</b>";
    [SerializeField] [Min(0f)] private float successDuration = 1.2f;
    [SerializeField] private bool debugEvents;

    public bool TutorialsEnabled { get; private set; } = true;
    public bool IsRunning => _active != null;
    public TutorialSequence ActiveSequence => _active;

    private readonly List<TutorialSequence> _queue = new List<TutorialSequence>();
    private readonly HashSet<string> _sessionCompleted = new HashSet<string>();
    private TutorialSequence _active;
    private TutorialStep _step;
    private TutorialStepEvaluator _evaluator;
    private Coroutine _routine;
    private MovementController _frozen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        TutorialsEnabled = PlayerPrefs.GetInt(EnabledPrefKey, 1) == 1;
    }

    private void OnEnable()
    {
        TutorialEvents.OnRaised += HandleEvent;
    }

    private void OnDisable()
    {
        TutorialEvents.OnRaised -= HandleEvent;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded += HandleDialogueEnded;
        }

        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged += HandleDeviceChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded -= HandleDialogueEnded;
        }

        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged -= HandleDeviceChanged;

        Cleanup();
    }

    private void Update()
    {
        if (_active != null || _queue.Count == 0 || !TutorialsEnabled) return;
        if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return;

        var next = _queue[0];
        _queue.RemoveAt(0);

        if (next != null && (next.CanRepeat || !IsCompleted(next)))
            Begin(next);
    }

    public void SetTutorialsEnabled(bool enabled)
    {
        TutorialsEnabled = enabled;
        PlayerPrefs.SetInt(EnabledPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled)
        {
            _queue.Clear();
            Abort();
        }
    }

    public bool TryStart(TutorialSequence sequence, IList<TutorialSequence> requiredSequences = null, bool waitIfBusy = true)
    {
        if (!TutorialsEnabled || sequence == null) return false;

        if (!sequence.CanRepeat && IsCompleted(sequence))
        {
            Log($"'{sequence.Id}' skipped - already completed");
            return false;
        }

        if (requiredSequences != null)
        {
            foreach (var required in requiredSequences)
            {
                if (required != null && !IsCompleted(required))
                {
                    Log($"'{sequence.Id}' skipped - requires '{required.Id}' first");
                    return false;
                }
            }
        }

        if (_active == sequence || _queue.Contains(sequence)) return false;

        if (_active != null)
        {
            if (!waitIfBusy)
            {
                Log($"'{sequence.Id}' skipped - '{_active.Id}' is running");
                return false;
            }

            _queue.Add(sequence);
            Log($"'{sequence.Id}' queued behind '{_active.Id}'");
            return true;
        }

        Begin(sequence);
        return true;
    }

    public void Abort()
    {
        if (_active == null) return;
        Log($"'{_active.Id}' aborted");
        Cleanup();
    }

    public bool IsCompleted(TutorialSequence sequence)
    {
        if (sequence == null) return false;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null ? save.completedTutorials.Contains(sequence.Id) : _sessionCompleted.Contains(sequence.Id);
    }

    [ContextMenu("Reset Tutorial Progress")]
    public void ResetProgress()
    {
        _sessionCompleted.Clear();
        _queue.Clear();
        Abort();

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null) return;

        save.completedTutorials.Clear();
        SaveManager.Instance.SaveCurrent();
    }

    private void Begin(TutorialSequence sequence)
    {
        _active = sequence;
        Log($"'{sequence.Id}' started");
        _routine = StartCoroutine(Run(sequence));
    }

    private IEnumerator Run(TutorialSequence sequence)
    {
        foreach (var step in sequence.Steps)
        {
            if (step != null)
                yield return RunStep(step);
        }

        MarkCompleted(sequence);
        Log($"'{sequence.Id}' completed");
        _routine = null;
        Cleanup();
    }

    private IEnumerator RunStep(TutorialStep step)
    {
        _step = step;
        _evaluator = TutorialStepEvaluator.Create(step, continueActionPath);
        _evaluator.Begin();

        SetFrozen(step.freezePlayer);
        RefreshPromptText();

        float shown = 0f;
        while (!_evaluator.IsComplete || shown < step.minDisplayTime)
        {
            bool blocked = IsBlocked(step);
            if (ui != null)
                ui.SetPromptVisible(!blocked && !string.IsNullOrEmpty(step.prompt));

            if (!blocked)
            {
                _evaluator.Tick(Time.unscaledDeltaTime);
                shown += Time.unscaledDeltaTime;
            }

            yield return null;
        }

        EndStep();

        if (ui == null || string.IsNullOrEmpty(step.successText) || successDuration <= 0f) yield break;

        ui.SetSuccessText(FormatPrompt(step.successText));

        float elapsed = 0f;
        while (elapsed < successDuration)
        {
            bool blocked = IsBlocked(step);
            ui.SetSuccessVisible(!blocked);
            if (!blocked)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ui.SetSuccessVisible(false);
    }

    private void EndStep()
    {
        _evaluator?.End();
        _evaluator = null;
        _step = null;
        SetFrozen(false);

        if (ui != null)
            ui.SetPromptVisible(false);
    }

    private void Cleanup()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        EndStep();
        _active = null;

        if (ui != null)
            ui.HideAll();
    }

    private void MarkCompleted(TutorialSequence sequence)
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null)
        {
            _sessionCompleted.Add(sequence.Id);
            return;
        }

        if (!save.completedTutorials.Contains(sequence.Id))
            save.completedTutorials.Add(sequence.Id);

        SaveManager.Instance.SaveCurrent();
    }

    private static bool IsBlocked(TutorialStep step)
    {
        var game = GameManager.Instance;
        if (game != null && game.IsTransitioning) return true;
        if (step.allowMenus) return false;

        bool menuOpen = UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen;
        bool talking = DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
        return menuOpen || talking;
    }

    private void SetFrozen(bool freeze)
    {
        if (!freeze)
        {
            if (_frozen != null)
                _frozen.SetInputEnabled(true);
            _frozen = null;
            return;
        }

        if (_frozen != null) return;

        var stats = PlayerStatsController.Active;
        var player = stats != null ? stats.GetComponent<PlayerController>() : null;
        if (player == null || player.Movement == null) return;

        _frozen = player.Movement;
        _frozen.SetInputEnabled(false);
    }

    private void RefreshPromptText()
    {
        if (ui != null && _step != null)
            ui.SetPromptText(FormatPrompt(_step.prompt));
    }

    private string FormatPrompt(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return ActionToken.Replace(text, match => string.Format(keyFormat, BindingLabel(match.Groups[1].Value)));
    }

    private static string BindingLabel(string path)
    {
        var input = InputManager.Instance;
        var action = input != null ? input.FindAction(path) : null;
        if (action == null) return path;

        bool gamepad = input.ActiveDevice == InputDeviceType.Gamepad;
        var bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.isPartOfComposite) continue;

            string devicePath = binding.isComposite
                ? (i + 1 < bindings.Count ? bindings[i + 1].effectivePath : null)
                : binding.effectivePath;

            if (MatchesDevice(devicePath, gamepad))
                return action.GetBindingDisplayString(i);
        }

        return action.GetBindingDisplayString();
    }

    private static bool MatchesDevice(string path, bool gamepad)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return gamepad
            ? path.StartsWith("<Gamepad>")
            : path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
    }

    private void HandleEvent(string key)
    {
        Log($"event '{key}'");
        if (!TutorialsEnabled) return;

        foreach (var trigger in eventTriggers)
        {
            if (trigger == null || trigger.sequence == null || trigger.eventKey != key) continue;
            TryStart(trigger.sequence, trigger.requiredSequences, trigger.waitIfBusy);
        }
    }

    private void HandleStateChanged(GameState previous, GameState next)
    {
        _queue.Clear();
        Abort();
    }

    private void HandleDialogueStarted(DialogueData dialogue) =>
        TutorialEvents.Raise(TutorialEvents.DialogueStarted, dialogue != null ? dialogue.name : null);

    private void HandleDialogueEnded(DialogueData dialogue) =>
        TutorialEvents.Raise(TutorialEvents.DialogueFinished, dialogue != null ? dialogue.name : null);

    private void HandleDeviceChanged(InputDeviceType device) => RefreshPromptText();

    private void Log(string message)
    {
        if (debugEvents)
            Debug.Log($"[Tutorial] {message}", this);
    }
}
