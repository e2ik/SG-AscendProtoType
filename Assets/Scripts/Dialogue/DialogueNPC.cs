using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueNpc : MonoBehaviour, IInteractable
{
    [Serializable]
    public class ConditionalDialogue
    {
        public string requiredFlag;
        public DialogueData dialogue;
    }

    [SerializeField] private CharacterProfile profile;
    [SerializeField] private DialogueData defaultDialogue;
    [SerializeField] private List<ConditionalDialogue> conditionalDialogues = new List<ConditionalDialogue>();
    [SerializeField] private bool debugLog;

    public CharacterProfile Profile => profile;
    public string DisplayName => profile != null && !string.IsNullOrEmpty(profile.displayName) ? profile.displayName : name;
    public string InteractionPrompt => $"Talk to {DisplayName}";

    public bool CanInteract =>
        DialogueManager.Instance != null && !DialogueManager.Instance.IsActive && ChooseDialogue() != null;

    public void Interact(PlayerController interactor)
    {
        var dialogue = ChooseDialogue(debugLog);
        if (dialogue == null) return;

        interactor.FreezeMovement();
        CameraFollow2D.FocusDialogue(transform, interactor.transform);

        void Finish()
        {
            interactor.UnfreezeMovement();
            CameraFollow2D.ClearFocusAll();
        }

        if (!DialogueManager.Instance.StartDialogue(dialogue, Finish, profile, GetSideRelativeTo(interactor.transform)))
            Finish();
    }

    private DialogueSide GetSideRelativeTo(Transform player)
    {
        float npcX = TryGetComponent<Collider2D>(out var col) ? col.bounds.center.x : transform.position.x;
        return npcX < player.position.x ? DialogueSide.Left : DialogueSide.Right;
    }

    private DialogueData ChooseDialogue() => ChooseDialogue(false);

    private DialogueData ChooseDialogue(bool log)
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        if (save == null)
        {
            if (log) Debug.Log($"[{DisplayName}] No save loaded - flags can't be checked, using Default Dialogue '{Describe(defaultDialogue)}'", this);
            return defaultDialogue;
        }

        foreach (var entry in conditionalDialogues)
        {
            if (entry.dialogue == null || string.IsNullOrEmpty(entry.requiredFlag)) continue;

            bool hasFlag = save.GetFlag(entry.requiredFlag);
            if (log) Debug.Log($"[{DisplayName}] Checking '{entry.requiredFlag}': {(hasFlag ? "SET" : "not set")}", this);

            if (hasFlag)
            {
                if (log) Debug.Log($"[{DisplayName}] Using '{Describe(entry.dialogue)}'", this);
                return entry.dialogue;
            }
        }

        if (log) Debug.Log($"[{DisplayName}] No flags matched - using Default Dialogue '{Describe(defaultDialogue)}'", this);
        return defaultDialogue;
    }

    private static string Describe(DialogueData dialogue) => dialogue != null ? dialogue.name : "none";
}