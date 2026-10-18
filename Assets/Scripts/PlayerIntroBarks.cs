using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerIntroBarks : MonoBehaviour
{
    [SerializeField] private Barker barker;
    [SerializeField] private string introDoneFlag = "intro_barks_done";
    [SerializeField] [Min(0f)] private float startDelay = 1f;
    [SerializeField] [Min(0f)] private float gapBetweenLines = 0.25f;
    [SerializeField] [Min(0f)] private float minHold = 0.7f;
    [SerializeField] [Min(0f)] private float holdPerCharacter = 0.03f;
    [SerializeField] [Min(0.01f)] private float bubbleScale = 1f;

    [Header("Fresh Start")]
    [SerializeField] [TextArea(1, 2)] private List<string> introLines = new List<string>
    {
        "Where... am I?",
        "How did the city sink?",
        "It's all buried in fog.",
        "I can't see a thing down here.",
        "Maybe it's clearer up top.",
        "I need to make my way up."
    };

    [Header("Loaded Save")]
    [SerializeField] [TextArea(1, 2)] private List<string> loadLines = new List<string>
    {
        "Another good day to climb.",
        "Right. Where was I?",
        "Up we go.",
        "Let's keep climbing.",
        "Still foggy. Still climbing."
    };

    private static object _greetedSave;

    private IEnumerator Start()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && ReferenceEquals(save, _greetedSave)) yield break;
        _greetedSave = save;

        bool fresh = save == null || string.IsNullOrEmpty(introDoneFlag) || !save.GetFlag(introDoneFlag);

        while (GameManager.Instance != null && GameManager.Instance.IsTransitioning)
            yield return null;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float waited = 0f;
        while (ResolveBarker() == null)
        {
            waited += Time.deltaTime;
            if (waited > 5f)
            {
                Debug.LogWarning("PlayerIntroBarks couldn't find a Barker on the spawned player - add a Barker (with a Bubble Prefab) to the world player prefab, or assign one here", this);
                yield break;
            }
            yield return null;
        }

        if (fresh)
        {
            MarkIntroDone(save);
            foreach (var line in introLines)
                yield return SayAndWait(line);
        }
        else if (loadLines.Count > 0)
        {
            yield return SayAndWait(loadLines[Random.Range(0, loadLines.Count)]);
        }
    }

    private void MarkIntroDone(SaveData save)
    {
        if (save == null || string.IsNullOrEmpty(introDoneFlag) || save.GetFlag(introDoneFlag)) return;
        save.SetFlag(introDoneFlag, true);
        SaveManager.Instance.SaveCurrent();
    }

    private Barker ResolveBarker()
    {
        if (barker != null) return barker;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            barker = player.GetComponentInChildren<Barker>();
        return barker;
    }

    private IEnumerator SayAndWait(string line)
    {
        if (string.IsNullOrEmpty(line)) yield break;

        while (Blocked())
            yield return null;

        float hold = minHold + line.Length * holdPerCharacter;
        if (barker.Say(line, hold, bubbleScale) == null)
        {
            Debug.LogWarning($"'{barker.name}' couldn't show a speech bubble - check its Barker has a Bubble Prefab assigned", barker);
            yield break;
        }

        yield return null;
        while (barker != null && barker.IsSpeaking)
            yield return null;

        if (gapBetweenLines > 0f)
            yield return new WaitForSeconds(gapBetweenLines);
    }

    private static bool Blocked()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return true;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return true;
        return DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
    }
}