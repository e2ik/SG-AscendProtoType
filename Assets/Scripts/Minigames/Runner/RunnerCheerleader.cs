using System.Collections.Generic;
using UnityEngine;

public class RunnerCheerleader : MonoBehaviour
{
    [SerializeField] private EndlessRunnerGame game;
    [SerializeField] private Barker barker;
    [SerializeField] private float scrollFactor = 1f;

    [Header("Cheers")]
    [SerializeField] [TextArea(1, 2)] private List<string> startCheers = new List<string>
    {
        "GO GO GO!",
        "You got this, {name}!",
        "Run, {name}, run!",
        "Show 'em how it's done!"
    };
    [SerializeField] [TextArea(1, 2)] private List<string> followUpCheers = new List<string>
    {
        "Keep going!",
        "Woo! Nice!",
        "Don't stop now!",
        "Looking good!",
        "Faster! Faster!"
    };
    [SerializeField] private Vector2 followUpInterval = new Vector2(0.9f, 1.4f);
    [SerializeField] [Min(0f)] private float cheerDuration = 0.9f;

    [Header("Despawn")]
    [SerializeField] [Min(0f)] private float offScreenMargin = 1f;

    private Vector3 _startPosition;
    private Renderer[] _renderers;
    private MinigameBase.Phase _lastPhase = MinigameBase.Phase.Idle;
    private float _nextCheerTime;
    private bool _gone;
    private int _lastFollowUp = -1;

    private void Awake()
    {
        if (game == null)
            game = FindFirstObjectByType<EndlessRunnerGame>();
        if (barker == null)
            barker = GetComponent<Barker>();

        _startPosition = transform.position;

        if (game != null)
            game.GoShown += OnGoShown;
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (game == null) return;

        var phase = game.CurrentPhase;
        if (phase != _lastPhase)
        {
            OnPhaseChanged(_lastPhase, phase);
            _lastPhase = phase;
        }

        if (_gone || phase != MinigameBase.Phase.Playing) return;

        transform.position += Vector3.left * game.Speed * scrollFactor * Time.deltaTime;

        if (Time.time >= _nextCheerTime)
        {
            Cheer(followUpCheers, ref _lastFollowUp);
            ScheduleNext();
        }

        if (IsOffScreen())
            Leave();
    }

    private void OnDestroy()
    {
        if (game != null)
            game.GoShown -= OnGoShown;
    }

    private void OnGoShown()
    {
        if (_gone) return;

        int ignored = -1;
        Cheer(startCheers, ref ignored);
    }

    private void OnPhaseChanged(MinigameBase.Phase from, MinigameBase.Phase to)
    {
        if (to == MinigameBase.Phase.Idle || to == MinigameBase.Phase.Intro || to == MinigameBase.Phase.Countdown)
            ResetCheerleader();

        if (to == MinigameBase.Phase.Playing && !_gone)
            ScheduleNext();
    }

    private void ResetCheerleader()
    {
        _gone = false;
        transform.position = _startPosition;
        SetVisible(true);
    }

    private void Cheer(List<string> lines, ref int last)
    {
        if (barker == null || lines == null || lines.Count == 0) return;

        int index = Random.Range(0, lines.Count);
        if (lines.Count > 1 && index == last)
            index = (index + Random.Range(1, lines.Count)) % lines.Count;
        last = index;

        barker.Say(lines[index], cheerDuration);
    }

    private void ScheduleNext()
    {
        float low = Mathf.Min(followUpInterval.x, followUpInterval.y);
        float high = Mathf.Max(followUpInterval.x, followUpInterval.y);
        _nextCheerTime = Time.time + Random.Range(low, high);
    }

    private bool IsOffScreen()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return false;

        float leftEdge = cam.transform.position.x - cam.orthographicSize * cam.aspect;
        float right = transform.position.x;

        foreach (var r in _renderers)
        {
            if (r != null && r.enabled)
                right = Mathf.Max(right, r.bounds.max.x);
        }

        return right < leftEdge - offScreenMargin;
    }

    private void Leave()
    {
        _gone = true;
        if (barker != null)
            barker.StopSpeaking();
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in _renderers)
        {
            if (r != null)
                r.enabled = visible;
        }
    }
}