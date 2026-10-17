using System;
using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Free-play filming stage. Presets manage their own roots; hand-placed content is retained.</summary>
[DefaultExecutionOrder(12000)]
public sealed class CombatVideoStudio : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private PlayerStateMachine player;
    [SerializeField] private VideoCombatPresentation presentation;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private Camera studioCamera;
    [SerializeField] private GameObject enemyPrefab, gimmickPrefab;
    [SerializeField] private Transform enemiesRoot, gimmicksRoot, customContent;
    [SerializeField] private StudioPreset[] presets;
    [SerializeField] private StudioPreset currentPreset;
    [SerializeField] private List<StudioEnemyActor> enemies = new();
    [SerializeField] private List<StudioStageGimmick> gimmicks = new();
    [SerializeField] private Vector2 playerHome = Vector2.up;
    [SerializeField] private bool faceLeft;
    [SerializeField] private bool followCamera = true;
    [SerializeField] private float cameraSize = 2.109375f;
    [SerializeField] private Vector2 fixedCameraCenter = new Vector2(0, 1.05f);
    [SerializeField] private bool showDiagnostics;
    private CombatTimeController clock;
    private PlayerAttack2D attack;
    private PlayerParry2D parry;
    private PlayerParryCounter2D counter;
    private PlayerMotor2D motor;
    private StudioResetAnchor[] customAnchors = Array.Empty<StudioResetAnchor>();
    [SerializeField] private int nextEnemyId = 1;
    private bool comboPreview, counterPreview, subscribed, previousRunInBackground;
    private StudioEnemyActor parryPreview;
    private long previewStartTick, parriedTick = -1;

    public PlayerStateMachine Player => player;
    public VideoCombatPresentation Presentation => presentation;
    public VideoVfxRenderer Vfx => vfx;
    public Camera StudioCamera => studioCamera;
    public StudioPreset[] Presets => presets;
    public StudioPreset CurrentPreset => currentPreset;
    public IReadOnlyList<StudioEnemyActor> Enemies => enemies;
    public IReadOnlyList<StudioStageGimmick> Gimmicks => gimmicks;
    public Transform CustomContent => customContent;
    public bool FollowCamera { get => followCamera; set => followCamera = value; }
    public bool ShowDiagnostics { get => showDiagnostics; set => showDiagnostics = value; }
    public float CameraSize { get => cameraSize; set => cameraSize = Mathf.Clamp(value, .84375f, 8.4375f); }
    public Vector2 FixedCameraCenter { get => fixedCameraCenter; set => fixedCameraCenter=value; }
    public string LastEvent { get; private set; } = "準備完了";
    public double RealElapsed { get; private set; }
    public bool PreviewRunning => comboPreview || parryPreview != null;
    public int CombatTickOrder => -200;
    public event Action<double> FramePresented;
    public event Action CaptureToggleRequested;
    public event Action ScreenshotRequested;

    private void OnEnable()
    {
        if (!Application.isPlaying || player == null) return;
        previousRunInBackground=Application.runInBackground;Application.runInBackground=true;
        clock=FindFirstObjectByType<CombatTimeController>();
        attack=player.GetComponent<PlayerAttack2D>(); parry=player.GetComponent<PlayerParry2D>();
        counter=player.GetComponent<PlayerParryCounter2D>(); motor=player.GetComponent<PlayerMotor2D>();
        attack.HitConfirmed+=OnSlash; parry.Succeeded+=OnParry; counter.HitConfirmed+=OnKick;
        foreach (var enemy in enemies) if (enemy != null) enemy.Health.Died+=OnDeath;
        subscribed=true; CombatTimeController.Register(this);
    }
    private void Start()
    {
        // The authored Scene is the starting layout. Reapplying the source asset here
        // would discard positions/HP edited in Scene View or the studio panel before Play.
        playerHome=player.transform.position;
        foreach (var enemy in enemies) if (enemy != null)
        { enemy.CaptureHome();enemy.Configure(enemy.Setup,player.transform); }
        foreach (var gimmick in gimmicks) if (gimmick != null)
        { gimmick.CaptureHome();gimmick.Configure(gimmick.Setup,player); }
        ResetStudio();
    }

    public void ApplyPreset(StudioPreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));
        if (Application.isPlaying) ResetStudio(); // Release a counter's target before replacing actors.
        ClearManaged(enemiesRoot); ClearManaged(gimmicksRoot);
        enemies.Clear(); gimmicks.Clear(); nextEnemyId=1;
        currentPreset=preset; playerHome=preset.playerPosition; faceLeft=preset.faceLeft;
        followCamera=preset.followCamera; CameraSize=preset.cameraSize;
        fixedCameraCenter=preset.cameraCenter;
        foreach (var setup in preset.enemies) if (setup != null) AddEnemy(setup);
        foreach (var setup in preset.gimmicks) if (setup != null) AddGimmick(setup);
        if (Application.isPlaying) ResetStudio();
        else
        {
            player.transform.position=playerHome;
            player.GetComponent<SpriteRenderer>().flipX=faceLeft;
            studioCamera.transform.position=new Vector3(fixedCameraCenter.x,fixedCameraCenter.y,-10);
            studioCamera.orthographicSize=cameraSize;
        }
        LastEvent="セット: "+preset.displayName;
    }

    private static void ClearManaged(Transform root)
    {
        for (int i=root.childCount-1; i>=0; i--) RemoveObject(root.GetChild(i).gameObject);
    }
    private static void RemoveObject(GameObject value)
    {
        if (Application.isPlaying) { value.SetActive(false); Destroy(value); }
        else
        {
#if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(value);
#else
            DestroyImmediate(value);
#endif
        }
    }
    public StudioEnemyActor AddEnemy(StudioEnemySetup setup)
    {
        GameObject source=setup.prefab != null ? setup.prefab : enemyPrefab;
        if (source == null || source.GetComponent<StudioEnemyActor>() == null)
            throw new ArgumentException("敵PrefabにはStudioEnemyActorと近接AIが必要です。",nameof(setup));
        GameObject instance;
#if UNITY_EDITOR
        instance=!Application.isPlaying?(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source,enemiesRoot):Instantiate(source,enemiesRoot);
#else
        instance=Instantiate(source,enemiesRoot);
#endif
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(instance,"Add studio enemy");
#endif
        string identifier;
        do { identifier=$"StudioEnemy_{nextEnemyId++:D3}"; }
        while(enemies.Exists(existing=>existing!=null&&existing.name==identifier));
        instance.name=identifier;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
#endif
        var actor=instance.GetComponent<StudioEnemyActor>(); actor.Configure(setup,player.transform);
        actor.Health.Died+=OnDeath; enemies.Add(actor);
        return actor;
    }
    public StudioStageGimmick AddGimmick(StudioGimmickSetup setup)
    {
        GameObject instance;
#if UNITY_EDITOR
        instance=!Application.isPlaying?(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(gimmickPrefab,gimmicksRoot):Instantiate(gimmickPrefab,gimmicksRoot);
#else
        instance=Instantiate(gimmickPrefab,gimmicksRoot);
#endif
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(instance,"Add studio gimmick");
#endif
        instance.name=setup.kind.ToString();
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
#endif
        var gimmick=instance.GetComponent<StudioStageGimmick>();
        gimmick.Configure(setup,player); gimmicks.Add(gimmick); return gimmick;
    }
    public void RemoveEnemy(StudioEnemyActor enemy)
    {
        if (enemy == null || !enemies.Contains(enemy)) return;
        if (Application.isPlaying) ResetStudio();
        enemy.Health.Died-=OnDeath; enemies.Remove(enemy); RemoveObject(enemy.gameObject);
    }
    public void RemoveGimmick(StudioStageGimmick gimmick)
    {
        if (gimmick == null || !gimmicks.Contains(gimmick)) return;
        if (Application.isPlaying) ResetStudio();
        gimmicks.Remove(gimmick); RemoveObject(gimmick.gameObject);
    }

    public void ResetStudio()
    {
        comboPreview=false; parryPreview=null; parriedTick=-1; RealElapsed=0;
        if (!Application.isPlaying || player == null) return;
        CombatTimeController.ResetSession();
        player.ResetToSafeState("StudioReset"); player.transform.position=playerHome;
        var body=player.GetComponent<Rigidbody2D>(); body.position=playerHome; body.linearVelocity=Vector2.zero;
        motor.SetFacing(faceLeft?-1:1);
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        player.GetComponent<PlayerSpecialGauge>().ResetGauge();
        player.GetComponent<PlayerHeal2D>()?.Refill();
        foreach (var enemy in enemies) if (enemy != null) enemy.ResetActor();
        foreach (var gimmick in gimmicks) if (gimmick != null) gimmick.ResetGimmick();
        customAnchors=customContent.GetComponentsInChildren<StudioResetAnchor>(true);
        foreach (var anchor in customAnchors) anchor.Restore(player.transform);
        player.GetComponent<VideoPlayerAnimationDriver>().ResetVisual();
        presentation.ResetPresentation(); vfx.ResetEffects(); UpdateCameraFrame();
        var router=player.GetComponent<PlayerInputRouter>(); router.enabled=true; router.ClearGameplayInput();
        Physics2D.SyncTransforms(); LastEvent="リセット完了";
    }
    public void CaptureResetPoint()
    {
        playerHome=player.transform.position;
        if (motor != null) faceLeft=motor.FacingSign<0;
        foreach (var enemy in enemies) if (enemy != null) enemy.CaptureHome();
        foreach (var gimmick in gimmicks) if (gimmick != null) gimmick.CaptureHome();
        foreach (var anchor in customContent.GetComponentsInChildren<StudioResetAnchor>(true)) anchor.Capture();
        LastEvent="現在位置をリセット位置に登録";
    }
    public StudioPreset SnapshotPreset()
    {
        var snapshot=ScriptableObject.CreateInstance<StudioPreset>();
        snapshot.displayName=currentPreset != null ? currentPreset.displayName+" コピー" : "カスタムセット";
        snapshot.playerPosition=playerHome; snapshot.faceLeft=faceLeft;
        snapshot.followCamera=followCamera; snapshot.cameraSize=cameraSize;
        snapshot.cameraCenter=fixedCameraCenter;
        var enemyData=new List<StudioEnemySetup>(); var gimmickData=new List<StudioGimmickSetup>();
        foreach (var enemy in enemies) if (enemy != null)
        { var copy=enemy.Setup.Copy(); if (!Application.isPlaying) copy.position=enemy.transform.position; enemyData.Add(copy); }
        foreach (var gimmick in gimmicks) if (gimmick != null)
        { var copy=gimmick.Setup.Copy(); if (!Application.isPlaying) copy.position=gimmick.transform.position; gimmickData.Add(copy); }
        snapshot.enemies=enemyData.ToArray(); snapshot.gimmicks=gimmickData.ToArray(); return snapshot;
    }
    public void MirrorLayout()
    {
        if (Application.isPlaying) ResetStudio();
        faceLeft=!faceLeft;
        foreach (var enemy in enemies) if (enemy != null)
        {
            var setup=enemy.Setup.Copy();setup.position.x=2*playerHome.x-setup.position.x;
            enemy.Configure(setup,player.transform);
        }
        foreach (var gimmick in gimmicks) if (gimmick != null)
        {
            var setup=gimmick.Setup.Copy();setup.position.x=2*playerHome.x-setup.position.x;setup.travel.x=-setup.travel.x;
            gimmick.Configure(setup,player);
        }
        if (Application.isPlaying) ResetStudio();
        else player.GetComponent<SpriteRenderer>().flipX=faceLeft;
        LastEvent="セットの左右を反転";
    }
    public bool RequestAction(PlayerActionRequest action)
    {
        if (!Application.isPlaying) return false;
        bool accepted=action==PlayerActionRequest.Attack?attack.RequestAttack():player.RequestAction(action);
        LastEvent=accepted?"入力: "+action:"入力不可: "+action; return accepted;
    }
    public void BeginComboPreview()
    { previewStartTick=CombatTimeController.TickCount; comboPreview=RequestAction(PlayerActionRequest.Attack); }
    public bool BeginParryPreview(StudioEnemyActor enemy, bool withCounter)
    {
        if (!Application.isPlaying || enemy == null) return false;
        if (Vector2.Distance(player.transform.position,enemy.transform.position)>1.85f)
        { LastEvent="パリィ用の敵を主人公から1.8以内へ配置してください";return false; }
        if (!enemy.AttackOnce()) { LastEvent="敵の攻撃・ひるみが終わってから試してください";return false; }
        parryPreview=enemy; counterPreview=withCounter; parriedTick=-1;
        previewStartTick=CombatTimeController.TickCount; LastEvent="実判定でパリィを試行"; return true;
    }
    public void RefillPlayer()
    {
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        var gauge=player.GetComponent<PlayerSpecialGauge>(); gauge.AddOnAttackHit(gauge.MaxGauge);
        player.GetComponent<PlayerHeal2D>()?.Refill(); LastEvent="HP・ゲージ・回復回数を補充";
    }
    public void Face(int sign) => player.GetComponent<PlayerMotor2D>().SetFacing(sign);

    public void CombatTick()
    {
        foreach (var enemy in enemies) if (enemy != null) enemy.Step();
        if (comboPreview && attack.IsAttacking && attack.IsComboWindowOpen)
        {
            if (attack.ComboStep<attack.MaxComboStep) attack.RequestAttack();
            else comboPreview=false;
        }
        if (comboPreview && !attack.IsAttacking && CombatTimeController.TickCount>previewStartTick+90) comboPreview=false;
        if (parryPreview != null)
        {
            if (parryPreview.AI.IsWindingUp && parryPreview.AI.WindupRemaining<=5*CombatTimeController.StepSeconds && !parry.IsParryActive && parriedTick<0)
                player.RequestAction(PlayerActionRequest.Parry);
            if (parriedTick>=0 && CombatTimeController.TickCount-parriedTick>=10)
            {
                if (counterPreview) RequestAction(PlayerActionRequest.Attack);
                parryPreview=null;
            }
            if (CombatTimeController.TickCount-previewStartTick>180) parryPreview=null;
        }
        if (counter.IsActive && counter.ElapsedFrames>=counter.ChargeReleaseFrame && counter.ElapsedFrames<=counter.HitFrame && counter.ElapsedFrames%2==0)
            vfx.EmitEyeTrail(player.GetComponent<SpriteRenderer>(),motor.FacingSign);
        if (player.transform.position.y < -4 || Mathf.Abs(player.transform.position.x)>21)
            ResetStudio();
    }
    private void OnSlash(Damageable2D target,int step,Vector2 point)
    {
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyHitback(new[]{.1f,.18f,.3f,.58f}[step-1],6+step,Vector2.right*motor.FacingSign);
        if(step!=1||!player.GetComponent<VideoPlayerAnimationDriver>().Art.Find("VerticalSlash").provisional)
            vfx.Emit("Slash",point,new[]{.65f,.8f,1f,1.25f}[step-1],motor.FacingSign);
        presentation.ShakeDirectional(.09f+step*.01f,.025f+step*.02f,new Vector2(motor.FacingSign,step==1?-1:.15f));
        LastEvent=$"{target.IndividualId} / {step}段目 / HP {target.CurrentHp}";
    }
    private void OnParry(Damageable2D target,ParryResult result)
    {
        if (parryPreview != null && parryPreview.Health==target) parriedTick=CombatTimeController.TickCount;
        vfx.Emit("Parry",player.GetComponent<VideoPlayerAnimationDriver>().ParryContactPoint,.85f,motor.FacingSign);
        presentation.PresentLightParry(motor.FacingSign); LastEvent="パリィ: "+target.IndividualId+" / "+result;
    }
    private void OnKick(Damageable2D target,Vector2 point)
    {
        target.GetComponent<VideoEnemyAnimationDriver>()?.ApplyLaunch(counter.IsOverheadHeelDrop?1.4f:1.75f,counter.IsOverheadHeelDrop?.18f:1.05f,20,Vector2.right*motor.FacingSign);
        LastEvent="反撃: "+target.IndividualId;
    }
    private void OnDeath(Damageable2D target,Vector2 direction) => vfx.EmitFollowing("Vanish",target.transform,Vector2.down*.2f,.6f,.18f);

    private void Update()
    {
        var keys=Keyboard.current; if (keys==null) return;
        if (keys.f1Key.wasPressedThisFrame) showDiagnostics=!showDiagnostics;
        if (keys.f2Key.wasPressedThisFrame) ResetStudio();
        if (keys.f3Key.wasPressedThisFrame && enemies.Count>0) enemies[0]?.AttackOnce();
        if (keys.f8Key.wasPressedThisFrame) ScreenshotRequested?.Invoke();
        if (keys.f9Key.wasPressedThisFrame) CaptureToggleRequested?.Invoke();
    }
    private void LateUpdate()
    { if (clock != null && !clock.ManualAdvanceOnly) AdvanceVisuals(Time.unscaledDeltaTime); }
    public void RefreshCameraFrame() => UpdateCameraFrame();
    private void UpdateCameraFrame()
    {
        Vector2 center=followCamera?new Vector2(player.transform.position.x,Mathf.Max(1.05f,player.transform.position.y+.05f)):fixedCameraCenter;
        presentation.SetCameraFrame(new Vector3(center.x,center.y,-10),cameraSize);
    }
    private void AdvanceVisuals(double seconds)
    {
        RealElapsed+=seconds; UpdateCameraFrame();
        vfx.AdvanceVisuals((float)seconds); presentation.AdvanceVisuals((float)seconds);
        FramePresented?.Invoke(seconds);
    }
    public void StepStudio(double seconds)
    {
        if (clock==null || !clock.ManualAdvanceOnly) throw new InvalidOperationException("Manual combat clock required.");
        clock.AdvanceFrame(seconds); AdvanceVisuals(seconds);
    }
    private void OnGUI()
    {
        if (!showDiagnostics) return;
        GUI.Box(new Rect(12,12,470,110),"VIDEO STUDIO / F1: hide");
        GUI.Label(new Rect(24,40,450,26),$"{player.ActionState} / HP {player.GetComponent<PlayerDamageReceiver2D>().CurrentHp} / {LastEvent}");
        GUI.Label(new Rect(24,68,450,24),"F2 reset   F3 first enemy attack   F8 still   F9 record");
        GUI.Label(new Rect(24,94,450,24),$"Tick {CombatTimeController.TickCount} / Slow {CombatTimeController.SlowScale:0.00}");
    }
    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        if (!subscribed) return;
        attack.HitConfirmed-=OnSlash; parry.Succeeded-=OnParry; counter.HitConfirmed-=OnKick;
        foreach (var enemy in enemies) if (enemy != null) enemy.Health.Died-=OnDeath;
        subscribed=false;
        Application.runInBackground=previousRunInBackground;
        if (player != null) player.ResetToSafeState("StudioDisabled");
        if (presentation != null) presentation.ResetPresentation();
        if (vfx != null) vfx.ResetEffects();
    }
}
