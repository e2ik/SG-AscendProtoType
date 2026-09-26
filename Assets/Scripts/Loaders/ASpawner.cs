using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class ASpawner : MonoBehaviour
{
    public static ASpawner Instance { get; private set; }

    [Serializable]
    public class AudioMapping
    {
        public string key;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 0.5f)] public float pitchVariation;
        public bool loop;
        [Min(1)] public int maxInstances = 4;
        public bool stealOldest = true;
        [Min(0f)] public float cooldown;
        [Range(0, 256)] public int priority = 128;
        public AudioMixerGroup mixerGroup;

        public bool LooksUninitialized => pitch <= 0f;

        public void ApplyDefaults()
        {
            if (volume <= 0f) volume = 1f;
            pitch = 1f;
            if (maxInstances < 1) maxInstances = 4;
            stealOldest = true;
            if (priority == 0) priority = 128;
        }
    }

    private class Voice
    {
        public AudioSource Source;
        public AudioMapping Mapping;
        public float StartTime;
    }

    [SerializeField] private List<AudioMapping> database = new List<AudioMapping>();
    [SerializeField] [Min(1)] private int maxVoices = 24;
    [SerializeField] [Min(0)] private int initialPoolSize = 8;
    [SerializeField] private AudioMixerGroup defaultMixerGroup;

    private readonly Dictionary<string, AudioMapping> _mappings = new Dictionary<string, AudioMapping>();
    private readonly Dictionary<string, float> _lastPlayTime = new Dictionary<string, float>();
    private readonly Queue<AudioSource> _free = new Queue<AudioSource>();
    private readonly List<Voice> _active = new List<Voice>();
    private int _createdCount;

    public int ActiveVoiceCount => _active.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (var entry in database)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;

            if (_mappings.ContainsKey(entry.key))
                Debug.LogWarning($"ASpawner has duplicate key '{entry.key}' - only the first is used", this);
            else
                _mappings[entry.key] = entry;
        }

        for (int i = 0; i < initialPoolSize; i++)
            _free.Enqueue(CreateSource());
    }

    private void OnValidate()
    {
        foreach (var entry in database)
        {
            if (entry != null && entry.LooksUninitialized)
                entry.ApplyDefaults();
        }
    }

    private void Update()
    {
        if (AudioListener.pause) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var voice = _active[i];
            if (voice.Source == null)
                _active.RemoveAt(i);
            else if (!voice.Source.isPlaying)
                Release(i);
        }
    }

    public static AudioSource Play(string key, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"Cannot play '{key}' - no ASpawner in the scene");
            return null;
        }

        return Instance.PlayInternal(key, volumeScale, pitchScale);
    }

    public static void Stop(AudioSource source)
    {
        if (Instance == null || source == null) return;

        int index = Instance._active.FindIndex(v => v.Source == source);
        if (index >= 0)
            Instance.Release(index);
    }

    public static void StopAll(string key)
    {
        if (Instance == null) return;

        for (int i = Instance._active.Count - 1; i >= 0; i--)
        {
            if (Instance._active[i].Mapping.key == key)
                Instance.Release(i);
        }
    }

    private AudioSource PlayInternal(string key, float volumeScale, float pitchScale)
    {
        if (!_mappings.TryGetValue(key, out var mapping) || mapping.clips == null || mapping.clips.Length == 0)
        {
            Debug.LogWarning($"Audio key '{key}' not found or has no clips", this);
            return null;
        }

        float now = Time.unscaledTime;
        if (mapping.cooldown > 0f && _lastPlayTime.TryGetValue(key, out float lastPlayed) && now - lastPlayed < mapping.cooldown)
            return null;

        if (CountActive(mapping) >= Mathf.Max(1, mapping.maxInstances))
        {
            if (!mapping.stealOldest) return null;
            Release(FindOldest(mapping));
        }

        if (_active.Count >= maxVoices)
        {
            int victim = FindStealCandidate(mapping.priority);
            if (victim < 0) return null;
            Release(victim);
        }

        var clip = mapping.clips[UnityEngine.Random.Range(0, mapping.clips.Length)];
        if (clip == null) return null;

        var source = GetFreeSource();

        source.clip = clip;
        source.volume = mapping.volume * Mathf.Clamp01(volumeScale);
        float pitch = mapping.pitch + UnityEngine.Random.Range(-mapping.pitchVariation, mapping.pitchVariation);
        source.pitch = Mathf.Max(0.01f, pitch * Mathf.Max(0.01f, pitchScale));
        source.loop = mapping.loop;
        source.priority = mapping.priority;
        source.outputAudioMixerGroup = mapping.mixerGroup != null ? mapping.mixerGroup : defaultMixerGroup;

        source.gameObject.SetActive(true);
        source.Play();

        _active.Add(new Voice { Source = source, Mapping = mapping, StartTime = now });
        _lastPlayTime[key] = now;
        return source;
    }

    private int CountActive(AudioMapping mapping)
    {
        int count = 0;
        foreach (var voice in _active)
        {
            if (voice.Mapping == mapping)
                count++;
        }
        return count;
    }

    private int FindOldest(AudioMapping mapping)
    {
        int oldest = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].Mapping != mapping) continue;
            if (oldest < 0 || _active[i].StartTime < _active[oldest].StartTime)
                oldest = i;
        }
        return oldest;
    }

    private int FindStealCandidate(int newPriority)
    {
        int candidate = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            var voice = _active[i];
            if (voice.Mapping.priority < newPriority) continue;

            if (candidate < 0)
            {
                candidate = i;
                continue;
            }

            var best = _active[candidate];
            bool lessImportant = voice.Mapping.priority > best.Mapping.priority;
            bool sameButOlder = voice.Mapping.priority == best.Mapping.priority && voice.StartTime < best.StartTime;
            if (lessImportant || sameButOlder)
                candidate = i;
        }
        return candidate;
    }

    private void Release(int index)
    {
        if (index < 0 || index >= _active.Count) return;

        var source = _active[index].Source;
        _active.RemoveAt(index);

        if (source == null) return;

        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);
        _free.Enqueue(source);
    }

    private AudioSource GetFreeSource()
    {
        while (_free.Count > 0)
        {
            var source = _free.Dequeue();
            if (source != null) return source;
        }
        return CreateSource();
    }

    private AudioSource CreateSource()
    {
        var go = new GameObject($"Audio_{_createdCount++}");
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        go.SetActive(false);
        return source;
    }
}