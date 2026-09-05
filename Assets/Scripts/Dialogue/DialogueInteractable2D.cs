using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DialogueInteractable2D : MonoBehaviour, IInteractable2D
{
    [SerializeField] private DialogueSequence sequence;
    [SerializeField] private DialogueController controller;
    [SerializeField] private string interactionPrompt = "E / B  話す";
    [SerializeField] private int interactionPriority;
    [SerializeField] private bool playOnce;
    [SerializeField] private UnityEvent onFinished = new();

    private Collider2D triggerCollider;
    private bool hasPlayed;
    private bool subscribed;
    private bool ownsCurrentPlayback;

    public int InteractionPriority => interactionPriority;
    public Vector3 InteractionPosition => triggerCollider != null
        ? triggerCollider.bounds.center
        : transform.position;
    public string InteractionPrompt => interactionPrompt;
    public bool HasPlayed => hasPlayed;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        ResolveController();
    }

    private void OnEnable()
    {
        ResolveController();
        Subscribe();
    }

    public bool CanInteract(GameObject interactor)
    {
        return sequence != null && controller != null && !controller.IsGameplayLockActive && (!playOnce || !hasPlayed);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        ownsCurrentPlayback = controller.TryPlay(sequence);
    }

    public void ResetPlayedState()
    {
        hasPlayed = false;
    }

    private void ResolveController()
    {
        if (controller == null)
        {
            controller = FindFirstObjectByType<DialogueController>(FindObjectsInactive.Include);
        }
    }

    private void Subscribe()
    {
        if (controller == null || subscribed)
        {
            return;
        }

        controller.DialogueFinished += HandleDialogueFinished;
        subscribed = true;
    }

    private void HandleDialogueFinished(DialogueSequence finishedSequence, DialogueCompletionReason reason)
    {
        if (!ownsCurrentPlayback || finishedSequence != sequence)
        {
            return;
        }

        ownsCurrentPlayback = false;

        if (reason == DialogueCompletionReason.Interrupted)
        {
            return;
        }

        hasPlayed = true;
        onFinished.Invoke();
    }

    private void OnDisable()
    {
        if (controller != null && subscribed)
        {
            controller.DialogueFinished -= HandleDialogueFinished;
        }

        subscribed = false;
        ownsCurrentPlayback = false;
    }

    private void OnValidate()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }
}
