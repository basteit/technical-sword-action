using UnityEngine;

public interface IInteractable2D
{
    int InteractionPriority { get; }
    Vector3 InteractionPosition { get; }
    string InteractionPrompt { get; }
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
