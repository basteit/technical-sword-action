using System;
using TechnicalSwordAction.PlayerState;
using UnityEngine;

/// <summary>Optional return-to-idle animation. It never owns or locks a gameplay action.</summary>
[DisallowMultipleComponent]
public sealed class PlayerVisualRecovery2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private Animator animator;
    private int duration;
    public bool IsPlaying { get; private set; }
    public int ElapsedFrames { get; private set; }
    public string StateName { get; private set; } = "";
    public event Action<string> Started;
    public event Action<string,bool> Ended;
    public int CombatTickOrder=>305;
    private bool CanContinue=>isActiveAndEnabled&&state!=null&&motor!=null&&
        state.LifeState==PlayerLifeState.Alive&&state.ActionState==PlayerActionState.Neutral&&
        !state.CombatSuspended&&!DialogueController.GameplayInputBlocked&&motor.IsGrounded&&
        Mathf.Abs(motor.MoveInput)<.01f&&!motor.DownInput&&(state.PendingRequests&PlayerActionRequest.Gameplay)==0;
    private void OnEnable()
    {
        state=GetComponent<PlayerStateMachine>();motor=GetComponent<PlayerMotor2D>();animator=GetComponent<Animator>();
        CombatTimeController.Register(this);CombatTimeController.RegisterAnimator(animator,this);
    }
    public bool TryPlay(string animationState,int frames)
    {
        if(!CanContinue||animator==null||animator.runtimeAnimatorController==null||!animator.HasState(0,Animator.StringToHash(animationState))||frames<=0) return false;
        Cancel();IsPlaying=true;StateName=animationState;ElapsedFrames=0;duration=frames;
        animator.Play(animationState,0,0);animator.Update(0);Started?.Invoke(StateName);return true;
    }
    public void CombatTick() { if(IsPlaying&&!CanContinue) Cancel(); }
    public void CombatTickTimers()
    { if(IsPlaying&&++ElapsedFrames>=duration) End(true); }
    public void Cancel() { if(IsPlaying) End(false); }
    private void End(bool completed)
    {
        string name=StateName;IsPlaying=false;ElapsedFrames=duration=0;StateName="";
        Ended?.Invoke(name,completed);
    }
    private void OnDisable()
    { Cancel();CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this); }
}
