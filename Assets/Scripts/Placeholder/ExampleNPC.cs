using UnityEngine;

// placeholder until the dialogue system exists - shows the freeze/unfreeze
// pattern IInteractable implementers should follow
public class ExampleNpc : MonoBehaviour, IInteractable
{
    [SerializeField] private string npcName = "NPC";

    public string InteractionPrompt => $"Talk to {npcName}";
    public bool CanInteract => true;

    public void Interact(PlayerController interactor)
    {
        interactor.FreezeMovement();

        // ... start dialogue here ...
        // DialogueManager.Instance.StartDialogue(someNode, onComplete: () => interactor.UnfreezeMovement());

        Debug.Log($"{npcName}: Hello there!");
        interactor.UnfreezeMovement();
    }
}