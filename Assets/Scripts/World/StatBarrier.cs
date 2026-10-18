using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StatBarrier : MonoBehaviour, IInteractable
{
    [Header("Requirement")]
    [SerializeField] private StatType stat = StatType.Strength;
    [SerializeField][Min(1)] private int requiredValue = 5;
    [SerializeField] private string openedFlag = "gate_tier2_open";

    [Header("Prompt")]
    [SerializeField] private string interactPrompt = "Push";

    [Header("Opening")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, 3f);
    [SerializeField] private float openDuration = 1.2f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool fadeOutWhenOpen = true;
    [SerializeField] [Min(0f)] private float fadeDelay = 0.1f;
    [SerializeField] [Min(0f)] private float fadeDuration = 0.6f;

    [Header("Too Weak")]
    [SerializeField] private Transform visual;
    [SerializeField] private float wobbleDistance = 0.06f;
    [SerializeField] private float wobbleDuration = 0.3f;

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Sounds")]
    [SerializeField] private string strainSound;
    [SerializeField] private string openSound;
    [SerializeField] private string openedSound;
    [SerializeField] private string fadeSound;
    [SerializeField] private string lockedSound;

    [Header("Effects")]
    [SerializeField] private string strainEffect;
    [SerializeField] private string openEffect;
    [SerializeField] private string openedEffect;
    [SerializeField] private string fadeEffect;
    [SerializeField] private string lockedEffect;
    [SerializeField] private Vector2 effectOffset;

    [Header("Locked Bump")]
    [SerializeField] private bool barkOnBump = true;
    [SerializeField] [Min(0f)] private float bumpDistance = 0.05f;
    [SerializeField] [Min(0f)] private float bumpCooldown = 4f;
    [SerializeField] [TextArea(1, 2)] private List<string> bumpLines = new List<string>
    {
        "Something's blocking the way.",
        "Maybe I can push it?",
        "A gate... looks heavy."
    };

    [Header("Barks")]
    [SerializeField] private string statDisplayName;
    [SerializeField] [TextArea(1, 2)] private List<string> tooWeakLines = new List<string>
    {
        "It won't budge. I need more {stat}.",
        "Too heavy... I need {value} {stat}.",
        "Ngh! Not enough {stat} yet."
    };
    [SerializeField] [TextArea(1, 2)] private List<string> openLines = new List<string>
    {
        "There we go!",
        "It moved!",
        "Yes! It's open."
    };
    [SerializeField] [Range(0f, 1f)] private float openBarkChance = 0.6f;
    [SerializeField] [Min(0f)] private float barkDuration = 1.4f;
    [SerializeField] [Min(0.01f)] private float barkScale = 1f;

    private int _lastWeakLine = -1;
    private float _nextBumpTime;
    private PlayerController _player;
    private Collider2D _playerCollider;

    public bool IsOpen { get; private set; }

    public string InteractionPrompt => interactPrompt;
    public bool CanInteract => !IsOpen && !_wobbling;

    private Collider2D _collider;
    private Vector3 _closedPosition;
    private bool _wobbling;
    private Vector3 _visualOrigin;
    private SpriteRenderer[] _fadeRenderers;
    private float[] _fadeAlphas;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _closedPosition = transform.position;
        if (visual == null) visual = transform;
        _visualOrigin = visual.localPosition;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _fadeRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _fadeAlphas = new float[_fadeRenderers.Length];
        for (int i = 0; i < _fadeRenderers.Length; i++)
            _fadeAlphas[i] = _fadeRenderers[i].color.a;
        SetSprite(false);
    }

    private void OnDisable()
    {
        if (_wobbling)
        {
            visual.localPosition = _visualOrigin;
            _wobbling = false;
        }

        if (!IsOpen) return;

        transform.position = _closedPosition + (Vector3)openOffset;
        RestoreAlphas();
        if (fadeOutWhenOpen)
            SetRenderersVisible(false);
    }

    private void RestoreAlphas()
    {
        for (int i = 0; i < _fadeRenderers.Length; i++)
        {
            if (_fadeRenderers[i] == null) continue;
            var colour = _fadeRenderers[i].color;
            colour.a = _fadeAlphas[i];
            _fadeRenderers[i].color = colour;
        }
    }

    private static Collider2D BodyCollider(PlayerController player)
    {
        Collider2D fallback = null;
        foreach (var candidate in player.GetComponentsInChildren<Collider2D>())
        {
            if (!candidate.enabled) continue;
            if (!candidate.isTrigger) return candidate;
            if (fallback == null) fallback = candidate;
        }
        return fallback;
    }

    private void Update()
    {
        if (IsOpen || !barkOnBump || Time.time < _nextBumpTime) return;

        if (_playerCollider == null)
        {
            _player = FindFirstObjectByType<PlayerController>();
            _playerCollider = _player != null ? BodyCollider(_player) : null;
            if (_playerCollider == null) return;
        }

        var distance = Physics2D.Distance(_playerCollider, _collider);
        if (!distance.isValid || distance.distance > bumpDistance) return;

        _nextBumpTime = Time.time + bumpCooldown;
        PlaySound(lockedSound);
        PlayEffect(lockedEffect);
        if (bumpLines.Count > 0)
            Bark(_player, bumpLines[Random.Range(0, bumpLines.Count)], 0);
    }

    private void SetSprite(bool open)
    {
        if (spriteRenderer == null) return;
        var sprite = open ? openSprite : closedSprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }

    private void PlayEffect(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        Vector3 position = _collider != null && _collider.enabled ? _collider.bounds.center : transform.position;
        PSpawner.Spawn(key, position + (Vector3)effectOffset);
    }

    private void Start()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null || string.IsNullOrEmpty(openedFlag) || !save.GetFlag(openedFlag)) return;

        IsOpen = true;
        _collider.enabled = false;
        transform.position = _closedPosition + (Vector3)openOffset;
        SetSprite(true);
        if (fadeOutWhenOpen)
            SetRenderersVisible(false);
    }

    public void Interact(PlayerController interactor)
    {
        if (!CanInteract || interactor == null) return;

        int current = interactor.Stats != null ? interactor.Stats.Get(stat) : PlayerStats.StartingValue;
        if (current >= requiredValue)
        {
            Open(current);
            if (openLines.Count > 0 && Random.value <= openBarkChance)
                Bark(interactor, openLines[Random.Range(0, openLines.Count)], current);
        }
        else
        {
            Strain(current);
            if (tooWeakLines.Count > 0)
            {
                int index = Random.Range(0, tooWeakLines.Count);
                if (tooWeakLines.Count > 1 && index == _lastWeakLine)
                    index = (index + Random.Range(1, tooWeakLines.Count)) % tooWeakLines.Count;
                _lastWeakLine = index;
                Bark(interactor, tooWeakLines[index], current);
            }
        }
    }

    private void Bark(PlayerController interactor, string line, int current)
    {
        if (interactor == null) return;
        var barker = interactor.GetComponentInChildren<Barker>();
        if (barker == null || string.IsNullOrEmpty(line)) return;

        string statName = string.IsNullOrEmpty(statDisplayName) ? stat.ToString() : statDisplayName;
        string text = line.Replace("{stat}", statName)
            .Replace("{value}", requiredValue.ToString())
            .Replace("{current}", current.ToString());
        barker.Say(text, barkDuration, barkScale);
    }

    private void Open(int current)
    {
        IsOpen = true;
        _collider.enabled = false;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(openedFlag))
        {
            save.SetFlag(openedFlag, true);
            SaveManager.Instance.SaveCurrent();
        }

        TelemetryManager.Log("barrier_open", openedFlag, current, $"{stat} {requiredValue}");
        PlaySound(openSound);
        PlayEffect(openEffect);
        SetSprite(true);
        StartCoroutine(Slide());
    }

    private void Strain(int current)
    {
        TelemetryManager.Log("barrier_blocked", openedFlag, current, $"{stat} {requiredValue}");
        PlaySound(strainSound);
        PlayEffect(strainEffect);
        _nextBumpTime = Time.time + bumpCooldown;
        StartCoroutine(Wobble());
    }

    private IEnumerator Slide()
    {
        Vector3 from = transform.position;
        Vector3 to = _closedPosition + (Vector3)openOffset;

        for (float t = 0f; t < openDuration; t += Time.deltaTime)
        {
            transform.position = Vector3.LerpUnclamped(from, to, openCurve.Evaluate(t / openDuration));
            yield return null;
        }

        transform.position = to;
        PlaySound(openedSound);
        PlayEffect(openedEffect);

        if (!fadeOutWhenOpen) yield break;

        if (fadeDelay > 0f)
            yield return new WaitForSeconds(fadeDelay);

        PlaySound(fadeSound);
        PlayEffect(fadeEffect);

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float k = 1f - t / fadeDuration;
            for (int i = 0; i < _fadeRenderers.Length; i++)
            {
                if (_fadeRenderers[i] == null) continue;
                var colour = _fadeRenderers[i].color;
                colour.a = _fadeAlphas[i] * k;
                _fadeRenderers[i].color = colour;
            }
            yield return null;
        }

        SetRenderersVisible(false);
        RestoreAlphas();
    }

    private void SetRenderersVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    private IEnumerator Wobble()
    {
        _wobbling = true;
        Vector3 origin = _visualOrigin;

        for (float t = 0f; t < wobbleDuration; t += Time.deltaTime)
        {
            float damping = 1f - t / wobbleDuration;
            visual.localPosition = origin + Vector3.right * (Mathf.Sin(t * 60f) * wobbleDistance * damping);
            yield return null;
        }

        visual.localPosition = origin;
        _wobbling = false;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }
}