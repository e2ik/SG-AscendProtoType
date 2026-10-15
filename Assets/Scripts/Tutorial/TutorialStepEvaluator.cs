using UnityEngine;
using UnityEngine.InputSystem;

public abstract class TutorialStepEvaluator
{
    public bool IsComplete { get; private set; }

    public virtual void Begin() { }
    public virtual void Tick(float deltaTime) { }
    public virtual void End() { }

    protected void Complete() => IsComplete = true;

    public static TutorialStepEvaluator Create(TutorialStep step, string continueActionPath)
    {
        switch (step.condition)
        {
            case TutorialStepCondition.Continue:
                return new InputActionStepEvaluator(string.IsNullOrEmpty(step.actionPath) ? continueActionPath : step.actionPath, 1);
            case TutorialStepCondition.Timed:
                return new TimedStepEvaluator(step.duration);
            case TutorialStepCondition.InputAction:
                return new InputActionStepEvaluator(step.actionPath, step.requiredCount);
            case TutorialStepCondition.GameEvent:
                return new GameEventStepEvaluator(step.eventKey, step.requiredCount);
            case TutorialStepCondition.Flag:
                return new FlagStepEvaluator(step.flag);
            default:
                return new TimedStepEvaluator(0f);
        }
    }
}

public class TimedStepEvaluator : TutorialStepEvaluator
{
    private readonly float _duration;
    private float _elapsed;

    public TimedStepEvaluator(float duration) => _duration = duration;

    public override void Tick(float deltaTime)
    {
        _elapsed += deltaTime;
        if (_elapsed >= _duration)
            Complete();
    }
}

public class InputActionStepEvaluator : TutorialStepEvaluator
{
    private readonly string _path;
    private readonly int _required;
    private InputAction _action;
    private int _count;

    public InputActionStepEvaluator(string path, int required)
    {
        _path = path;
        _required = Mathf.Max(1, required);
    }

    public override void Begin()
    {
        _action = InputManager.Instance != null && !string.IsNullOrEmpty(_path) ? InputManager.Instance.FindAction(_path) : null;
        if (_action == null)
        {
            Debug.LogWarning($"Tutorial step waits for input '{_path}' but it was not found - skipping the step");
            Complete();
            return;
        }

        _action.performed += OnPerformed;
    }

    public override void End()
    {
        if (_action != null)
            _action.performed -= OnPerformed;
        _action = null;
    }

    private void OnPerformed(InputAction.CallbackContext ctx)
    {
        _count++;
        if (_count >= _required)
            Complete();
    }
}

public class GameEventStepEvaluator : TutorialStepEvaluator
{
    private readonly string _key;
    private readonly int _required;
    private int _count;

    public GameEventStepEvaluator(string key, int required)
    {
        _key = key;
        _required = Mathf.Max(1, required);
    }

    public override void Begin()
    {
        if (string.IsNullOrEmpty(_key))
        {
            Debug.LogWarning("Tutorial step waits for a game event but has no event key - skipping the step");
            Complete();
            return;
        }

        TutorialEvents.OnRaised += OnRaised;
    }

    public override void End() => TutorialEvents.OnRaised -= OnRaised;

    private void OnRaised(string key)
    {
        if (key != _key) return;

        _count++;
        if (_count >= _required)
            Complete();
    }
}

public class FlagStepEvaluator : TutorialStepEvaluator
{
    private readonly string _flag;

    public FlagStepEvaluator(string flag) => _flag = flag;

    public override void Begin()
    {
        if (string.IsNullOrEmpty(_flag))
        {
            Debug.LogWarning("Tutorial step waits for a flag but has no flag name - skipping the step");
            Complete();
            return;
        }

        Tick(0f);
    }

    public override void Tick(float deltaTime)
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && save.GetFlag(_flag))
            Complete();
    }
}
