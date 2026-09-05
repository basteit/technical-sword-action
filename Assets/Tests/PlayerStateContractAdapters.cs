#if UNITY_EDITOR
using TechnicalSwordAction.PlayerState;
using UnityEngine;

// Editor-only fixtures in Assembly-CSharp can implement the existing runtime seams.
// Test assemblies access these through reflection; none of this ships in a player build.
namespace TechnicalSwordAction.Tests
{
    public sealed class StateContractActionAdapter : MonoBehaviour, IPlayerActionStateHandler
    {
        public PlayerActionState State = PlayerActionState.Heal;
        public bool Available = true;
        public bool Active { get; private set; }
        public int Starts { get; private set; }
        public PlayerActionState ActionState => State;
        public bool CanStartAction => Available && !Active && isActiveAndEnabled;
        public float LockRemaining => Active ? 1f : 0f;
        public bool TryStartAction()
        {
            if (!CanStartAction) return false;
            Active = true;
            Starts++;
            return true;
        }
        public void CancelAction() => Active = false;
        public void Finish()
        {
            Active = false;
            GetComponent<PlayerStateMachine>().CompleteAction(State, "AdapterComplete");
        }
    }

    public sealed class StateContractInteractable : MonoBehaviour, IInteractable2D
    {
        public int Count { get; private set; }
        public int InteractionPriority => 0;
        public Vector3 InteractionPosition => transform.position;
        public string InteractionPrompt => "Contract test";
        public bool CanInteract(GameObject interactor) => isActiveAndEnabled;
        public void Interact(GameObject interactor) => Count++;
    }
}
#endif
