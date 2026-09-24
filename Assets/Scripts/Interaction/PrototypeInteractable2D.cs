using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class PrototypeInteractable2D : MonoBehaviour, IInteractable2D
{
    public int Count { get; private set; }
    public int InteractionPriority => 0;
    public Vector3 InteractionPosition => transform.position;
    public string InteractionPrompt => $"E / B  Interact ({Count})";
    public bool CanInteract(GameObject interactor) => isActiveAndEnabled;
    public void Interact(GameObject interactor) { if (CanInteract(interactor)) Count++; }
}
