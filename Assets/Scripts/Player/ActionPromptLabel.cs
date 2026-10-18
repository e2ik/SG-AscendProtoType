using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class ActionPromptLabel : MonoBehaviour
{
    [System.Serializable]
    private class StateLabel
    {
        public GameState state;
        public string text;
        public InputActionReference action;
    }

    [SerializeField] private TMP_Text label;
    [SerializeField] private KeyPrompt prompt;
    [SerializeField] private string defaultText = "JUMP";
    [SerializeField] private List<StateLabel> stateLabels = new List<StateLabel>
    {
        new StateLabel { state = GameState.Minigame_Memory, text = "FLIP" },
        new StateLabel { state = GameState.Minigame_Rhythm, text = "BREATHE IN" }
    };

    [Header("Breathing")]
    [SerializeField] private string breatheInText = "BREATHE IN";
    [SerializeField] private string breatheOutText = "BREATHE OUT";

    [Header("Bounce On Change")]
    [SerializeField] [Min(1f)] private float bounceScale = 1.15f;
    [SerializeField] [Min(0.01f)] private float bounceDuration = 0.2f;

    private InputActionReference _defaultAction;
    private string _defaultPart;
    private InputActionReference _currentAction;
    private string _currentText;
    private Vector3 _baseScale = Vector3.one;
    private Coroutine _bounce;

    private void Awake()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
        if (prompt == null)
            prompt = GetComponentInChildren<KeyPrompt>(true);

        if (prompt != null)
        {
            _defaultAction = prompt.Action;
            _defaultPart = prompt.CompositePart;
            _currentAction = _defaultAction;
        }

        if (label != null)
            _baseScale = label.rectTransform.localScale;
    }

    private void OnDisable()
    {
        if (_bounce != null)
        {
            StopCoroutine(_bounce);
            _bounce = null;
        }
        if (label != null)
            label.rectTransform.localScale = _baseScale;
    }

    private void Update()
    {
        var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.World;

        string text = defaultText;
        InputActionReference action = null;

        foreach (var entry in stateLabels)
        {
            if (entry.state != state) continue;
            if (!string.IsNullOrEmpty(entry.text)) text = entry.text;
            action = entry.action;
            break;
        }

        if (state == GameState.Minigame_Rhythm && MinigameBase.Current is RhythmGame rhythm)
            text = rhythm.IsHolding ? breatheOutText : breatheInText;

        ApplyText(text);
        ApplyAction(action != null ? action : _defaultAction, action != null ? null : _defaultPart);
    }

    private void ApplyText(string text)
    {
        if (label == null || text == _currentText) return;

        bool first = _currentText == null;
        _currentText = text;
        label.text = text;

        if (first || !isActiveAndEnabled) return;
        if (_bounce != null) StopCoroutine(_bounce);
        _bounce = StartCoroutine(Bounce());
    }

    private void ApplyAction(InputActionReference action, string part)
    {
        if (prompt == null || action == _currentAction) return;
        _currentAction = action;
        prompt.SetAction(action, part);
    }

    private IEnumerator Bounce()
    {
        var rect = label.rectTransform;
        for (float t = 0f; t < bounceDuration; t += Time.unscaledDeltaTime)
        {
            float p = t / bounceDuration;
            float scale = p < 0.4f
                ? Mathf.Lerp(1f, bounceScale, p / 0.4f)
                : Mathf.Lerp(bounceScale, 1f, (p - 0.4f) / 0.6f);
            rect.localScale = _baseScale * scale;
            yield return null;
        }
        rect.localScale = _baseScale;
        _bounce = null;
    }
}
