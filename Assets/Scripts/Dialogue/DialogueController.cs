using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum DialogueCompletionReason
{
    Completed,
    Skipped,
    Interrupted
}

[Serializable]
public sealed class DialogueIdEvent : UnityEvent<string>
{
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class DialogueController : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private DialogueView view;

    [Header("Prototype Gameplay Lock")]
    [Tooltip("Temporary bridge until all gameplay input is routed through Input Actions.")]
    [SerializeField] private Behaviour[] gameplayBehavioursToDisable;
    [SerializeField] private Rigidbody2D playerBody;

    [Header("Voice")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Input")]
    [SerializeField, Min(0f)] private float releaseGuardDuration = 0.08f;

    [Header("Events")]
    [SerializeField] private DialogueIdEvent onDialogueStarted = new();
    [SerializeField] private DialogueIdEvent onDialogueCompleted = new();

    private InputAction advanceAction;
    private InputAction skipAction;
    private bool[] previousBehaviourStates;
    private DialogueSequence currentSequence;
    private int currentLineIndex = -1;
    private bool waitingForRelease;
    private float releaseGuardRemaining;
    private bool pendingGameplayUnlock;

    private static DialogueController activeController;

    public static bool GameplayInputBlocked => activeController != null && activeController.IsGameplayLockActive;
    public bool IsOpen => currentSequence != null;
    public bool IsGameplayLockActive => IsOpen || pendingGameplayUnlock;
    public DialogueSequence CurrentSequence => currentSequence;
    public int CurrentLineIndex => currentLineIndex;

    public event Action<DialogueSequence> DialogueStarted;
    public event Action<DialogueSequence, int, DialogueLine> DialogueLineChanged;
    public event Action<DialogueSequence, DialogueCompletionReason> DialogueFinished;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponentInChildren<DialogueView>(true);
        }

        if (voiceSource == null)
        {
            voiceSource = GetComponent<AudioSource>();
        }

        if (voiceSource != null)
        {
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
        }

