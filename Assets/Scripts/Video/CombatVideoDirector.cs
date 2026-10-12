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
    [SerializeField] private VideoCombatPresentation presentation;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private Vector3 playerStart=new Vector3(0,1,0);
    [SerializeField] private Vector3 frontStart=new Vector3(1.1f,1,0);
    [SerializeField] private Vector3 rearStart=new Vector3(-.85f,1,0);
    private CombatTimeController clock;
    private PlayerAttack2D attack;
    private PlayerParry2D parry;
    private PlayerParryCounter2D counter;
    private long startTick, rearWindupTick, successTick;
    private int queuedStep;
    private bool attackSent, rearStarted, parrySent, counterSent;
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
    public int CombatTickOrder=>-200;
    private void Awake()
    {
        attack=player.GetComponent<PlayerAttack2D>();parry=player.GetComponent<PlayerParry2D>();counter=player.GetComponent<PlayerParryCounter2D>();
    }
    private void OnEnable()
    { CombatTimeController.Register(this); }
    private void Start()
    {
        clock=FindFirstObjectByType<CombatTimeController>();
        attack.HitConfirmed+=OnSlash;parry.Succeeded+=OnParry;
        frontEnemy.Died+=OnDeath;rearEnemy.Died+=OnDeath;counter.HitConfirmed+=OnKick;counter.Finished+=OnCounterFinished;
        counter.ChargeStarted+=OnCharge;counter.ChargeReleased+=OnRelease;counter.Landed+=OnLanding;
        ResetTake(comboStages);
    }
    private void MarkEvent(string kind,Damageable2D target)
    { Marks.Add(new Mark {kind=kind,target=target!=null?target.IndividualId:"",realSeconds=RealElapsed,tick=CombatTimeController.TickCount-startTick}); }
    private void OnSlash(Damageable2D target,int step,Vector2 point)
    {
        int direction=player.GetComponent<PlayerMotor2D>().FacingSign;
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyHitback(new[]{.08f,.14f,.24f,.4f}[step-1],6+step,Vector2.right*direction);
        MarkEvent("Slash"+step,target);vfx.Emit("Slash",point,new[]{.5f,.6f,.72f,.9f}[step-1],direction);
        presentation.Shake(.045f,.025f);
    }
    private void OnParry(Damageable2D target,ParryResult result)
    {
        successTick=CombatTimeController.TickCount;MarkEvent("Parry",target);
        if(target!=null) vfx.Emit("Parry",Vector2.Lerp(player.transform.position,target.transform.position,.45f)+Vector2.up*.12f,1.05f,player.GetComponent<PlayerMotor2D>().FacingSign);
        presentation.Shake(.06f,.04f);
    }
    private void OnKick(Damageable2D target,Vector2 point)
    {
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyHitback(.65f,10,Vector2.right*player.GetComponent<PlayerMotor2D>().FacingSign);
        MarkEvent("Kick",target);
    }
    private void OnCharge()=>MarkEvent("ChargeStart",counter.SelectedTarget);
    private void OnRelease()=>MarkEvent("ChargeRelease",counter.SelectedTarget);
    private void OnLanding()=>MarkEvent("Landing",counter.SelectedTarget);
    private void OnCounterFinished(bool hit)
    {
        if(!hit && Running)
        {
            Running=false;
            if(player!=null) { var router=player.GetComponent<PlayerInputRouter>();if(router!=null) router.enabled=true;player.ClearBufferedGameplayInput(); }
        }
    }
    private void OnDeath(Damageable2D target,Vector2 direction)
    { MarkEvent("Death",target);vfx.EmitFollowing("Vanish",target.transform,Vector2.down*.2f,.6f,.18f);if(target==rearEnemy) deathReal=RealElapsed; }
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
        CombatTimeController.ResetSession();
        player.ResetToSafeState("VideoReset");
        player.transform.position = playerStart;
        int layoutSign=MirrorLayout ? -1 : 1;
        player.GetComponent<PlayerMotor2D>().SetFacing(layoutSign);
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        player.GetComponent<PlayerSpecialGauge>().ResetGauge();
        var frontPosition=frontStart;frontPosition.x=playerStart.x+(frontStart.x-playerStart.x)*layoutSign;
        var rearPosition=rearStart;rearPosition.x=playerStart.x+(rearStart.x-playerStart.x)*layoutSign;
        frontEnemy.ResetForCapture(frontPosition, comboStages == 4 ? 7 : 2);
        rearEnemy.ResetForCapture(rearPosition, 3);
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
        if(counter.IsActive && counter.ElapsedFrames>0 && counter.ElapsedFrames<18 && counter.ElapsedFrames%3==0)
            vfx.EmitAfterimage(player.GetComponent<SpriteRenderer>(),player.GetComponent<PlayerMotor2D>().FacingSign);
        if(counter.IsActive&&counter.ElapsedFrames>=counter.ChargeReleaseFrame&&counter.ElapsedFrames<=counter.HitFrame&&counter.ElapsedFrames%2==0)
            vfx.EmitEyeTrail(player.GetComponent<SpriteRenderer>(),player.GetComponent<PlayerMotor2D>().FacingSign);
        if(!attackSent && tick-startTick>=18) { attackSent=true;attack.RequestAttack(); }
        if(attack.IsAttacking && attack.ComboStep<comboStages && attack.IsComboWindowOpen && queuedStep!=attack.ComboStep)
        { queuedStep=attack.ComboStep;attack.RequestAttack(); }
        if(!frontEnemy.IsAlive && !attack.IsAttacking && !rearStarted)
        { rearWindupTick=tick+12;rearStarted=true; }
        var ai=rearEnemy.GetComponent<EnemyMeleeChaser2D>();
        if(rearStarted && tick==rearWindupTick) { ai.BeginCaptureAttack();MarkEvent("RearWindup",rearEnemy); }
        if(ai.IsWindingUp && ai.WindupRemaining<=5*CombatTimeController.StepSeconds && !parrySent)
        { parrySent=true;player.RequestAction(PlayerActionRequest.Parry); }
        if(successTick>0 && tick-successTick>=10 && !counterSent)
        { counterSent=true;player.RequestAction(PlayerActionRequest.Attack); }
    }
    public void StepCapture(double realSeconds)
    {
        if(clock==null) clock=FindFirstObjectByType<CombatTimeController>();
        if(!clock.ManualAdvanceOnly) throw new InvalidOperationException("Capture requires manual clock advancement.");
        RealElapsed+=realSeconds;clock.AdvanceFrame(realSeconds);AdvanceVisuals((float)realSeconds);
    }
    private void LateUpdate()
    { if(clock!=null && !clock.ManualAdvanceOnly) { if(Running) RealElapsed+=Time.unscaledDeltaTime;AdvanceVisuals(Time.unscaledDeltaTime); } }
    private void AdvanceVisuals(float dt)
    {
        vfx.AdvanceVisuals(dt);presentation.AdvanceVisuals(dt);
        if(Running && deathReal>=0 && RealElapsed-deathReal>=.85d && !counter.IsActive)
        { Running=false;Completed=true;player.GetComponent<PlayerInputRouter>().enabled=true;player.ClearBufferedGameplayInput(); }
    }
    private void OnDisable()
    {
        Running=false;CombatTimeController.Unregister(this);
        if(attack!=null) attack.HitConfirmed-=OnSlash;if(parry!=null) parry.Succeeded-=OnParry;
        if(frontEnemy!=null) frontEnemy.Died-=OnDeath;if(rearEnemy!=null) rearEnemy.Died-=OnDeath;
        if(counter!=null) { counter.HitConfirmed-=OnKick;counter.Finished-=OnCounterFinished;counter.ChargeStarted-=OnCharge;counter.ChargeReleased-=OnRelease;counter.Landed-=OnLanding; }
    }
}
