using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcScheduler : MonoBehaviour
{
    [Serializable]
    public class Stage
    {
        public string requiredFlag;
        public string hideWhenFlag;
        public GameObject npc;
    }

    [Serializable]
    public class Character
    {
        public string name;
        public string goneWhenFlag;
        public List<Stage> stages = new List<Stage>();

        [NonSerialized] public GameObject shown;
        [NonSerialized] public Coroutine routine;
        [NonSerialized] public bool initialised;
    }

    private class Parts
    {
        public readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        public readonly List<float> spriteAlphas = new List<float>();
        public readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        public readonly List<float> groupAlphas = new List<float>();
        public readonly List<Collider2D> colliders = new List<Collider2D>();
        public readonly List<bool> colliderStates = new List<bool>();
    }

    [SerializeField] private List<Character> characters = new List<Character>();

    [Header("Transition")]
    [SerializeField] [Min(0f)] private float fadeOutDuration = 0.8f;
    [SerializeField] [Min(0f)] private float fadeInDuration = 0.6f;
    [SerializeField] [Min(0f)] private float gapBetween = 0.15f;
    [SerializeField] private bool crossfade;
    [SerializeField] private string hideEffect;
    [SerializeField] private string showEffect;
    [SerializeField] private string hideSound;
    [SerializeField] private string showSound;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    private readonly Dictionary<GameObject, Parts> _parts = new Dictionary<GameObject, Parts>();
    private bool _started;

    private void Awake()
    {
        foreach (var character in characters)
        {
            foreach (var stage in character.stages)
            {
                if (stage.npc == null) continue;
                if (stage.npc == gameObject || transform.IsChildOf(stage.npc.transform))
                    Debug.LogWarning($"NpcScheduler is inside '{stage.npc.name}', so hiding that NPC would also switch the scheduler off - put the scheduler on its own object", this);
                Cache(stage.npc);
            }
        }
    }

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
        if (_started) ApplyAll(true);
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;
        foreach (var character in characters)
        {
            if (character.routine == null) continue;
            StopCoroutine(character.routine);
            character.routine = null;
            Snap(character, character.shown);
        }
    }

    private void Start()
    {
        _started = true;
        ApplyAll(true);
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (!_started) return;
        foreach (var character in characters)
        {
            if (Uses(character, key))
                Apply(character, false);
        }
    }

    private static bool Uses(Character character, string key)
    {
        if (character.goneWhenFlag == key) return true;
        foreach (var stage in character.stages)
        {
            if (stage.requiredFlag == key || stage.hideWhenFlag == key)
                return true;
        }
        return false;
    }

    public void ApplyAll(bool instant)
    {
        foreach (var character in characters)
            Apply(character, instant);
    }

    private void Apply(Character character, bool instant)
    {
        var target = Resolve(character);

        if (character.routine != null)
        {
            StopCoroutine(character.routine);
            character.routine = null;
            Snap(character, character.shown);
        }

        if (debugLog)
            Debug.Log($"[NpcScheduler] {Label(character)} -> {(target != null ? target.name : "hidden")}{(instant ? " (instant)" : "")}", this);

        if (instant || !character.initialised || !isActiveAndEnabled || target == character.shown)
        {
            character.initialised = true;
            character.shown = target;
            Snap(character, target);
            return;
        }

        var from = character.shown;
        character.shown = target;
        character.routine = StartCoroutine(Transition(character, from, target));
    }

    private static GameObject Resolve(Character character)
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        if (Has(save, character.goneWhenFlag)) return null;

        GameObject result = null;
        foreach (var stage in character.stages)
        {
            if (stage.npc == null) continue;
            bool required = string.IsNullOrEmpty(stage.requiredFlag) || Has(save, stage.requiredFlag);
            bool hidden = Has(save, stage.hideWhenFlag);
            if (required && !hidden)
                result = stage.npc;
        }
        return result;
    }

    private static bool Has(SaveData save, string flag) =>
        save != null && !string.IsNullOrEmpty(flag) && save.GetFlag(flag);

    private void Snap(Character character, GameObject visible)
    {
        foreach (var stage in character.stages)
        {
            if (stage.npc == null) continue;
            SetAlpha(stage.npc, 1f);
            RestoreColliders(stage.npc);
        }

        foreach (var stage in character.stages)
        {
            if (stage.npc != null && stage.npc != visible)
                stage.npc.SetActive(false);
        }

        if (visible != null)
            visible.SetActive(true);
    }

    private IEnumerator Transition(Character character, GameObject from, GameObject to)
    {
        if (crossfade && from != null && to != null)
        {
            BeginHide(from);
            BeginShow(to);
            float duration = Mathf.Max(fadeOutDuration, fadeInDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetAlpha(from, fadeOutDuration > 0f ? 1f - Mathf.Clamp01(t / fadeOutDuration) : 0f);
                SetAlpha(to, fadeInDuration > 0f ? Mathf.Clamp01(t / fadeInDuration) : 1f);
                yield return null;
            }
            EndHide(from);
            EndShow(to);
            character.routine = null;
            yield break;
        }

        if (from != null)
        {
            BeginHide(from);
            for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
            {
                SetAlpha(from, 1f - t / fadeOutDuration);
                yield return null;
            }
            EndHide(from);

            if (to != null && gapBetween > 0f)
                yield return new WaitForSeconds(gapBetween);
        }

        if (to != null)
        {
            BeginShow(to);
            for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
            {
                SetAlpha(to, t / fadeInDuration);
                yield return null;
            }
            EndShow(to);
        }

        character.routine = null;
    }

    private void BeginHide(GameObject npc)
    {
        SetColliders(npc, false);
        var barker = npc.GetComponentInChildren<Barker>();
        if (barker != null) barker.StopSpeaking();
        PlayEffect(hideEffect, npc);
        PlaySound(hideSound);
    }

    private void EndHide(GameObject npc)
    {
        npc.SetActive(false);
        SetAlpha(npc, 1f);
        RestoreColliders(npc);
    }

    private void BeginShow(GameObject npc)
    {
        SetAlpha(npc, 0f);
        npc.SetActive(true);
        SetColliders(npc, false);
        PlayEffect(showEffect, npc);
        PlaySound(showSound);
    }

    private void EndShow(GameObject npc)
    {
        SetAlpha(npc, 1f);
        RestoreColliders(npc);
    }

    private Parts Cache(GameObject npc)
    {
        if (_parts.TryGetValue(npc, out var parts)) return parts;

        parts = new Parts();
        foreach (var sprite in npc.GetComponentsInChildren<SpriteRenderer>(true))
        {
            parts.sprites.Add(sprite);
            parts.spriteAlphas.Add(sprite.color.a);
        }
        foreach (var group in npc.GetComponentsInChildren<CanvasGroup>(true))
        {
            parts.groups.Add(group);
            parts.groupAlphas.Add(group.alpha);
        }
        foreach (var collider in npc.GetComponentsInChildren<Collider2D>(true))
        {
            parts.colliders.Add(collider);
            parts.colliderStates.Add(collider.enabled);
        }

        _parts[npc] = parts;
        return parts;
    }

    private void SetAlpha(GameObject npc, float multiplier)
    {
        var parts = Cache(npc);
        for (int i = 0; i < parts.sprites.Count; i++)
        {
            if (parts.sprites[i] == null) continue;
            var colour = parts.sprites[i].color;
            colour.a = parts.spriteAlphas[i] * multiplier;
            parts.sprites[i].color = colour;
        }
        for (int i = 0; i < parts.groups.Count; i++)
        {
            if (parts.groups[i] != null)
                parts.groups[i].alpha = parts.groupAlphas[i] * multiplier;
        }
    }

    private void SetColliders(GameObject npc, bool enabled)
    {
        foreach (var collider in Cache(npc).colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private void RestoreColliders(GameObject npc)
    {
        var parts = Cache(npc);
        for (int i = 0; i < parts.colliders.Count; i++)
        {
            if (parts.colliders[i] != null)
                parts.colliders[i].enabled = parts.colliderStates[i];
        }
    }

    private static void PlayEffect(string key, GameObject npc)
    {
        if (!string.IsNullOrEmpty(key))
            PSpawner.Spawn(key, npc.transform.position);
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }

    private static string Label(Character character) =>
        string.IsNullOrEmpty(character.name) ? "(unnamed)" : character.name;

    [ContextMenu("Log Current Stages")]
    private void LogStages()
    {
        foreach (var character in characters)
        {
            var target = Resolve(character);
            Debug.Log($"[NpcScheduler] {Label(character)}: {(target != null ? target.name : "hidden")}", this);
        }
    }

    private void OnValidate()
    {
        var seen = new Dictionary<GameObject, string>();
        foreach (var character in characters)
        {
            foreach (var stage in character.stages)
            {
                if (stage.npc == null) continue;
                if (seen.TryGetValue(stage.npc, out var owner) && owner != character.name)
                    Debug.LogWarning($"NpcScheduler: '{stage.npc.name}' is listed under both '{owner}' and '{character.name}' - each NPC object should belong to one character", this);
                else
                    seen[stage.npc] = character.name;

                if (stage.npc.GetComponent<FlagVisibility>() != null)
                    Debug.LogWarning($"NpcScheduler: '{stage.npc.name}' also has a FlagVisibility - remove it so they don't fight", stage.npc);
            }
        }
    }
}