        view?.HideImmediate();
        CreateInputActions();
    }

    private void OnEnable()
    {
        CreateInputActions();
        advanceAction.Enable();
        skipAction.Enable();
    }

    private void Update()
    {
        if (waitingForRelease)
        {
            releaseGuardRemaining = Mathf.Max(0f, releaseGuardRemaining - Time.unscaledDeltaTime);
            if (releaseGuardRemaining <= 0f && !advanceAction.IsPressed() && !skipAction.IsPressed())
            {
                waitingForRelease = false;
            }
            if (waitingForRelease)
            {
                return;
            }
        }

        if (pendingGameplayUnlock)
        {
            ReleaseGameplayLock();
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        if (currentSequence.AllowSkip && skipAction.WasPressedThisFrame())
        {
            Finish(DialogueCompletionReason.Skipped);
            return;
        }

        if (advanceAction.WasPressedThisFrame())
        {
            Advance();
        }
    }

    public bool TryPlay(DialogueSequence sequence)
    {
        if (sequence == null || sequence.LineCount <= 0 || IsGameplayLockActive)
        {
            return false;
        }

        if (activeController != null && activeController != this && activeController.IsGameplayLockActive)
        {
            return false;
        }

        currentSequence = sequence;
        currentLineIndex = 0;
        activeController = this;
        pendingGameplayUnlock = false;
        waitingForRelease = true;
        releaseGuardRemaining = releaseGuardDuration;

        SetGameplayLocked(true);
        onDialogueStarted.Invoke(sequence.StableId);
        DialogueStarted?.Invoke(sequence);
        ShowCurrentLine();
        return true;
    }

    public void Advance()
    {
        if (!IsOpen)
        {
            return;
        }

        int nextIndex = currentLineIndex + 1;
        if (nextIndex >= currentSequence.LineCount)
        {
            Finish(DialogueCompletionReason.Completed);
            return;
        }

        currentLineIndex = nextIndex;
        ShowCurrentLine();
    }

    public void Skip()
    {
        if (IsOpen && currentSequence.AllowSkip)
        {
            Finish(DialogueCompletionReason.Skipped);
        }
    }

    public void Interrupt()
    {
        if (IsOpen)
        {
            Finish(DialogueCompletionReason.Interrupted);
        }

        // Forced interruption (Hit / Dead / Disable) must not wait for the
        // dialogue input release guard. Otherwise a held advance button can
        // leave gameplay disabled after the dialogue UI has already closed.
        if (pendingGameplayUnlock)
        {
            waitingForRelease = false;
            releaseGuardRemaining = 0f;
            ReleaseGameplayLock();
        }
    }

    public static bool InterruptActive()
    {
        DialogueController controller = activeController;
        if (controller == null || !controller.IsGameplayLockActive)
        {
            return false;
        }

        controller.Interrupt();
        return true;
    }

    private void ShowCurrentLine()
    {
        if (!currentSequence.TryGetLine(currentLineIndex, out DialogueLine line))
        {
            Finish(DialogueCompletionReason.Interrupted);
            return;
        }

        bool isLast = currentLineIndex >= currentSequence.LineCount - 1;
        view?.Show(line, isLast);
        DialogueLineChanged?.Invoke(currentSequence, currentLineIndex, line);

        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = line.Voice;
            if (line.Voice != null)
            {
                voiceSource.Play();
            }
        }
    }

    private void Finish(DialogueCompletionReason reason)
    {
        DialogueSequence finishedSequence = currentSequence;
        currentSequence = null;
        currentLineIndex = -1;
        waitingForRelease = true;
        releaseGuardRemaining = releaseGuardDuration;

        view?.HideImmediate();
        if (voiceSource != null)
        {
            voiceSource.Stop();
            voiceSource.clip = null;
        }

        pendingGameplayUnlock = true;

        if (finishedSequence == null)
        {
            return;
        }

        if (reason == DialogueCompletionReason.Completed || reason == DialogueCompletionReason.Skipped)
        {
            onDialogueCompleted.Invoke(finishedSequence.StableId);
        }

        DialogueFinished?.Invoke(finishedSequence, reason);
    }

    private void ReleaseGameplayLock()
    {
        SetGameplayLocked(false);
        pendingGameplayUnlock = false;
        if (activeController == this)
        {
            activeController = null;
        }
    }

    private void SetGameplayLocked(bool locked)
    {
        if (locked)
        {
            previousBehaviourStates = new bool[gameplayBehavioursToDisable?.Length ?? 0];
            for (int i = 0; i < previousBehaviourStates.Length; i++)
            {
                Behaviour behaviour = gameplayBehavioursToDisable[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                previousBehaviourStates[i] = behaviour.enabled;
                behaviour.enabled = false;
            }

            if (playerBody != null)
            {
                playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
            }

            return;
        }

        if (gameplayBehavioursToDisable == null || previousBehaviourStates == null)
        {
            return;
        }

        int count = Mathf.Min(gameplayBehavioursToDisable.Length, previousBehaviourStates.Length);
        for (int i = 0; i < count; i++)
        {
            Behaviour behaviour = gameplayBehavioursToDisable[i];
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = previousBehaviourStates[i];
            }
        }

        previousBehaviourStates = null;
    }

    private void CreateInputActions()
    {
        if (advanceAction != null)
        {
            return;
        }

        advanceAction = new InputAction("DialogueAdvance", InputActionType.Button);
        advanceAction.AddBinding("<Keyboard>/e");
        advanceAction.AddBinding("<Keyboard>/enter");
        advanceAction.AddBinding("<Keyboard>/space");
        advanceAction.AddBinding("<Mouse>/leftButton");
        advanceAction.AddBinding("<Gamepad>/buttonEast");
        advanceAction.AddBinding("<Gamepad>/buttonSouth");

        skipAction = new InputAction("DialogueSkip", InputActionType.Button);
        skipAction.AddBinding("<Keyboard>/escape");
        skipAction.AddBinding("<Gamepad>/select");
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            Finish(DialogueCompletionReason.Interrupted);
        }

        if (pendingGameplayUnlock)
        {
            ReleaseGameplayLock();
        }

        advanceAction?.Disable();
        skipAction?.Disable();
    }

    private void OnDestroy()
    {
        advanceAction?.Dispose();
        skipAction?.Dispose();
    }
}
