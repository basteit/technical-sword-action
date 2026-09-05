using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TechnicalSwordAction.PlayerState;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerInteractor2D : MonoBehaviour
{
    [SerializeField] private InteractionPromptView promptView;

    private readonly Dictionary<Collider2D, List<MonoBehaviour>> overlapSources = new();
    private readonly List<MonoBehaviour> candidates = new();
    private InputAction interactAction;
    private IInteractable2D currentInteractable;
    private bool waitForRelease = true;
    private PlayerMotor2D motor;
    private PlayerAttack2D attack;
    private PlayerParry2D parry;
    private PlayerSpecialSkill2D specialSkill;
    private PlayerDamageReceiver2D damageReceiver;
    private PlayerStateMachine stateMachine;

    public IInteractable2D CurrentInteractable => currentInteractable;

    private void Awake()
    {
        ResolvePlayerActions();
        CreateInputAction();
    }

    private void OnEnable()
    {
        CreateInputAction();
        interactAction.Enable();
        waitForRelease = true;
    }

    private void Update()
    {
        SelectCurrentInteractable();

        if (DialogueController.GameplayInputBlocked)
        {
            waitForRelease = true;
            promptView?.Hide();
            return;
        }

        UpdatePrompt();

        if (waitForRelease)
        {
            if (!interactAction.IsPressed())
            {
                waitForRelease = false;
            }

            return;
        }

        if (interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
    }

    public bool TryInteract()
    {
        if (!CanStartInteraction() || currentInteractable == null || !currentInteractable.CanInteract(gameObject))
        {
            return false;
        }

        return stateMachine != null && stateMachine.RequestAction(PlayerActionRequest.Interact);
    }

    public bool TryStartInteractionFromStateMachine()
    {
        if (!CanStartInteraction() || currentInteractable == null || !currentInteractable.CanInteract(gameObject))
        {
            return false;
        }

        currentInteractable.Interact(gameObject);
        return true;
    }

    public void CancelInteractionFromStateMachine()
    {
        DialogueController.InterruptActive();

        waitForRelease = true;
        promptView?.Hide();
        currentInteractable = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        List<MonoBehaviour> found = new();
        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractable2D)
            {
                found.Add(behaviours[i]);
            }
        }

        if (found.Count > 0)
        {
            overlapSources[other] = found;
            RebuildCandidates();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && overlapSources.Remove(other))
        {
            RebuildCandidates();
        }
    }

    private void RebuildCandidates()
    {
        candidates.Clear();
        foreach (List<MonoBehaviour> source in overlapSources.Values)
        {
            for (int i = 0; i < source.Count; i++)
            {
                MonoBehaviour behaviour = source[i];
                if (behaviour != null && !candidates.Contains(behaviour))
                {
                    candidates.Add(behaviour);
                }
            }
        }
    }

    private void SelectCurrentInteractable()
    {
        currentInteractable = null;
        if (!CanStartInteraction())
        {
            return;
        }

        int bestPriority = int.MinValue;
        float bestDistance = float.PositiveInfinity;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = candidates[i];
            if (behaviour == null || behaviour is not IInteractable2D candidate)
            {
                candidates.RemoveAt(i);
                continue;
            }

            if (!candidate.CanInteract(gameObject))
            {
                continue;
            }

            int priority = candidate.InteractionPriority;
            float distance = (candidate.InteractionPosition - transform.position).sqrMagnitude;
            if (priority > bestPriority || (priority == bestPriority && distance < bestDistance))
            {
                currentInteractable = candidate;
                bestPriority = priority;
                bestDistance = distance;
            }
        }
    }

    private void UpdatePrompt()
    {
        if (currentInteractable == null)
        {
            promptView?.Hide();
            return;
        }

        promptView?.Show(currentInteractable.InteractionPrompt);
    }

    private void CreateInputAction()
    {
        if (interactAction != null)
        {
            return;
        }

        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void ResolvePlayerActions()
    {
        if (motor == null) motor = GetComponent<PlayerMotor2D>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (parry == null) parry = GetComponent<PlayerParry2D>();
        if (specialSkill == null) specialSkill = GetComponent<PlayerSpecialSkill2D>();
        if (damageReceiver == null) damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
    }

    public bool CanStartInteraction()
    {
        if (stateMachine != null)
        {
            return !DialogueController.GameplayInputBlocked &&
                   stateMachine.LifeState == PlayerLifeState.Alive &&
                   stateMachine.ActionState == PlayerActionState.Neutral &&
                   stateMachine.LocomotionState == PlayerLocomotionState.Grounded &&
                   (motor == null || Mathf.Abs(motor.Velocity.y) <= 0.1f);
        }

        return !DialogueController.GameplayInputBlocked &&
               (motor == null ||
                (motor.IsGrounded && !motor.IsDashing && Mathf.Abs(motor.Velocity.y) <= 0.1f)) &&
               (attack == null || !attack.IsAttacking) &&
               (parry == null || (!parry.IsParryActive && !parry.IsFailLocked)) &&
               (specialSkill == null || !specialSkill.IsUsingSkill) &&
               (damageReceiver == null || !damageReceiver.IsHitLocked);
    }

    private void OnDisable()
    {
        interactAction?.Disable();
        promptView?.Hide();
        currentInteractable = null;
    }

    private void OnDestroy()
    {
        interactAction?.Dispose();
    }
}
