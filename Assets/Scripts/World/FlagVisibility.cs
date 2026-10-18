using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlagVisibility : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string showWhenFlag;
    [SerializeField] private string hideWhenFlag;

    [Header("Fade")]
    [SerializeField] [Min(0f)] private float fadeOutDuration = 0.8f;
    [SerializeField] [Min(0f)] private float fadeInDuration = 0.6f;
    [SerializeField] [Min(0f)] private float fadeDelay;
    [SerializeField] private bool disableCollidersWhileFading = true;
    [SerializeField] private string hideEffect;
    [SerializeField] private string showEffect;

    private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    private readonly List<float> _spriteAlphas = new List<float>();
    private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
    private readonly List<float> _groupAlphas = new List<float>();
    private readonly List<Collider2D> _colliders = new List<Collider2D>();
    private readonly List<bool> _colliderStates = new List<bool>();

    private readonly List<Behaviour> _softBehaviours = new List<Behaviour>();
    private readonly List<bool> _softBehaviourStates = new List<bool>();
    private readonly List<Renderer> _softRenderers = new List<Renderer>();
    private readonly List<bool> _softRendererStates = new List<bool>();

    private Coroutine _fade;
    private bool _cached;
    private bool _visible;
    private bool _started;
    private bool _softVisible = true;

    private bool SoftMode => target != null && (target == gameObject || transform.IsChildOf(target.transform));

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
        if (_started) Refresh(true);
    }

    private void Start()
    {
        _started = true;
        Refresh(true);
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;

        if (_fade != null)
        {
            StopCoroutine(_fade);
            _fade = null;
            if (target != null)
            {
                SetAlpha(1f);
                RestoreColliders();
                SetTargetActive(_visible);
            }
        }
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (key == showWhenFlag || key == hideWhenFlag)
            Refresh(false);
    }

    private bool ShouldShow()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        bool shown = string.IsNullOrEmpty(showWhenFlag) || (save != null && save.GetFlag(showWhenFlag));
        bool hidden = !string.IsNullOrEmpty(hideWhenFlag) && save != null && save.GetFlag(hideWhenFlag);
        return shown && !hidden;
    }

    private void Refresh(bool instant)
    {
        if (target == null) return;
        CacheParts();

        bool show = ShouldShow();
        bool wasVisible = _fade != null ? _visible : TargetVisible();

        if (_fade != null)
        {
            StopCoroutine(_fade);
            _fade = null;
        }

        _visible = show;

        if (instant || !isActiveAndEnabled || show == wasVisible)
        {
            SetAlpha(1f);
            RestoreColliders();
            SetTargetActive(show);
            return;
        }

        _fade = StartCoroutine(show ? FadeIn() : FadeOut());
    }

    private void CacheParts()
    {
        if (_cached || target == null) return;
        _cached = true;

        foreach (var sprite in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            _sprites.Add(sprite);
            _spriteAlphas.Add(sprite.color.a);
        }

        foreach (var group in target.GetComponentsInChildren<CanvasGroup>(true))
        {
            _groups.Add(group);
            _groupAlphas.Add(group.alpha);
        }

        foreach (var collider in target.GetComponentsInChildren<Collider2D>(true))
        {
            _colliders.Add(collider);
            _colliderStates.Add(collider.enabled);
        }
    }

    private IEnumerator FadeOut()
    {
        if (disableCollidersWhileFading)
            SetColliders(false);

        var barker = target.GetComponentInChildren<Barker>();
        if (barker != null)
            barker.StopSpeaking();

        if (fadeDelay > 0f)
            yield return new WaitForSeconds(fadeDelay);

        PlayEffect(hideEffect);

        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            SetAlpha(1f - t / fadeOutDuration);
            yield return null;
        }

        SetAlpha(1f);
        RestoreColliders();
        SetTargetActive(false);
        _fade = null;
    }

    private IEnumerator FadeIn()
    {
        if (fadeDelay > 0f)
            yield return new WaitForSeconds(fadeDelay);

        SetAlpha(0f);
        if (disableCollidersWhileFading)
            SetColliders(false);
        SetTargetActive(true);
        if (disableCollidersWhileFading)
            SetColliders(false);
        PlayEffect(showEffect);

        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetAlpha(t / fadeInDuration);
            yield return null;
        }

        SetAlpha(1f);
        RestoreColliders();
        _fade = null;
    }

    private bool TargetVisible() => SoftMode ? _softVisible : target.activeSelf;

    private void SetTargetActive(bool active)
    {
        if (!SoftMode)
        {
            target.SetActive(active);
            return;
        }

        if (!target.activeSelf) target.SetActive(true);
        if (active == _softVisible) return;
        _softVisible = active;

        if (!active)
        {
            _softBehaviours.Clear();
            _softBehaviourStates.Clear();
            _softRenderers.Clear();
            _softRendererStates.Clear();

            foreach (var behaviour in target.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || behaviour == this) continue;
                _softBehaviours.Add(behaviour);
                _softBehaviourStates.Add(behaviour.enabled);
                behaviour.enabled = false;
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                _softRenderers.Add(renderer);
                _softRendererStates.Add(renderer.enabled);
                renderer.enabled = false;
            }
            return;
        }

        for (int i = 0; i < _softBehaviours.Count; i++)
        {
            if (_softBehaviours[i] != null)
                _softBehaviours[i].enabled = _softBehaviourStates[i];
        }

        for (int i = 0; i < _softRenderers.Count; i++)
        {
            if (_softRenderers[i] != null)
                _softRenderers[i].enabled = _softRendererStates[i];
        }
    }

    private void SetAlpha(float multiplier)
    {
        for (int i = 0; i < _sprites.Count; i++)
        {
            if (_sprites[i] == null) continue;
            var colour = _sprites[i].color;
            colour.a = _spriteAlphas[i] * multiplier;
            _sprites[i].color = colour;
        }

        for (int i = 0; i < _groups.Count; i++)
        {
            if (_groups[i] != null)
                _groups[i].alpha = _groupAlphas[i] * multiplier;
        }
    }

    private void SetColliders(bool enabled)
    {
        foreach (var collider in _colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private void RestoreColliders()
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = _colliderStates[i];
        }
    }

    private void PlayEffect(string key)
    {
        if (!string.IsNullOrEmpty(key) && target != null)
            PSpawner.Spawn(key, target.transform.position);
    }
}