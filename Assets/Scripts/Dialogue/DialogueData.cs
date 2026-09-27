using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueActionType
{
    None,
    SetFlag,
    StartMinigame
}

[Serializable]
public class DialogueAction
{
    public DialogueActionType type;
    public GameState minigame = GameState.Minigame_EndlessRunner;
    public MinigameConfig config;
    public string flag;

    public void Execute()
    {
        switch (type)
        {
            case DialogueActionType.SetFlag:
                var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
                if (save == null || string.IsNullOrEmpty(flag)) return;
                save.SetFlag(flag, true);
                SaveManager.Instance.SaveCurrent();
                break;

            case DialogueActionType.StartMinigame:
                if (GameManager.Instance != null)
                    GameManager.Instance.StartMinigame(minigame, string.IsNullOrEmpty(flag) ? null : flag, config);
                break;
        }
    }
}

[Serializable]
public class DialogueLine
{
    [TextArea(2, 5)] public string text;
    public CharacterProfile speaker;
}

[Serializable]
public class DialogueChoice
{
    public string text;
    public List<DialogueLine> responseLines = new List<DialogueLine>();
    public DialogueData next;
    public DialogueAction action = new DialogueAction();

    public bool HasResponse => responseLines != null && responseLines.Count > 0;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue")]
public class DialogueData : ScriptableObject
{
    public List<DialogueLine> lines = new List<DialogueLine>();
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    public bool HasLines => lines != null && lines.Count > 0;
    public bool HasChoices => choices != null && choices.Count > 0;
}