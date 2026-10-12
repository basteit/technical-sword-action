using TechnicalSwordAction.PlayerState;
using UnityEngine;

public sealed class VideoPlayerAnimationDriver : MonoBehaviour, ICombatTickListener
{
    private Animator animator;
    private PlayerStateMachine state;
    private PlayerParry2D parry;
    private PlayerActionState lastAction = (PlayerActionState)(-1);
    public int CombatTickOrder => 310;
    private void OnEnable()
    {
        animator=GetComponent<Animator>();state=GetComponent<PlayerStateMachine>();parry=GetComponent<PlayerParry2D>();
        CombatTimeController.Register(this);CombatTimeController.RegisterAnimator(animator,this);
        parry.Succeeded+=OnParried;
    }
    private void OnParried(Damageable2D source, ParryResult result)
    { animator.Play("PlayerVideo_ParrySuccess",0,0);lastAction=PlayerActionState.ParrySuccess; }
    public void ResetVisual()
    { lastAction=PlayerActionState.Neutral;animator.Rebind();animator.Play("PlayerVideo_Idle",0,0);animator.Update(0); }
    public void CombatTick()
    {
        var action=state.ActionState;
        if (action==PlayerActionState.Attack) { lastAction=action;return; }
        if (lastAction==action) return;
        string clip=action switch
        {
            PlayerActionState.Parry => "Parry",
            PlayerActionState.ParrySuccess => "ParrySuccess",
            PlayerActionState.ParryCounter => "Counter",
            _ => "Idle"
        };
        animator.Play("PlayerVideo_"+clip,0,0);lastAction=action;
    }
    private void OnDisable()
    { if(parry!=null) parry.Succeeded-=OnParried;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this); }
}
