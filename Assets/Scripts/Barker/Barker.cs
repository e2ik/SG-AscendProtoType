using System.Collections.Generic;
using UnityEngine;

public class Barker : MonoBehaviour
{
    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private float offsetY = 1f;

    [Header("Random Barks")]
    [SerializeField] private bool autoBark = true;
    [SerializeField] [TextArea(1, 3)] private List<string> lines = new List<string>();
    [SerializeField] private Vector2 intervalRange = new Vector2(6f, 12f);
    [SerializeField] [Range(0f, 1f)] private float barkChance = 0.7f;
    [SerializeField] private bool avoidRepeats = true;
    [SerializeField] private string fallbackPlayerName = "friend";

    [Header("Conditions")]
    [SerializeField] [Min(0f)] private float maxDistanceToPlayer;
    [SerializeField] private bool onlyWhenOnScreen = true;
    [SerializeField] private bool skipDuringDialogue = true;
    [SerializeField] private bool skipWhenUIOpen = true;
    [SerializeField] private bool skipWhenHighlighted = true;
    [SerializeField] private bool closeWhenHighlighted = true;

    public bool IsSpeaking => _current != null;

    private SpeechBubble _current;
    private float _nextBarkTime;
    private int _lastLine = -1;
    private Transform _player;
    private Renderer _renderer;
    private PlayerInteractor _interactor;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable() => ScheduleNext();

    private void OnDisable()
    {
        if (_current != null)
            _current.Close();
        _current = null;
    }

    public SpeechBubble Say(string message, float duration = -1f)
    {
        if (_current != null)
            _current.Close();

        _current = SpeechBubble.Spawn(bubblePrefab, transform, FillTokens(message), offsetY, duration);
        return _current;
    }

    public SpeechBubble SayRandom()
    {
        if (lines == null || lines.Count == 0) return null;

        int index = Random.Range(0, lines.Count);
        if (avoidRepeats && lines.Count > 1 && index == _lastLine)
            index = (index + Random.Range(1, lines.Count)) % lines.Count;

        _lastLine = index;
        return Say(lines[index]);
    }

    public void StopSpeaking()
    {
        if (_current != null)
            _current.Close();
        _current = null;
    }

    private void Update()
    {
        if (_current != null && (_current.IsClosing || _current.Source != transform))
            _current = null;

        if (_current != null && closeWhenHighlighted && IsHighlighted())
            StopSpeaking();

        if (!autoBark || _current != null || Time.time < _nextBarkTime) return;

        ScheduleNext();

        if (Random.value > barkChance || !CanBark()) return;
        SayRandom();
    }

    public static string PlayerName
    {
        get
        {
            var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            return save != null && save.character != null ? save.character.characterName : null;
        }
    }

    private string FillTokens(string message)
    {
        if (string.IsNullOrEmpty(message) || message.IndexOf('{') < 0) return message;

        string name = PlayerName;
        if (string.IsNullOrWhiteSpace(name)) name = fallbackPlayerName;
        return message.Replace("{name}", name).Replace("{player}", name);
    }

    private bool IsHighlighted()
    {
        if (_interactor == null)
            _interactor = FindFirstObjectByType<PlayerInteractor>();
        if (_interactor == null) return false;

        if (_interactor.Current is not Component component || component == null) return false;
        var target = component.transform;
        return target == transform || target.IsChildOf(transform) || transform.IsChildOf(target);
    }

    private void ScheduleNext()
    {
        float low = Mathf.Min(intervalRange.x, intervalRange.y);
        float high = Mathf.Max(intervalRange.x, intervalRange.y);
        _nextBarkTime = Time.time + Random.Range(low, high);
    }

    private bool CanBark()
    {
        if (skipDuringDialogue && DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return false;
        if (skipWhenUIOpen && UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return false;
        if (onlyWhenOnScreen && _renderer != null && !_renderer.isVisible) return false;
        if (skipWhenHighlighted && IsHighlighted()) return false;

        if (maxDistanceToPlayer > 0f)
        {
            if (_player == null)
            {
                var controller = FindFirstObjectByType<PlayerController>();
                _player = controller != null ? controller.transform : null;
            }

            if (_player == null || _player == transform) return _player == transform;
            if (Vector2.Distance(_player.position, transform.position) > maxDistanceToPlayer) return false;
        }

        return true;
    }
}