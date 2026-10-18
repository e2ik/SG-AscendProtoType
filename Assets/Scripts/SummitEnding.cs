using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SummitEnding : MonoBehaviour
{
    public const string DefaultCompleteFlag = "journey_complete";

    [SerializeField] private string triggerFlag = "summit_reached";
    [SerializeField] private string completeFlag = DefaultCompleteFlag;
    [SerializeField] private bool freezePlayer = true;

    [Header("Timing")]
    [SerializeField] [Min(0f)] private float startDelay = 0.8f;
    [SerializeField] [Min(0f)] private float gapBetweenLines = 0.3f;
    [SerializeField] [Min(0f)] private float minHold = 1f;
    [SerializeField] [Min(0f)] private float holdPerCharacter = 0.04f;
    [SerializeField] [Min(0f)] private float holdBeforeFade = 1.2f;
    [SerializeField] [Min(0.01f)] private float bubbleScale = 1.2f;

    [Header("Lines")]
    [SerializeField] [TextArea(1, 2)] private List<string> endingLines = new List<string>
    {
        "I made it... the summit.",
        "The fog is all below me now.",
        "I can finally breathe up here.",
        "One step at a time. That's all it took.",
        "I couldn't have done it alone.",
        "Time to rest. I've earned it."
    };

    [Header("Sounds")]
    [SerializeField] private string startSound;
    [SerializeField] private string fadeSound;

    private bool _running;
    private Barker _barker;
    private PlayerController _player;

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;
    }

    private void Start()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && save.GetFlag(triggerFlag) && !save.GetFlag(completeFlag))
            Begin();
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (value && key == triggerFlag)
            Begin();
    }

    private void Begin()
    {
        if (_running) return;
        _running = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (GameManager.Instance != null && GameManager.Instance.IsTransitioning)
            yield return null;

        ResolvePlayer();
        if (freezePlayer && _player != null)
            _player.FreezeMovement();

        PlaySound(startSound);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        foreach (var line in endingLines)
            yield return SayAndWait(line);

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(completeFlag))
        {
            save.SetFlag(completeFlag, true);
            SaveManager.Instance.SaveCurrent();
        }

        TelemetryManager.Log("journey_complete", triggerFlag, 0, "");

        if (holdBeforeFade > 0f)
            yield return new WaitForSeconds(holdBeforeFade);

        PlaySound(fadeSound);

        while (GameManager.Instance != null && GameManager.Instance.IsTransitioning)
            yield return null;

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToTitle();
    }

    private void ResolvePlayer()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();
        if (_barker == null && _player != null)
            _barker = _player.GetComponentInChildren<Barker>();
    }

    private IEnumerator SayAndWait(string line)
    {
        if (string.IsNullOrEmpty(line)) yield break;

        ResolvePlayer();
        if (_barker == null) yield break;

        while (Blocked())
            yield return null;

        float hold = minHold + line.Length * holdPerCharacter;
        if (_barker.Say(line, hold, bubbleScale) == null) yield break;

        yield return null;
        while (_barker != null && _barker.IsSpeaking)
            yield return null;

        if (gapBetweenLines > 0f)
            yield return new WaitForSeconds(gapBetweenLines);
    }

    private static bool Blocked()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return true;
        return DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }
}
