using TechnicalSwordAction.PlayerState;
using UnityEngine;

public sealed class VideoPlayerAnimationDriver : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private VideoArtLibrary library;
    private Animator animator;
    private PlayerStateMachine state;
    private PlayerParry2D parry;
    private PlayerAttack2D attack;
    private PlayerMotor2D motor;
    private PlayerVisualRecovery2D recovery;
    private int lastStep=1;
    private string driven="";
    private PlayerState lastMotion=PlayerState.Idle;
    private PlayerActionState lastAction = (PlayerActionState)(-1);
    public bool IsRecovering=>recovery!=null&&recovery.IsPlaying;
    public int CombatTickOrder => 310;
    private void OnEnable()
    {
        animator=GetComponent<Animator>();state=GetComponent<PlayerStateMachine>();parry=GetComponent<PlayerParry2D>();
        attack=GetComponent<PlayerAttack2D>();motor=GetComponent<PlayerMotor2D>();recovery=GetComponent<PlayerVisualRecovery2D>();
        CombatTimeController.Register(this);CombatTimeController.RegisterAnimator(animator,this);
        parry.Succeeded+=OnParried;attack.StepStarted+=OnStep;
    }
    private void OnStep(int step) { lastStep=step;driven="Attack_"+step; }
    private void OnParried(Damageable2D source, ParryResult result)
    { recovery?.Cancel();Play("ParrySuccess");lastAction=PlayerActionState.ParrySuccess; }
    public void ResetVisual()
    { recovery?.Cancel();lastAction=PlayerActionState.Neutral;lastMotion=PlayerState.Idle;lastStep=1;driven="";animator.Rebind();Play("Idle");animator.Update(0); }
    public void CombatTick()
    {
        var action=state.ActionState;var motion=state.CurrentState;
        if(state.LifeState==PlayerLifeState.Dead) { recovery?.Cancel();lastAction=action;lastMotion=motion;return; }
        if(action==PlayerActionState.Attack) { recovery?.Cancel();lastAction=action;lastMotion=motion;return; }
        if(action==PlayerActionState.Neutral&&motion==PlayerState.Idle)
        {
            if(recovery!=null&&recovery.IsPlaying) return;
            string name=lastAction switch
            {
                PlayerActionState.Attack=>new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash"}[lastStep-1],
                PlayerActionState.ParryCounter=>"AirRoundhouse",
                PlayerActionState.ParrySuccess or PlayerActionState.ParryFail or PlayerActionState.Parry=>"ParryDeflect",
                _=>lastMotion==PlayerState.Move?"Move":lastMotion==PlayerState.Fall||lastMotion==PlayerState.Jump?"Landing":""
            };
            var recover=library!=null?library.Find("Recovery_"+name):null;
            lastAction=action;lastMotion=motion;
            if(recover!=null&&recovery!=null&&recovery.TryPlay("PlayerVideo_Recovery_"+name,recover.TotalFrames))
            { driven="Recovery_"+name;return; }
            Play("Idle");return;
        }
        recovery?.Cancel();
        string clip=action switch
        {
            PlayerActionState.Parry => "Parry",
            PlayerActionState.ParrySuccess => "ParrySuccess",
            PlayerActionState.ParryFail=>"ParryFail",
            PlayerActionState.ParryCounter => "Counter",
            _=>motion==PlayerState.Move?"Move":motion==PlayerState.Jump?"Jump":motion==PlayerState.Fall?"Fall":"Idle"
        };
        Play(clip);lastAction=action;lastMotion=motion;
    }
    private void Play(string clip)
    { if(driven==clip) return;animator.Play("PlayerVideo_"+clip,0,0);driven=clip; }
    private void OnDisable()
    { recovery?.Cancel();if(parry!=null) parry.Succeeded-=OnParried;if(attack!=null) attack.StepStarted-=OnStep;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this); }
}
