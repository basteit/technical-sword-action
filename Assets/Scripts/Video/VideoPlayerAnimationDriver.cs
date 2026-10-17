using TechnicalSwordAction.PlayerState;
using UnityEngine;

public sealed class VideoPlayerAnimationDriver : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField,Tooltip("Go directly to idle after a sword attack or airborne counter. Keeps the recovery artwork available for later comparison.")]
    private bool skipAttackRecovery;
    private Animator animator;
    private PlayerStateMachine state;
    private PlayerParry2D parry;
    private PlayerAttack2D attack;
    private PlayerMotor2D motor;
    private PlayerVisualRecovery2D recovery;
    private PlayerParryCounter2D counter;
    private SpriteRenderer visual;
    private int lastStep=1;
    private string driven="";
    private PlayerState lastMotion=PlayerState.Idle;
    private PlayerActionState lastAction = (PlayerActionState)(-1);
    private long parryStartTick;
    private string parryRecoveryMotion = "ParryDeflect";
    public bool IsRecovering=>recovery!=null&&recovery.IsPlaying;
    public VideoArtLibrary Art=>library;
    public Vector2 ParryContactPoint
    {
        get
        {
            var motion=library!=null?library.FindParrySuccess(parry.LastReactionWeight,out _):null;
            Vector2 offset=motion!=null&&motion.hasParryContact?motion.parryContactOffset:new Vector2(19/32f,8/32f);
            return (Vector2)transform.position+new Vector2(motor.FacingSign*offset.x,offset.y);
        }
    }
    public bool SkipAttackRecovery { get=>skipAttackRecovery;set=>skipAttackRecovery=value; }
    public int CombatTickOrder => 310;
    private void OnEnable()
    {
        animator=GetComponent<Animator>();state=GetComponent<PlayerStateMachine>();parry=GetComponent<PlayerParry2D>();
        attack=GetComponent<PlayerAttack2D>();motor=GetComponent<PlayerMotor2D>();recovery=GetComponent<PlayerVisualRecovery2D>();
        counter=GetComponent<PlayerParryCounter2D>();visual=GetComponent<SpriteRenderer>();
        CombatTimeController.Register(this);CombatTimeController.RegisterAnimator(animator,this);
        parry.Succeeded+=OnParried;attack.StepStarted+=OnStep;
    }
    private void OnStep(int step) { lastStep=step;driven="Attack_"+step; }
    private void OnParried(Damageable2D source, ParryResult result)
    {
        recovery?.Cancel();parryStartTick=CombatTimeController.TickCount;
        string reaction = SuccessClip();
        Play(reaction);lastAction=PlayerActionState.ParrySuccess;
        parryRecoveryMotion = library != null && library.Find("Recovery_" + reaction) != null ? reaction : "ParryDeflect";
        // The collision can occur after Animator.Update in this tick.
        // Freeze the actual blade contact immediately when hitstop begins.
        int startFrame = 0;
        var motion=library!=null?library.FindParrySuccess(parry.LastReactionWeight,out startFrame):null;
        if(visual!=null&&motion!=null) visual.sprite=motion.At(startFrame);
    }
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
                PlayerActionState.Dash=>"Dash",
                PlayerActionState.ParrySuccess=>parryRecoveryMotion,
                PlayerActionState.ParryFail or PlayerActionState.Parry=>"ParryDeflect",
                _=>lastMotion==PlayerState.Move?"Move":lastMotion==PlayerState.Fall||lastMotion==PlayerState.Jump?"Landing":""
            };
            bool skipReturn=skipAttackRecovery&&(lastAction==PlayerActionState.Attack||lastAction==PlayerActionState.ParryCounter);
            var recover=!skipReturn&&library!=null?library.Find("Recovery_"+name):null;
            lastAction=action;lastMotion=motion;
            if(recover!=null&&recovery!=null&&recovery.TryPlay("PlayerVideo_Recovery_"+name,recover.TotalFrames))
            { driven="Recovery_"+name;return; }
            Play("Idle");return;
        }
        recovery?.Cancel();
        string clip=action switch
        {
            PlayerActionState.Parry => "Parry",
            PlayerActionState.ParrySuccess => SuccessClip(),
            PlayerActionState.ParryFail=>"ParryFail",
            PlayerActionState.ParryCounter => "Counter",
            PlayerActionState.Dash => "Dash",
            _=>motion==PlayerState.Move?"Move":motion==PlayerState.Jump?"Jump":motion==PlayerState.Fall?"Fall":"Idle"
        };
        Play(clip);lastAction=action;lastMotion=motion;
    }
    private void Play(string clip)
    { if(driven==clip) return;animator.Play("PlayerVideo_"+clip,0,0);driven=clip; }
    private string SuccessClip()
    {
        string reaction = parry.LastReactionWeight == ParryReactionWeight.Heavy ? "ParrySuccessHeavy" : "ParrySuccessLight";
        if (animator.HasState(0, Animator.StringToHash("PlayerVideo_" + reaction))) return reaction;
        return animator.HasState(0, Animator.StringToHash("PlayerVideo_ParrySuccessLight")) ? "ParrySuccessLight" : "ParrySuccess";
    }
    public void CombatTickTimers()
    {
        // Counter timers run after Animator.Update. Sample the same integer frame
        // after its events, so slow entry and hitstop never hold the previous pose.
        if(visual==null||library==null) return;
        if(state.ActionState==PlayerActionState.Parry)
        {
            var ready=library.Find("ParryReady");
            if(ready!=null) visual.sprite=ready.At(parry.ElapsedFrames);
            return;
        }
        if(state.ActionState==PlayerActionState.ParrySuccess)
        {
            var motion=library.FindParrySuccess(parry.LastReactionWeight,out int startFrame);
            if(motion!=null)
                visual.sprite=motion.At(startFrame+(int)(CombatTimeController.TickCount-parryStartTick));
            return;
        }
        if(counter!=null&&counter.IsActive&&counter.IsAirborneKick)
        {
            var motion=library.Find("AirRoundhouse");
            if(motion!=null) visual.sprite=motion.At(counter.ElapsedFrames);
            return;
        }
        if(attack!=null&&attack.IsAttacking)
        {
            var info=animator.GetCurrentAnimatorStateInfo(0);
            if(info.shortNameHash!=Animator.StringToHash("PlayerVideo_Attack_"+attack.ComboStep)) return;
            // Animation Events still own damage. Quantize only the sprite lookup,
            // avoiding a float-key boundary holding the wind-up during hitstop.
            int frame=Mathf.FloorToInt(info.normalizedTime*info.length*60+.001f);
            var motion=library.Find(new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash"}[attack.ComboStep-1]);
            if(motion!=null) visual.sprite=motion.At(frame);
        }
    }
    private void OnDisable()
    { recovery?.Cancel();if(parry!=null) parry.Succeeded-=OnParried;if(attack!=null) attack.StepStarted-=OnStep;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this); }
}
