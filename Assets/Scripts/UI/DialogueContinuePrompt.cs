using UnityEngine;
using TMPro;

public class DialogueContinuePrompt : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject keyPrompt;
    [SerializeField] private bool hideWhileChoosing = true;
    [SerializeField] private bool snapToPixels = true;

    [Header("While Writing")]
    [SerializeField] private float squeezeAmount = 6f;
    [SerializeField] [Min(0f)] private float squeezesPerSecond = 2f;
    [SerializeField] [Min(0f)] private float squeezeStepsPerSecond = 8f;

    [Header("Ready To Continue")]
    [SerializeField] private float waveHeight = 4f;
    [SerializeField] [Min(0.01f)] private float wavesPerSecond = 1.5f;
    [SerializeField] [Min(0f)] private float letterDelay = 0.12f;

    private CanvasGroup _promptGroup;
    private CanvasGroup _textGroup;
    private float _baseSpacing;
    private float _time;
    private float _stepTimer;
    private bool _waved;

    private void Awake()
    {
        if (promptText != null)
        {
            _baseSpacing = promptText.characterSpacing;
            _textGroup = GetOrAddGroup(promptText.gameObject);
        }

        if (keyPrompt != null)
            _promptGroup = GetOrAddGroup(keyPrompt);
    }

    private static CanvasGroup GetOrAddGroup(GameObject target)
    {
        var group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private void OnDisable()
    {
        if (promptText != null)
        {
            promptText.characterSpacing = _baseSpacing;
            promptText.ForceMeshUpdate();
        }
        _waved = false;
    }

    private void LateUpdate()
    {
        var dialogue = DialogueManager.Instance;
        if (dialogue == null) return;

        float dt = Time.unscaledDeltaTime;
        _time += dt;

        bool writing = dialogue.IsWritingLine;
        bool ready = dialogue.WaitingForContinue;
        bool hidden = hideWhileChoosing && dialogue.IsChoosing;

        SetVisible(_promptGroup, ready && !hidden);
        SetVisible(_textGroup, !hidden);

        if (promptText == null) return;

        if (writing)
        {
            UpdateSqueeze(dt);
            ClearWave();
            return;
        }

        if (!Mathf.Approximately(promptText.characterSpacing, _baseSpacing))
            promptText.characterSpacing = _baseSpacing;

        if (ready && !hidden)
            Wave();
        else
            ClearWave();
    }

    private static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    private void UpdateSqueeze(float dt)
    {
        if (squeezeStepsPerSecond > 0f)
        {
            _stepTimer += dt;
            if (_stepTimer < 1f / squeezeStepsPerSecond) return;
            _stepTimer = 0f;
        }

        float spacing = _baseSpacing + Mathf.Sin(_time * squeezesPerSecond * Mathf.PI * 2f) * squeezeAmount;
        if (snapToPixels)
            spacing = Mathf.Round(spacing);

        promptText.characterSpacing = spacing;
    }

    private void Wave()
    {
        promptText.ForceMeshUpdate();
        var info = promptText.textInfo;

        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;

            float y = Mathf.Max(0f, Mathf.Sin((_time - i * letterDelay) * wavesPerSecond * Mathf.PI * 2f)) * waveHeight;
            if (snapToPixels)
                y = Mathf.Round(y);

            var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            int start = character.vertexIndex;
            for (int v = 0; v < 4; v++)
                vertices[start + v].y += y;
        }

        promptText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        _waved = true;
    }

    private void ClearWave()
    {
        if (!_waved) return;

        promptText.ForceMeshUpdate();
        _waved = false;
    }
}
