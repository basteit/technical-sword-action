using System;
using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;

/// <summary>Capture-only staging. All damage, parries and counters use gameplay components.</summary>
[DefaultExecutionOrder(12000)]
public sealed class CombatVideoDirector : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private PlayerStateMachine player;
    [SerializeField] private Damageable2D frontEnemy;
    [SerializeField] private Damageable2D rearEnemy;
    [SerializeField] private int comboStages = 2;
    [SerializeField] private bool defenseCancelTake;
    [SerializeField] private bool useSpacingApproaches;
    [SerializeField] private bool rearEntranceEnabled;
    [SerializeField] private float twoStageRightShift=1f;
    [SerializeField] private float rearEntranceDistance=1.6f;
    [SerializeField] private float rearEntranceSpeed=2.4f;
    [SerializeField] private float rearEntranceReach=1.6f;
    [SerializeField] private VideoCombatPresentation presentation;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private Vector3 playerStart=new Vector3(0,1,0);
    [SerializeField] private Vector3 frontStart=new Vector3(1.1f,1,0);
    [SerializeField] private Vector3 rearStart=new Vector3(-.85f,1,0);
    private CombatTimeController clock;
    private PlayerAttack2D attack;
    private PlayerParry2D parry;
    private PlayerParryCounter2D counter;
    private PlayerVisualRecovery2D recovery;
    private long startTick, rearWindupTick, successTick;
    private int queuedStep;
    private bool attackSent, rearStarted, parrySent, counterSent;
    private bool openingApproachStarted,rearWindupMarked;
    private bool returnDashSent,returnDashObserved,returnAttackSent,counterLanded;
    private double deathReal=-1;
    [Serializable] public sealed class Mark { public string kind,target;public double realSeconds;public long tick; }
    public readonly List<Mark> Marks=new();
    public PlayerStateMachine Player => player;
    public Damageable2D FrontEnemy => frontEnemy;
    public Damageable2D RearEnemy => rearEnemy;
    public int ComboStages => comboStages;
    public bool Running { get; private set; }
    public bool Completed { get; private set; }
    public double RealElapsed { get; private set; }
    public bool MirrorLayout { get; set; }
    public bool DefenseCancelTake { get=>defenseCancelTake; set=>defenseCancelTake=value; }
    public bool AttackWasCancelledForParry { get; private set; }
    public bool ReturnDashObserved=>returnDashObserved;
    // Preserve the optional four-stage comparison's travel while the selected
    // two-stage take uses the supplied sword artwork without an approach.
    public bool UsesSpacingApproaches=>useSpacingApproaches||!defenseCancelTake&&comboStages==4;
    public bool UsesRearEntrance=>rearEntranceEnabled&&!defenseCancelTake&&comboStages==2&&!UsesSpacingApproaches;
    public Vector3 InitialPlayerPosition=>playerStart+Vector3.right*(UsesRearEntrance?twoStageRightShift*(MirrorLayout?-1:1):0);
    public Vector3 InitialFrontPosition=>InitialPosition(frontStart,false);
    public Vector3 InitialRearPosition=>InitialPosition(rearStart,true);
    private Vector3 InitialPosition(Vector3 baseline,bool rear)
    {
        int sign=MirrorLayout?-1:1;
        baseline.x=InitialPlayerPosition.x+(baseline.x-playerStart.x)*sign;
        if(rear&&UsesRearEntrance)baseline.x-=rearEntranceDistance*sign;
        return baseline;
    }
    public int CombatTickOrder=>-200;
    private void Awake()
    {
        attack=player.GetComponent<PlayerAttack2D>();parry=player.GetComponent<PlayerParry2D>();counter=player.GetComponent<PlayerParryCounter2D>();recovery=player.GetComponent<PlayerVisualRecovery2D>();
    }
    private void OnEnable()
    { CombatTimeController.Register(this); }
    private void Start()
    {
        clock=FindFirstObjectByType<CombatTimeController>();
        attack.HitConfirmed+=OnSlash;parry.Succeeded+=OnParry;
        frontEnemy.Died+=OnDeath;rearEnemy.Died+=OnDeath;counter.HitConfirmed+=OnKick;counter.Finished+=OnCounterFinished;
        counter.ChargeStarted+=OnCharge;counter.ChargeReleased+=OnRelease;counter.Landed+=OnLanding;
        counter.Started+=OnCounterStarted;
        if(recovery!=null) { recovery.Started+=OnRecoveryStarted;recovery.Ended+=OnRecoveryEnded; }
        ResetTake(comboStages);
    }
    private void MarkEvent(string kind,Damageable2D target)
    { Marks.Add(new Mark {kind=kind,target=target!=null?target.IndividualId:"",realSeconds=RealElapsed,tick=CombatTimeController.TickCount-startTick}); }
    private void OnSlash(Damageable2D target,int step,Vector2 point)
    {
        int direction=player.GetComponent<PlayerMotor2D>().FacingSign;
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyHitback(new[]{.10f,.18f,.30f,.58f}[step-1],6+step,Vector2.right*direction);
        MarkEvent((defenseCancelTake&&!returnAttackSent?"OpeningSlash":"Slash")+step,target);
        var motion=player.GetComponent<VideoPlayerAnimationDriver>().Art.Find(new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash"}[step-1]);
        if(!motion.includesSlashTrail)
            vfx.Emit("Slash",point,new[]{.65f,.8f,1f,1.25f}[step-1],direction);
        var directionVector=step==1?new Vector2(direction*.2f,-1):new Vector2(direction,step==3?-.3f:.15f);
        presentation.ShakeDirectional(new[]{.09f,.10f,.11f,.14f}[step-1],new[]{.04f,.055f,.075f,.11f}[step-1],directionVector,2.5f);
    }
    private void OnParry(Damageable2D target,ParryResult result)
    {
        successTick=CombatTimeController.TickCount;MarkEvent("Parry",target);
        int facing=player.GetComponent<PlayerMotor2D>().FacingSign;
        Vector2 contact=player.GetComponent<VideoPlayerAnimationDriver>().ParryContactPoint;
        if(target!=null) vfx.Emit("Parry",contact,.85f,facing);
        presentation.PresentLightParry(facing);
    }
    private void OnKick(Damageable2D target,Vector2 point)
    {
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyLaunch(counter.AttacksFromBehind?3.2f:counter.IsOverheadHeelDrop?1.4f:defenseCancelTake?1.75f:2.25f,counter.AttacksFromBehind?.65f:counter.IsOverheadHeelDrop?.18f:1.05f,counter.AttacksFromBehind?22:20,Vector2.right*counter.KickFacingSign);
        MarkEvent("Kick",target);
    }
    private void OnCharge()=>MarkEvent("ChargeStart",counter.SelectedTarget);
    private void OnCounterStarted()=>MarkEvent("CounterStart",counter.SelectedTarget);
    private void OnRelease()=>MarkEvent("ChargeRelease",counter.SelectedTarget);
    private void OnLanding() { counterLanded=true;MarkEvent("Landing",counter.SelectedTarget); }
    private void OnRecoveryStarted(string name) { if(Running) MarkEvent("RecoveryStart:"+name,null); }
    private void OnRecoveryEnded(string name,bool completed) { if(Running) MarkEvent((completed?"RecoveryEnd:":"RecoveryCancel:")+name,null); }
    private void OnCounterFinished(bool hit)
    {
        if(!hit && Running)
        {
            Running=false;
            if(player!=null) { var router=player.GetComponent<PlayerInputRouter>();if(router!=null) router.enabled=true;player.ClearBufferedGameplayInput(); }
        }
    }
    private void OnDeath(Damageable2D target,Vector2 direction)
    { MarkEvent("Death",target);vfx.EmitFollowing("Vanish",target.transform,Vector2.down*.2f,.6f,.18f);if(defenseCancelTake?(!frontEnemy.IsAlive&&!rearEnemy.IsAlive):target==rearEnemy) deathReal=RealElapsed; }
    public void StartTake(int stages=2,bool hybrid=true)
    {
        ResetTake(stages);vfx.HybridGlow=hybrid;
        player.GetComponent<PlayerInputRouter>().enabled=false;
        // Router disable suppresses live edges for the current render frame. Reset the
        // state gate afterwards so multiple deterministic capture frames may run here.
        player.ResetToSafeState("VideoReplayReady");Running=true;
    }
    public void ResetTake(int stages = 2)
    {
        comboStages = stages == 4 ? 4 : 2;
        Running=Completed=false;RealElapsed=0;deathReal=-1;
        startTick=CombatTimeController.TickCount;rearWindupTick=successTick=0;
        queuedStep=0;attackSent=rearStarted=parrySent=counterSent=false;Marks.Clear();
        openingApproachStarted=rearWindupMarked=false;
        returnDashSent=returnDashObserved=returnAttackSent=counterLanded=AttackWasCancelledForParry=false;
        CombatTimeController.ResetSession();
        player.ResetToSafeState("VideoReset");
        player.transform.position = InitialPlayerPosition;
        int layoutSign=MirrorLayout ? -1 : 1;
        player.GetComponent<PlayerMotor2D>().SetFacing(layoutSign);
        player.GetComponent<PlayerMotor2D>().SetMoveInput(Vector2.zero);
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        player.GetComponent<PlayerSpecialGauge>().ResetGauge();
        frontEnemy.ResetForCapture(InitialFrontPosition, (comboStages == 4 ? 7 : 2)+(defenseCancelTake?1:0));
        rearEnemy.ResetForCapture(InitialRearPosition, 3);
        frontEnemy.GetComponent<EnemyMeleeChaser2D>().ResetForCapture(player.transform);
        rearEnemy.GetComponent<EnemyMeleeChaser2D>().ResetForCapture(player.transform);
        player.GetComponent<VideoPlayerAnimationDriver>()?.ResetVisual();
        presentation.ResetPresentation();vfx.ResetEffects();
        player.GetComponent<PlayerInputRouter>().enabled=true;
        Physics2D.SyncTransforms();
    }
    public void CombatTick()
    {
        if(!Running) return;
        long tick=CombatTimeController.TickCount;
        if(counter.IsActive && !counter.UsesBlinkApproach && counter.ElapsedFrames>0 && counter.ElapsedFrames<counter.ChargeFrame && counter.ElapsedFrames%3==0)
            vfx.EmitAfterimage(player.GetComponent<SpriteRenderer>(),player.GetComponent<PlayerMotor2D>().FacingSign);
        if(counter.IsActive&&counter.ElapsedFrames>=counter.ChargeReleaseFrame&&counter.ElapsedFrames<=counter.HitFrame&&counter.ElapsedFrames%2==0)
            vfx.EmitEyeTrail(player.GetComponent<SpriteRenderer>(),player.GetComponent<PlayerMotor2D>().FacingSign);
        if(counter.IsActive&&(counter.ElapsedFrames==counter.ChargeReleaseFrame||counter.ElapsedFrames==counter.ChargeReleaseFrame+2))
            vfx.EmitAfterimage(player.GetComponent<SpriteRenderer>(),player.GetComponent<PlayerMotor2D>().FacingSign);
        if(defenseCancelTake) { StepDefenseCancelTake(tick);return; }
        if(!attackSent && tick-startTick>=18)
        {
            var motor=player.GetComponent<PlayerMotor2D>();
            if(UsesSpacingApproaches&&Mathf.Abs(frontEnemy.transform.position.x-player.transform.position.x)>1.35f)
            {
                if(!openingApproachStarted) { openingApproachStarted=true;MarkEvent("OpeningApproach",frontEnemy); }
                motor.SetMoveInput(Vector2.right*(frontEnemy.transform.position.x>player.transform.position.x?1:-1));
            }
            else
            {
                motor.SetMoveInput(Vector2.zero);
                // Resume can suppress live edges for the current render frame.
                // Keep the staging request pending until gameplay actually accepts it.
                if(attack.RequestAttack())
                {
                    attackSent=true;
                    if(openingApproachStarted) MarkEvent("OpeningApproachEnd",frontEnemy);
                }
            }
        }
        if(attack.IsAttacking && attack.ComboStep<comboStages && attack.IsComboWindowOpen && queuedStep!=attack.ComboStep)
        { queuedStep=attack.ComboStep;attack.RequestAttack(); }
        var ai=rearEnemy.GetComponent<EnemyMeleeChaser2D>();
        if(UsesRearEntrance&&frontEnemy.HitCount>0&&!rearStarted)
        {
            var startup=player.GetComponent<VideoPlayerAnimationDriver>().Art.Find("Enemy_Windup");
            float windup=startup!=null?startup.TotalFrames*CombatTimeController.StepSeconds:.5f;
            rearStarted=true;ai.BeginCaptureApproachWithWindup(player.transform,rearEntranceSpeed,rearEntranceReach,windup);
            MarkEvent("RearApproach",rearEnemy);
        }
        if(!UsesRearEntrance&&!frontEnemy.IsAlive && !attack.IsAttacking && !rearStarted)
        { rearWindupTick=tick+12;rearStarted=true; }
        if(!UsesRearEntrance&&rearStarted && tick==rearWindupTick)
        {
            if(UsesSpacingApproaches) { ai.ResetForCapture(player.transform,false);MarkEvent("RearApproach",rearEnemy); }
            else { ai.BeginCaptureAttack();MarkEvent("RearWindup",rearEnemy);rearWindupMarked=true; }
        }
        if((UsesRearEntrance||UsesSpacingApproaches)&&rearStarted&&ai.IsWindingUp&&!rearWindupMarked)
        { rearWindupMarked=true;MarkEvent("RearWindup",rearEnemy); }
        int readyLead=player.GetComponent<VideoPlayerAnimationDriver>().Art.Find("ParryReady")!=null?9:5;
        if(ai.IsWindingUp && ai.WindupRemaining<=readyLead*CombatTimeController.StepSeconds && !parrySent)
        {
            parrySent=player.RequestAction(PlayerActionRequest.Parry);
            if(parrySent) MarkEvent("ParryReady",rearEnemy);
        }
        if(successTick>0 && tick-successTick>=10 && !counterSent)
        { counterSent=true;player.RequestAction(PlayerActionRequest.Attack); }
    }
    private void StepDefenseCancelTake(long tick)
    {
        var motor=player.GetComponent<PlayerMotor2D>();
        var ai=rearEnemy.GetComponent<EnemyMeleeChaser2D>();
        int openingHit=player.GetComponent<VideoPlayerAnimationDriver>().Art.Find("VerticalSlash").hitFrame;
        if(!rearStarted&&tick-startTick>=6+Mathf.Max(0,openingHit-10))
        { rearStarted=true;ai.BeginCaptureAttack();MarkEvent("RearWindup",rearEnemy); }
        if(!attackSent&&tick-startTick>=18)
        { attackSent=true;attack.RequestAttack(); }
        if(!parrySent&&attack.IsAttacking&&
            (attack.OpenCancelWindows&PlayerAttackCancelWindow.Defense)!=0&&
            ai.IsWindingUp&&ai.WindupRemaining<=5*CombatTimeController.StepSeconds)
        { parrySent=true;MarkEvent("AttackCancelRequest",rearEnemy);player.RequestAction(PlayerActionRequest.Parry); }
        if(parrySent&&!AttackWasCancelledForParry&&parry.IsParryActive&&
            !attack.IsAttacking&&player.PreviousActionState==PlayerActionState.Attack)
        { AttackWasCancelledForParry=true;MarkEvent("AttackCancelAccepted",rearEnemy); }
        if(successTick>0&&tick-successTick>=10&&!counterSent)
        { counterSent=true;player.RequestAction(PlayerActionRequest.Attack); }
        if(counterSent&&counterLanded&&!counter.IsActive&&!returnDashSent&&player.ActionState==PlayerActionState.Neutral)
        {
            int sign=MirrorLayout?-1:1;motor.SetMoveInput(Vector2.right*sign);
            returnDashSent=true;player.RequestAction(PlayerActionRequest.Dash);MarkEvent("ReturnDashRequest",frontEnemy);
            return;
        }
        if(returnDashSent&&motor.IsDashing)
        {
            if(!returnDashObserved) { returnDashObserved=true;MarkEvent("ReturnDash",frontEnemy); }
            if(tick%2==0) vfx.EmitAfterimage(player.GetComponent<SpriteRenderer>(),motor.FacingSign);
        }
        if(returnDashObserved&&!motor.IsDashing&&!returnAttackSent&&player.ActionState==PlayerActionState.Neutral)
        {
            if(Mathf.Abs(frontEnemy.transform.position.x-player.transform.position.x)>1.35f)
            { motor.SetMoveInput(Vector2.right*(frontEnemy.transform.position.x>player.transform.position.x?1:-1));return; }
            motor.SetMoveInput(Vector2.zero);returnAttackSent=true;MarkEvent("ReturnAttack",frontEnemy);attack.RequestAttack();
        }
        if(returnAttackSent&&attack.IsAttacking&&attack.ComboStep<comboStages&&attack.IsComboWindowOpen&&queuedStep!=attack.ComboStep)
        { queuedStep=attack.ComboStep;attack.RequestAttack(); }
    }
    public void StepCapture(double realSeconds)
    {
        if(clock==null) clock=FindFirstObjectByType<CombatTimeController>();
        if(!clock.ManualAdvanceOnly) throw new InvalidOperationException("Capture requires manual clock advancement.");
        if(!CombatTimeController.IsPaused) RealElapsed+=realSeconds;
        clock.AdvanceFrame(realSeconds);AdvanceVisuals((float)realSeconds);
    }
    private void LateUpdate()
    { if(clock!=null && !clock.ManualAdvanceOnly) { if(Running&&!CombatTimeController.IsPaused) RealElapsed+=Time.unscaledDeltaTime;AdvanceVisuals(Time.unscaledDeltaTime); } }
    private void AdvanceVisuals(float dt)
    {
        if(CombatTimeController.IsPaused) return;
        vfx.AdvanceVisuals(dt);presentation.AdvanceVisuals(dt);
        bool idleVisible=player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("PlayerVideo_Idle");
        if(Running && deathReal>=0 && RealElapsed-deathReal>=.85d && !counter.IsActive && (recovery==null||!recovery.IsPlaying)&&idleVisible)
        { Running=false;Completed=true;player.GetComponent<PlayerInputRouter>().enabled=true;player.ClearBufferedGameplayInput(); }
    }
    private void OnDisable()
    {
        if(Running&&player!=null)
        {
            player.GetComponent<PlayerMotor2D>().SetMoveInput(Vector2.zero);
            player.GetComponent<PlayerInputRouter>().enabled=true;player.ClearBufferedGameplayInput();
        }
        Running=false;CombatTimeController.Unregister(this);
        if(attack!=null) attack.HitConfirmed-=OnSlash;if(parry!=null) parry.Succeeded-=OnParry;
        if(frontEnemy!=null) frontEnemy.Died-=OnDeath;if(rearEnemy!=null) rearEnemy.Died-=OnDeath;
        if(counter!=null) { counter.HitConfirmed-=OnKick;counter.Finished-=OnCounterFinished;counter.Started-=OnCounterStarted;counter.ChargeStarted-=OnCharge;counter.ChargeReleased-=OnRelease;counter.Landed-=OnLanding; }
        if(recovery!=null) { recovery.Started-=OnRecoveryStarted;recovery.Ended-=OnRecoveryEnded; }
    }
}
