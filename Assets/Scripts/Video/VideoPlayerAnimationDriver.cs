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
    private PlayerDamageReceiver2D damageReceiver;
    private bool parryImpactPending;
    private float parryHitstopSeconds;
    private Damageable2D parrySource;
    private bool parryHadSource;
    private ParryResult parryResult;
    public event System.Action<Damageable2D,ParryResult> ParryImpact;
    public int ParryImpactCount {get;private set;}
    public long ParryImpactTick {get;private set;}=-1;
    public bool IsParryImpactPending=>parryImpactPending;
    public int ParryImpactDelayFrames
    {
        get
        {
            var reaction=library!=null?library.FindParrySuccess(parry.LastReactionWeight,out _):null;
            return isActiveAndEnabled&&reaction!=null&&reaction.includesParryEffect?Mathf.Max(0,reaction.hitFrame):0;
        }
    }
    private string parryRecoveryMotion = "ParryDeflect";
    public bool IsRecovering=>recovery!=null&&recovery.IsPlaying;
    public VideoArtLibrary Art=>library;
    public int CounterArtFrame
    {
        get
        {
            int frame=counter.ElapsedFrames-(counter.HasAuthoredTransfer?counter.BlinkArrivalFrame:0);
            var kick=library!=null?library.Find("CounterKick"):null;
            if(kick==null||!counter.HasAuthoredTransfer) return frame;
            int hold=Mathf.Max(0,counter.ChargeReleaseFrame-counter.BlinkArrivalFrame-kick.releaseFrame);
            return frame<=kick.releaseFrame?frame:Mathf.Max(kick.releaseFrame,frame-hold);
        }
    }
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
        damageReceiver=GetComponent<PlayerDamageReceiver2D>();
        if(damageReceiver!=null) damageReceiver.ParryFeedbackRequested+=OnParryFeedbackRequested;
        CombatTimeController.SessionReset+=CancelParryImpact;
        CombatTimeController.Register(this);CombatTimeController.RegisterAnimator(animator,this);
        parry.Succeeded+=OnParried;attack.StepStarted+=OnStep;
    }
    private void OnStep(int step) { lastStep=step;driven="Attack_"+step; }
    private void OnParried(Damageable2D source, ParryResult result)
    {
        CancelParryImpact();recovery?.Cancel();parryStartTick=CombatTimeController.TickCount;
        parrySource=source;parryHadSource=source!=null;parryResult=result;
        string reaction = SuccessClip();
        Play(reaction);lastAction=PlayerActionState.ParrySuccess;
        parryRecoveryMotion = library != null && library.Find("Recovery_" + reaction) != null ? reaction : "ParryDeflect";
        // Begin the received reaction immediately; its metadata selects the later impact cel.
        int startFrame = 0;
        var motion=library!=null?library.FindParrySuccess(parry.LastReactionWeight,out startFrame):null;
        if(visual!=null&&motion!=null) visual.sprite=motion.At(startFrame);
    }
    public void ResetVisual()
    { CancelParryImpact();ParryImpactCount=0;ParryImpactTick=-1;recovery?.Cancel();lastAction=PlayerActionState.Neutral;lastMotion=PlayerState.Idle;lastStep=1;driven="";animator.Rebind();Play("Idle");animator.Update(0); }
    private void OnParryFeedbackRequested(ParryResult result,float hitstopSeconds)
    {
        parryHitstopSeconds=hitstopSeconds;parryImpactPending=true;
        if(ParryImpactDelayFrames==0) PresentParryImpact();
    }
    private void PresentParryImpact()
    {
        if(!parryImpactPending)return;
        if(parryHadSource&&(parrySource==null||!parrySource.isActiveAndEnabled||!parrySource.IsAlive))
        {CancelParryImpact();return;}
        parryImpactPending=false;ParryImpactCount++;ParryImpactTick=CombatTimeController.TickCount;
        CombatTimeController.RequestHitstop(this,parryHitstopSeconds);
        ParryImpact?.Invoke(parrySource,parryResult);
    }
    private void CancelParryImpact()
    { parryImpactPending=false;parryHitstopSeconds=0;parrySource=null;parryHadSource=false;CombatTimeController.ReleaseOwner(this); }
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
            PlayerActionState.ParryCounter => counter.HasAuthoredTransfer && counter.ElapsedFrames < counter.BlinkArrivalFrame ? "CounterTransfer" : "Counter",
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
            if(parryImpactPending&&CombatTimeController.TickCount-parryStartTick>=ParryImpactDelayFrames)
                PresentParryImpact();
            return;
        }
        if(parryImpactPending)CancelParryImpact();
        if(counter!=null&&counter.IsActive&&counter.IsAirborneKick)
        {
            var transfer=library.Find("CounterTransfer");
            bool transferring=counter.HasAuthoredTransfer&&transfer!=null&&counter.ElapsedFrames<counter.BlinkArrivalFrame;
            var motion=transferring?transfer:library.Find("CounterKick")??library.Find("AirRoundhouse");
            int localFrame=transferring?counter.ElapsedFrames:CounterArtFrame;
            if(motion!=null) visual.sprite=motion.At(localFrame);
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
    { CancelParryImpact();CombatTimeController.SessionReset-=CancelParryImpact;if(damageReceiver!=null)damageReceiver.ParryFeedbackRequested-=OnParryFeedbackRequested;recovery?.Cancel();if(parry!=null) parry.Succeeded-=OnParried;if(attack!=null) attack.StepStarted-=OnStep;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this); }
}
