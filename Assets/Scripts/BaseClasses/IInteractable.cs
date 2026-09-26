public interface IInteractable
{
    string InteractionPrompt { get; } // e.g. "Talk to Bob" - for UI prompt display
    bool CanInteract { get; }
    void Interact(PlayerController interactor);
}