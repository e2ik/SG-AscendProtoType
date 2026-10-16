using System;
using System.Collections.Generic;
using UnityEngine;

public class StopMotionWobble : MonoBehaviour
{
    [Serializable]
    public class EmotionWobble
    {
        public string expression = "angry";
        [Min(0f)] public float strength = 3f;
        [Min(0.1f)] public float speed = 2f;
        public bool randomOrder = true;
        public bool onlyWhileTyping;
    }

    [SerializeField] [Min(1f)] private float framesPerSecond = 8f;
    [SerializeField] [Min(1)] private int poseCount = 4;
    [SerializeField] private bool randomOrder;
    [SerializeField] private int seed;

    [Header("Rotation")]
    [SerializeField] [Min(0f)] private float maxRotation = 1.5f;

    [Header("Position")]
    [SerializeField] private Vector2 maxOffset = new Vector2(1f, 2f);
    [SerializeField] private bool snapToPixels = true;
    [SerializeField] [Min(0.01f)] private float pixelSize = 1f;

    [Header("Scale")]
    [SerializeField] [Min(0f)] private float maxScale;

    [Header("Time")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Dialogue Sync")]
    [SerializeField] private bool syncWithDialogue = true;
    [SerializeField] private bool stillWhenNotTyping = true;
    [SerializeField] [Min(0f)] private float idleStrength = 0.4f;
    [SerializeField] [Min(0.1f)] private float idleSpeed = 0.75f;
    [SerializeField] [Min(0f)] private float typingStrength = 1f;
    [SerializeField] [Min(0.1f)] private float typingSpeed = 1f;

    [Header("While Choosing")]
    [SerializeField] private bool bobWhileChoosing = true;
    [SerializeField] [Min(0f)] private float bobHeight = 3f;
    [SerializeField] [Min(0.01f)] private float bobsPerSecond = 0.8f;
    [SerializeField] [Min(1f)] private float bobFramesPerSecond = 8f;

    [Header("Emotions")]
    [SerializeField] private List<EmotionWobble> emotions = new List<EmotionWobble>
    {
        new EmotionWobble { expression = "angry", strength = 3f, speed = 2f, randomOrder = true },
        new EmotionWobble { expression = "scared", strength = 1.5f, speed = 2.5f, randomOrder = true },
        new EmotionWobble { expression = "happy", strength = 1.3f, speed = 1.3f },
        new EmotionWobble { expression = "sad", strength = 0.6f, speed = 0.5f }
    };

    private struct Pose
    {
        public float Rotation;
        public Vector2 Offset;
        public float Scale;
    }

    private readonly List<Pose> _poses = new List<Pose>();
    private RectTransform _rect;
    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private Vector3 _restScale;
    private bool _captured;
    private float _timer;
    private int _index;
    private System.Random _random;
    private DialogueManager _dialogue;
    private bool _typing;
    private bool _choosing;
    private bool _resting;
    private float _bobTime;
    private EmotionWobble _emotion;

    private void Awake()
    {
        _rect = transform as RectTransform;
        BuildPoses();
    }

    private void OnEnable()
    {
        CaptureRest();
        _timer = 0f;
        TrySubscribe();
        ApplyPose(_index);
    }

    private void OnDisable()
    {
        Unsubscribe();
        ApplyRest();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _captured)
            BuildPoses();
    }

    private void TrySubscribe()
    {
        if (!syncWithDialogue || _dialogue != null || DialogueManager.Instance == null) return;

        _dialogue = DialogueManager.Instance;
        _dialogue.TypingChanged += OnTypingChanged;
        _dialogue.ExpressionChanged += OnExpressionChanged;
        _dialogue.ChoosingChanged += OnChoosingChanged;
        _typing = _dialogue.IsTyping;
        _choosing = _dialogue.IsChoosing;
        _emotion = FindEmotion(_dialogue.CurrentExpression);
    }

    private void Unsubscribe()
    {
        if (_dialogue == null) return;

        _dialogue.TypingChanged -= OnTypingChanged;
        _dialogue.ExpressionChanged -= OnExpressionChanged;
        _dialogue.ChoosingChanged -= OnChoosingChanged;
        _dialogue = null;
    }

    private void OnTypingChanged(bool typing) => _typing = typing;

    private void OnChoosingChanged(bool choosing)
    {
        _choosing = choosing;
        _bobTime = 0f;
        _timer = 0f;
    }

    private void OnExpressionChanged(string expression) => _emotion = FindEmotion(expression);

