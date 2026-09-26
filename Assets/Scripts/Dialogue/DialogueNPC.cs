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

    public CharacterProfile Profile => profile;
    public string DisplayName => profile != null && !string.IsNullOrEmpty(profile.displayName) ? profile.displayName : name;
    public string InteractionPrompt => $"Talk to {DisplayName}";

    public bool CanInteract =>
        DialogueManager.Instance != null && !DialogueManager.Instance.IsActive && ChooseDialogue() != null;

    public void Interact(PlayerController interactor)
    {
        var dialogue = ChooseDialogue();
        if (dialogue == null) return;

        interactor.FreezeMovement();
        if (!DialogueManager.Instance.StartDialogue(dialogue, interactor.UnfreezeMovement, profile, GetSideRelativeTo(interactor.transform)))
            interactor.UnfreezeMovement();
    }

    private DialogueSide GetSideRelativeTo(Transform player)
    {
        float npcX = TryGetComponent<Collider2D>(out var col) ? col.bounds.center.x : transform.position.x;
        return npcX < player.position.x ? DialogueSide.Left : DialogueSide.Right;
    }

    private DialogueData ChooseDialogue()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        if (save != null)
        {
            foreach (var entry in conditionalDialogues)
            {
                if (entry.dialogue != null && !string.IsNullOrEmpty(entry.requiredFlag) && save.GetFlag(entry.requiredFlag))
                    return entry.dialogue;
            }
        }

        return defaultDialogue;
    }
}