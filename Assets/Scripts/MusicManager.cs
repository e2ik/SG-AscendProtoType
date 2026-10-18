using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MusicManager : MonoBehaviour
{
    [System.Serializable]
    public class Track
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool resumePosition;
    }

    [System.Serializable]
    public class StateTrack
    {
        public GameState state;
        public Track track = new Track();
    }

    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] [Min(0f)] private float crossfadeDuration = 1f;
    [SerializeField] private List<StateTrack> stateTracks = new List<StateTrack>();
    [SerializeField] private Track instructionsTrack = new Track();

    [Header("Pause")]
    [SerializeField] private bool silenceWhenPaused = true;
    [SerializeField] [Min(0f)] private float pauseFadeDuration = 0.4f;

    private readonly AudioSource[] _sources = new AudioSource[2];
    private readonly float[] _levels = new float[2];
    private readonly Coroutine[] _fades = new Coroutine[2];
    private readonly Dictionary<AudioClip, float> _savedTimes = new Dictionary<AudioClip, float>();
    private int _activeIndex;
    private Track _current;
    private Track _override;
    private MinigameBase _minigame;
    private float _pauseFactor = 1f;
    private bool _sourcesPaused;

    public AudioClip CurrentClip => _current != null ? _current.clip : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        for (int i = 0; i < _sources.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.priority = 0;
            source.volume = 0f;
            source.outputAudioMixerGroup = mixerGroup;
            _sources[i] = source;
        }
    }

    private void Update()
    {
        UpdatePause();

        var desired = _override != null ? _override : ResolveTrack();
        if (desired == _current) return;
        if (desired != null && _current != null && desired.clip == _current.clip)
        {
            _current = desired;
            return;
        }

        CrossfadeTo(desired);
    }

    private void LateUpdate()
    {
        for (int i = 0; i < _sources.Length; i++)
            _sources[i].volume = _levels[i] * _pauseFactor;
    }

    private void UpdatePause()
    {
        bool paused = silenceWhenPaused && PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused;
        float target = paused ? 0f : 1f;

        if (!paused && _sourcesPaused)
        {
            foreach (var source in _sources)
                source.UnPause();
            _sourcesPaused = false;
        }

        _pauseFactor = pauseFadeDuration > 0f
            ? Mathf.MoveTowards(_pauseFactor, target, Time.unscaledDeltaTime / pauseFadeDuration)
            : target;

        if (paused && _pauseFactor <= 0f && !_sourcesPaused)
        {
            foreach (var source in _sources)
                source.Pause();
            _sourcesPaused = true;
        }
    }

    public void PlayOverride(AudioClip clip, float volume = 1f, bool resumePosition = false)
    {
        _override = clip != null ? new Track { clip = clip, volume = volume, resumePosition = resumePosition } : null;
    }

    public void ClearOverride() => _override = null;

    public void Stop() => PlayOverride(null);

    private Track ResolveTrack()
    {
        var manager = GameManager.Instance;
        if (manager == null) return null;

        var state = manager.CurrentState;
        if (GameManager.IsMinigameState(state) && InInstructions())
            return HasClip(instructionsTrack) ? instructionsTrack : null;

        foreach (var entry in stateTracks)
        {
            if (entry.state == state)
                return HasClip(entry.track) ? entry.track : null;
        }
        return null;
    }

    private bool InInstructions()
    {
        if (_minigame == null || !_minigame.isActiveAndEnabled)
            _minigame = FindFirstObjectByType<MinigameBase>();
        if (_minigame == null) return true;

        var phase = _minigame.CurrentPhase;
        return phase == MinigameBase.Phase.Idle || phase == MinigameBase.Phase.Intro;
    }

    private static bool HasClip(Track track) => track != null && track.clip != null;

    private void CrossfadeTo(Track next)
    {
        var outgoing = _sources[_activeIndex];
        if (_current != null && _current.resumePosition && outgoing.clip == _current.clip && (outgoing.isPlaying || _sourcesPaused))
            _savedTimes[_current.clip] = outgoing.time;

        FadeSource(_activeIndex, 0f, true);

        _current = next;
        if (next == null) return;

        _activeIndex = 1 - _activeIndex;
        var incoming = _sources[_activeIndex];
        if (_fades[_activeIndex] != null)
        {
            StopCoroutine(_fades[_activeIndex]);
            _fades[_activeIndex] = null;
        }

        incoming.clip = next.clip;
        _levels[_activeIndex] = 0f;
        incoming.time = next.resumePosition && _savedTimes.TryGetValue(next.clip, out float time) && time < next.clip.length ? time : 0f;
        incoming.Play();
        if (_sourcesPaused)
            incoming.Pause();

        FadeSource(_activeIndex, next.volume, false);
    }

    private void FadeSource(int index, float target, bool stopAtEnd)
    {
        if (_fades[index] != null)
            StopCoroutine(_fades[index]);
        _fades[index] = StartCoroutine(Fade(index, target, stopAtEnd));
    }

    private IEnumerator Fade(int index, float target, bool stopAtEnd)
    {
        float start = _levels[index];

        if (crossfadeDuration > 0f)
        {
            for (float t = 0f; t < crossfadeDuration; t += Time.unscaledDeltaTime)
            {
                _levels[index] = Mathf.Lerp(start, target, t / crossfadeDuration);
                yield return null;
            }
        }

        _levels[index] = target;
        if (stopAtEnd && target <= 0f)
        {
            _sources[index].Stop();
            _sources[index].clip = null;
        }
        _fades[index] = null;
    }
}