    private EmotionWobble FindEmotion(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return null;

        foreach (var emotion in emotions)
        {
            if (emotion != null && string.Equals(emotion.expression, expression, StringComparison.OrdinalIgnoreCase))
                return emotion;
        }
        return null;
    }

    private void CaptureRest()
    {
        if (_captured) return;

        _restPosition = _rect != null ? (Vector3)_rect.anchoredPosition : transform.localPosition;
        _restRotation = transform.localRotation;
        _restScale = transform.localScale;
        _captured = true;
    }

    private void BuildPoses()
    {
        _random = new System.Random(seed != 0 ? seed : GetInstanceID());
        _poses.Clear();

        for (int i = 0; i < poseCount; i++)
        {
            _poses.Add(new Pose
            {
                Rotation = Range(-1f, 1f),
                Offset = new Vector2(Range(-1f, 1f), Range(-1f, 1f)),
                Scale = Range(-1f, 1f)
            });
        }

        _index = 0;
    }

    private float Range(float min, float max) => (float)(min + _random.NextDouble() * (max - min));

    private bool Synced => syncWithDialogue && _dialogue != null;

    private bool EmotionActive => _emotion != null && (!_emotion.onlyWhileTyping || _typing);

    private float CurrentStrength
    {
        get
        {
            if (!Synced) return 1f;
            float strength = _typing ? typingStrength : idleStrength;
            return EmotionActive ? strength * _emotion.strength : strength;
        }
    }

    private float CurrentSpeed
    {
        get
        {
            if (!Synced) return 1f;
            float speed = _typing ? typingSpeed : idleSpeed;
            return EmotionActive ? speed * _emotion.speed : speed;
        }
    }

    private bool CurrentRandomOrder => Synced && EmotionActive ? _emotion.randomOrder : randomOrder;

    private void Update()
    {
        if (syncWithDialogue && _dialogue == null)
            TrySubscribe();

        if (_poses.Count == 0) return;

        if (Synced && bobWhileChoosing && _choosing && !_typing)
        {
            Bob();
            return;
        }

        if (Synced && stillWhenNotTyping && !_typing)
        {
            if (!_resting)
            {
                ApplyRest();
                _resting = true;
                _timer = 0f;
            }
            return;
        }

        if (_resting)
        {
            _resting = false;
            _index = NextIndex();
            ApplyPose(_index);
        }

        _timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float frameTime = 1f / (framesPerSecond * CurrentSpeed);
        if (_timer < frameTime) return;

        _timer -= frameTime;
        if (_timer > frameTime)
            _timer = 0f;

        _index = NextIndex();
        ApplyPose(_index);
    }

    private void Bob()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _bobTime += delta;
        _timer += delta;

        float frameTime = 1f / bobFramesPerSecond;
        if (_timer < frameTime && !_resting) return;
        _timer = 0f;

        float y = Mathf.Sin(_bobTime * bobsPerSecond * Mathf.PI * 2f) * bobHeight;
        if (snapToPixels)
            y = Mathf.Round(y / pixelSize) * pixelSize;

        ApplyRest();
        if (_rect != null)
            _rect.anchoredPosition = (Vector2)_restPosition + new Vector2(0f, y);
        else
            transform.localPosition = _restPosition + new Vector3(0f, y, 0f);

        _resting = false;
    }

    private int NextIndex()
    {
        if (_poses.Count == 1) return 0;
        if (!CurrentRandomOrder) return (_index + 1) % _poses.Count;

        int next;
        do next = _random.Next(_poses.Count);
        while (next == _index);
        return next;
    }

    private void ApplyPose(int index)
    {
        if (!_captured || index < 0 || index >= _poses.Count) return;

        var pose = _poses[index];
        float strength = CurrentStrength;
        var offset = new Vector2(pose.Offset.x * maxOffset.x, pose.Offset.y * maxOffset.y) * strength;

        if (snapToPixels)
            offset = new Vector2(Mathf.Round(offset.x / pixelSize) * pixelSize, Mathf.Round(offset.y / pixelSize) * pixelSize);

        if (_rect != null)
            _rect.anchoredPosition = (Vector2)_restPosition + offset;
        else
            transform.localPosition = _restPosition + (Vector3)offset;

        transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, pose.Rotation * maxRotation * strength);
        transform.localScale = _restScale * (1f + pose.Scale * maxScale * strength);
    }

    private void ApplyRest()
    {
        if (!_captured) return;

        if (_rect != null)
            _rect.anchoredPosition = _restPosition;
        else
            transform.localPosition = _restPosition;

        transform.localRotation = _restRotation;
        transform.localScale = _restScale;
    }
}