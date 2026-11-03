using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Counter-only drawing/time owner. The shared combat feedback remains the sole camera transform owner.</summary>
[DefaultExecutionOrder(12500), DisallowMultipleComponent]
public sealed class PrototypeCounterPresentation : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private PlayerParryCounter2D counter;
    [SerializeField] private PrototypePlayerArtPresentation playerArt;
    [SerializeField] private CombatFeedback2D feedback;
    [SerializeField] private VideoVfxRenderer effects;
    [SerializeField] private Camera gameplayCamera;
    private SpriteRenderer source, dimmer, lift, eyes;
    private Sprite solid;
    private Material liftMaterial;
    private CombatTimeController clock;
    private readonly List<EnemyCounterLaunch2D> launchedEnemies = new();
    private bool charged, hidden;
    private float dimBlend, trailAge;
    private VideoParryBacklight parryLight;
    private ParryOrangeImpact orangeImpact;
    private VideoCounterLighting counterLight;
    private PlayerStateMachine state;
    private PlayerParry2D parry;
    private Damageable2D parrySource;
    private bool counterLightReady;
    private float parryLightAge = -1, chargeRealElapsed;
    private bool parrySequenceActive, parryImpactPresented, queuedParryCounter;
    private long parryStartTick;
    private int parryLeadFrames;
    private ParryResult parryVisualResult;
    private PrototypeEnemyArtPresentation parryEnemyArt;
    private float parryRealTime, nextParryLightTime;
    private float parryJitterAge = -1;
    private EnemyCounterLaunch2D parryRetreatMotor;
    private bool parryEnemyRecoilPose;
    private int parryJitterFacing;
    private bool parryJitterFresh;
    private Damageable2D parryJitterTarget;
    private PrototypeEnemyArtPresentation parryJitterArt;
    private EnemyCombatCue2D parryJitterCue;
    private Transform parryCameraAnchor;
    private Damageable2D parryCameraTarget;
    private bool parryCameraActive, parryCameraImpacted, parryCameraReturning, parryCameraHoldFresh;
    private float parryCameraHoldRemaining = -1;
    private Vector3 parryCameraStartPosition;
    private float parryCameraStartSize, parryCameraReturnAge;
    private bool counterCameraBridgeActive;
    private Vector3 counterCameraBridgeFrom;
    private float counterCameraBridgeFromSize, counterCameraBridgeAge;
    private long parryCounterCameraPendingUntilTick = -1;
    private CounterCinematicProfile Profile => counter != null ? counter.CinematicProfile : null;
    private float FlashStrength => feedback != null && feedback.Settings != null ? Mathf.Clamp01(feedback.Settings.flash) : 1;
    private float VfxStrength => feedback != null && feedback.Settings != null ? Mathf.Clamp01(feedback.Settings.vfx) : 1;
    private float ShakeStrength => feedback != null && feedback.Settings != null ? Mathf.Clamp01(feedback.Settings.shake) : 1;
    public int CombatTickOrder => 400;
    public float DimAlpha => dimmer != null && dimmer.enabled ? dimmer.color.a : 0;
    public bool IsCharged => charged;
    public bool IsBodyHidden => hidden;
    public bool IsSpotlightVisible => lift != null && lift.enabled;
    public int ActiveEffectCount => effects != null ? effects.ActiveCount : 0;
    public bool UsesEnhancedPresentation => isActiveAndEnabled && Profile != null && Profile.enhancedPresentation;
    public float ParrySuccessNormalizedStart => UsesEnhancedPresentation && parry.LastReactionWeight == ParryReactionWeight.Light ? Profile.parrySuccessStartNormalized : 0;
    public int ParryImpactCount { get; private set; }
    public long ParryImpactTick { get; private set; } = -1;
    public bool IsParryBacklit => parryLight != null && parryLight.IsVisible;
    public bool IsParrySequenceActive => parrySequenceActive;
    public bool IsParryImpactPending => parrySequenceActive && !parryImpactPresented;
    public bool HasQueuedParryCounter => queuedParryCounter;
    public int ParryImpactDelayFrames => parryLeadFrames;
    public bool IsParryActorJitterActive => parryJitterAge >= 0 && Profile != null && Profile.parryActorJitterPixels > 0;
    public float ParryActorJitterX => playerArt != null ? playerArt.ImpactOffsetX : 0;
    public int OrangeParryEffectCount => orangeImpact != null ? orangeImpact.ActiveRendererCount : 0;
    public bool IsOrangeParryGlowVisible => orangeImpact != null && orangeImpact.IsGlowVisible;
    public bool IsOrangeParrySparkVisible => orangeImpact != null && orangeImpact.IsSparkVisible;
    public bool IsParryFlareVisible => orangeImpact != null && orangeImpact.IsFlareVisible;
    public int ParryParticleCount => orangeImpact != null ? orangeImpact.ParticleCount : 0;
    public bool IsParryCameraActive => parryCameraActive;
    public bool IsCounterCameraBridgeActive => counterCameraBridgeActive;
    private bool CanBridgeQueuedCounter => Profile != null && Profile.parryCounterCameraBridge && counter != null &&
        ((queuedParryCounter && ParrySourceIsValid && counter.EligibilityReason(parrySource) == "Eligible") ||
        (CombatTimeController.TickCount < parryCounterCameraPendingUntilTick && parryCameraTarget != null &&
        parryCameraTarget.IsParryStaggered && counter.EligibilityReason(parryCameraTarget) == "Eligible"));
    public string ParryCameraPhase => !parryCameraActive ? "None" : CanBridgeQueuedCounter ? "CounterLink" : parryCameraReturning ? "Returning" :
        parryCameraHoldRemaining >= 0 ? "Hold" : parryCameraImpacted ? "Impact" : "Preparation";
    public Vector2 ParryCameraCenter => parryCameraAnchor != null ? (Vector2)parryCameraAnchor.position : Vector2.zero;
    public string ParryPresentationPhase => !parrySequenceActive ? "None" : !parryImpactPresented ? "Preparation" :
        CombatTimeController.TickCount == ParryImpactTick ? "Impact" : "Deflection";
    public string CounterLightPhase => counterLight != null ? counterLight.Phase : "None";
    public int CounterLightFrame => counterLight != null ? counterLight.ImpactFrame : -1;
    public float CounterRimPulse => counterLight != null ? counterLight.RimPulse : 0;
    public void Configure(PlayerParryCounter2D action, PrototypePlayerArtPresentation art, CombatFeedback2D common, VideoVfxRenderer vfx, Camera camera)
    { counter = action; playerArt = art; feedback = common; effects = vfx; gameplayCamera = camera; }
    private void Awake()
    {
        source = GetComponent<SpriteRenderer>(); clock = Object.FindFirstObjectByType<CombatTimeController>();
        state = GetComponent<PlayerStateMachine>(); parry = GetComponent<PlayerParry2D>();
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        EnsureDrawing();
    }
    private void OnEnable()
    {
        if (counter == null || Profile == null) return;
        counter.Started += OnStarted; counter.BlinkStarted += OnBlink; counter.BlinkArrived += RestoreBody;
        counter.ChargeStarted += OnCharge; counter.ChargeReleased += OnRelease;
        counter.HitConfirmed += OnHit; counter.HitMissed += OnRelease; counter.Finished += OnFinished;
        CombatTimeController.SessionReset += ResetPresentation; SceneManager.activeSceneChanged += OnSceneChanged;
        CombatTimeController.Register(this);
    }
    private void OnStarted()
    {
        parryCounterCameraPendingUntilTick = -1;
        counterCameraBridgeActive = false;
        if (parryCameraActive && feedback != null && feedback.TransferCameraFocus(orangeImpact, this))
        {
            if (Profile.parryCounterCameraBridge && ShakeStrength > 0)
            {
                float strength = ShakeStrength;
                // Undo accessibility scaling here; shared feedback applies it exactly once to the bridge pose.
                counterCameraBridgeFrom = parryCameraStartPosition +
                    (gameplayCamera.transform.position - feedback.ShakeOffset - parryCameraStartPosition) / strength;
                counterCameraBridgeFromSize = parryCameraStartSize + (gameplayCamera.orthographicSize - parryCameraStartSize) / strength;
                counterCameraBridgeAge = 0; counterCameraBridgeActive = true;
                feedback.SetCameraFocusPose(this, counterCameraBridgeFrom, counterCameraBridgeFromSize);
            }
            parryCameraActive = false; parryCameraTarget = null; parryCameraHoldRemaining = -1;
        }
        else CancelParryCamera(true);
        launchedEnemies.RemoveAll(enemy => enemy == null || !enemy.IsActive); SampleCounter();
    }
    public void CombatTick()
    {
        if (!parrySequenceActive || !parryImpactPresented || parryRetreatMotor == null ||
            !ParrySourceIsValid || state.ActionState != TechnicalSwordAction.PlayerState.PlayerActionState.ParrySuccess) return;
        int frame = (int)(CombatTimeController.TickCount - ParryImpactTick);
        int duration = Profile.parryFollowThroughFrames;
        if (frame <= 0 || frame > duration) return;
        float current = 1 - frame / (float)duration;
        float previous = 1 - (frame - 1) / (float)duration;
        float distance = Profile.parryEnemyReleasePixels;
        float stepPixels = Mathf.Round(distance * (1 - current * current)) - Mathf.Round(distance * (1 - previous * previous));
        parryRetreatMotor.MoveParryRetreat(stepPixels * parryJitterFacing / 32f);
    }
    public void CombatTickTimers()
    {
        SampleCounter();
        if (!UsesEnhancedPresentation || source == null || state == null) return;
        if (parrySequenceActive) { SampleAdaptiveParry(); return; }
        if (state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.Parry ||
            state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.ParryFail)
            source.sprite = Profile.parryWaitSprite;
        // Hit resolution precedes Animator.Update. Preserve the success cel at the end of this tick too.
        else if (state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.ParrySuccess && ParryImpactTick == CombatTimeController.TickCount)
            source.sprite = Profile.parryContactSprite;
    }
    public bool TryPresentParryImpact(Damageable2D target, ParryResult result)
    {
        if (!UsesEnhancedPresentation || Profile.parryContactSprite == null || parry == null ||
            parry.LastReactionWeight != ParryReactionWeight.Light || target == null || feedback == null || !feedback.IsReady)
            return false;
        if (Profile.synchronizedParryImpact && Profile.ParryReaction != null)
        {
            ClearParrySequence(false);
            parrySource = target; parryVisualResult = result; parryStartTick = CombatTimeController.TickCount;
            parrySequenceActive = true; parryImpactPresented = false; parryLeadFrames = 0;
            parryEnemyArt = target.GetComponent<PrototypeEnemyArtPresentation>();
            if (parryEnemyArt != null && !parryEnemyArt.TryBeginParryDeflection(this, Profile.parryApproachMaxFrames, out parryLeadFrames)) parryEnemyArt = null;
            parry.EnsureSuccessLockFrames(parryLeadFrames + Profile.parryFollowThroughFrames + 1);
            BeginParryCamera();
            SampleAdaptiveParry(); return true;
        }
        parryLeadFrames = 0;
        parrySource = target; ParryImpactCount++; ParryImpactTick = CombatTimeController.TickCount;
        source.sprite = Profile.parryContactSprite;
        playerArt.RefreshVisual();int facing = GetComponent<PlayerMotor2D>().FacingSign;
        Vector2 contact = playerArt.Artwork.transform.TransformPoint(new Vector3(facing*Profile.parryContactOffset.x,Profile.parryContactOffset.y));
        CombatTimeController.RequestHitstop(this,(result==ParryResult.Just ? Profile.justParryHitstopFrames : Profile.parryHitstopFrames)/60f);
        feedback.PresentAuthoredImpact(result==ParryResult.Just ? CombatFeedbackKind.JustParry : CombatFeedbackKind.Parry,contact,parrySource);
        feedback.ShakeDirectional(9f/60,.22f,new Vector2(-facing,-.3f),1);
        parryLight?.Begin(parrySource,contact);parryLightAge = 0;
        return true;
    }
    public bool TryQueueParryCounter()
    {
        if (!parrySequenceActive || state == null || state.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Alive ||
            parrySource == null || !parrySource.IsParryStaggered || counter == null || counter.IsActive || counter.StockCount == 0) return false;
        queuedParryCounter = true; return true;
    }
    private bool ParrySourceIsValid => playerArt != null && playerArt.isActiveAndEnabled && parrySource != null && parrySource.isActiveAndEnabled && parrySource.IsAlive &&
        parrySource.IsParryStaggered && (parryEnemyArt == null || parryEnemyArt.IsParryDeflectionOwnedBy(this));
    private void SampleAdaptiveParry()
    {
        if (!parrySequenceActive) return;
        if (!ParrySourceIsValid) { ClearParrySequence(); return; }
        int age = Mathf.Max(0, (int)(CombatTimeController.TickCount - parryStartTick));
        var motion = Profile.ParryReaction;
        int frame = age < parryLeadFrames ? Mathf.Min(motion.hitFrame - 1, Mathf.FloorToInt(age * motion.hitFrame / (float)parryLeadFrames)) :
            motion.hitFrame + Mathf.FloorToInt(Mathf.Min(Profile.parryFollowThroughFrames, age - parryLeadFrames) *
                (motion.TotalFrames - motion.hitFrame - 1) / (float)Profile.parryFollowThroughFrames);
        source.sprite = motion.At(frame);
        bool fixedEnemyPose = parryImpactPresented && parryRetreatMotor != null;
        parryEnemyArt?.SampleParryDeflection(this, fixedEnemyPose ? Mathf.Min(age, parryLeadFrames) : age);
        playerArt.RefreshVisual();
        if (parryCameraActive && !parryImpactPresented && Profile.parryExpressiveCamera) SampleExpressiveParryPreparation();
        if (!parryImpactPresented && age >= parryLeadFrames)
        {
            parryImpactPresented = true; ParryImpactCount++; ParryImpactTick = CombatTimeController.TickCount;
            source.sprite = Profile.parryContactSprite; playerArt.RefreshVisual();
            int facing = GetComponent<PlayerMotor2D>().FacingSign;
            Vector2 contact = playerArt.Artwork.transform.TransformPoint(new Vector3(facing * Profile.parryContactOffset.x, Profile.parryContactOffset.y));
            CombatTimeController.RequestHitstop(this, Profile.parryTrialHitstopFrames / 60f);
            feedback.PresentSynchronizedParryImpact(parryVisualResult == ParryResult.Just ? CombatFeedbackKind.JustParry : CombatFeedbackKind.Parry, contact, parrySource, Profile);
            if (parryCameraActive)
            {
                parryCameraAnchor.position = new Vector3(contact.x, contact.y, 0);
                parryCameraImpacted = feedback.FocusCameraAtImpact(orangeImpact, parryCameraAnchor, Profile.parryCameraImpactSize, Profile.parryCameraReturnSeconds);
            }
            feedback.ShakeOwned(orangeImpact, Profile.parryImpactShakeSeconds, Profile.parryImpactShakeStrength, new Vector2(-facing, -.35f), 1.5f);
            if (VfxStrength > 0) orangeImpact?.Begin(contact, facing, Profile);
            parryJitterTarget = parrySource; parryJitterFacing = facing; parryJitterAge = 0; parryJitterFresh = true;
            parryJitterArt = parrySource.GetComponent<PrototypeEnemyArtPresentation>();
            parryJitterCue = parryJitterArt == null ? parrySource.GetComponent<EnemyCombatCue2D>() : null;
            if (Profile.parryEnemyReleasePixels > 0 && parryEnemyArt != null &&
                parrySource.TryGetComponent<EnemyCounterLaunch2D>(out var retreatMotor) && retreatMotor.TryPrepareParryRetreat())
                parryRetreatMotor = retreatMotor;
            if (parryRealTime >= nextParryLightTime)
            {
                parryLight?.Begin(parrySource, contact); parryLightAge = 0;
                nextParryLightTime = parryRealTime + Profile.parryLightRepeatSeconds;
            }
        }
        if (parryImpactPresented && age >= parryLeadFrames + Profile.parryFollowThroughFrames)
        {
            bool request = queuedParryCounter && ParrySourceIsValid;
            bool bridge = request && CanBridgeQueuedCounter;
            ClearParrySequence(false);
            if (request)
            {
                // The action resolver starts this request next tick; do not insert a normal-return frame in that gap.
                if (bridge) parryCounterCameraPendingUntilTick = CombatTimeController.TickCount + 1;
                if (!state.RequestAction(TechnicalSwordAction.PlayerState.PlayerActionRequest.Attack)) parryCounterCameraPendingUntilTick = -1;
            }
        }
    }
    private void ClearParrySequence(bool clearLight = true)
    {
        ClearParryJitter();
        parryRetreatMotor = null;
        parryEnemyArt?.ReleaseParryDeflection(this); parryEnemyArt = null;
        parrySequenceActive = parryImpactPresented = queuedParryCounter = false;
        CombatTimeController.ReleaseOwner(this);
        if (clearLight)
        { CancelParryCamera(true); parryLightAge = -1; parryLight?.ResetLight(); feedback?.CancelParryShockwave(); orangeImpact?.ResetAccent(); }
        else if (parryCameraActive && !parryCameraReturning) { parryCameraHoldRemaining = Profile.parryCameraHoldSeconds; parryCameraHoldFresh = true; }
    }
    private void BeginParryCamera()
    {
        CancelParryCamera(true);
        if (!Profile.parryCameraEnabled || ShakeStrength <= 0 || parryCameraAnchor == null || feedback == null) return;
        playerArt.RefreshVisual(); int facing = GetComponent<PlayerMotor2D>().FacingSign;
        parryCameraStartPosition = gameplayCamera.transform.position - feedback.ShakeOffset;
        parryCameraStartSize = gameplayCamera.orthographicSize;
        parryCameraAnchor.position = playerArt.Artwork.transform.TransformPoint(new Vector3(facing * Profile.parryContactOffset.x, Profile.parryContactOffset.y));
        parryCameraActive = feedback.FocusCamera(orangeImpact, parryCameraAnchor, Vector2.zero, Profile.parryCameraPrepareSize, Profile.parryCameraApproachSeconds);
        parryCameraImpacted = parryCameraReturning = false; parryCameraHoldRemaining = -1; parryCameraTarget = parrySource;
        if (parryCameraActive && Profile.parryExpressiveCamera) SampleExpressiveParryPreparation();
    }
    private void SampleExpressiveParryPreparation()
    {
        int elapsed = Mathf.Max(0, (int)(CombatTimeController.TickCount - parryStartTick));
        var position = Profile.SampleParryCameraPosition(parryCameraStartPosition, parryCameraAnchor.position, elapsed, parryLeadFrames);
        float size = Profile.SampleParryCameraSize(parryCameraStartSize, elapsed, parryLeadFrames);
        feedback.SetCameraFocusPose(orangeImpact, position, size);
    }
    private void AdvanceParryCamera(float seconds)
    {
        if (!parryCameraActive) return;
        if (feedback == null || !feedback.IsCameraFocusOwnedBy(orangeImpact))
        { parryCameraActive = false; parryCameraTarget = null; return; }
        if (CanBridgeQueuedCounter) return;
        if (!parryCameraReturning && Profile.parryCameraReturnOnHitstopEnd && parryCameraImpacted && !CombatTimeController.IsHitStopped)
            BeginParryCameraReturn();
        if (parryCameraReturning)
        {
            if (Profile.parryExpressiveCamera)
            {
                parryCameraReturnAge += seconds;
                float t = Profile.SampleParryCameraReturn(parryCameraReturnAge / Profile.parryCameraReturnSeconds);
                feedback.SetCameraFocusPose(orangeImpact, Vector3.Lerp(parryCameraAnchor.position, parryCameraStartPosition, t),
                    Mathf.Lerp(Profile.parryCameraImpactSize, parryCameraStartSize, t));
                if (parryCameraReturnAge >= Profile.parryCameraReturnSeconds)
                { feedback.ReleaseCameraFocus(orangeImpact, true); parryCameraActive = false; parryCameraTarget = null; }
            }
            return;
        }
        if (Profile.parryCameraReturnOnHitstopEnd || parrySequenceActive || parryCameraHoldRemaining < 0) return;
        if (parryCameraHoldFresh && parryCameraHoldRemaining > 0) { parryCameraHoldFresh = false; return; }
        parryCameraHoldRemaining = Mathf.Max(0, parryCameraHoldRemaining - seconds);
        if (parryCameraHoldRemaining <= .000001f) BeginParryCameraReturn();
    }
    private void BeginParryCameraReturn()
    {
        parryCameraReturnAge = 0; parryCameraReturning = true;
        if (Profile.parryExpressiveCamera)
            feedback.SetCameraFocusPose(orangeImpact, parryCameraAnchor.position, Profile.parryCameraImpactSize);
        else
        {
            feedback.FocusCamera(orangeImpact, parryCameraAnchor, Vector2.zero, Profile.parryCameraImpactSize, Profile.parryCameraReturnSeconds);
            feedback.ReleaseCameraFocus(orangeImpact, false);
        }
    }
    private void CancelParryCamera(bool immediate)
    {
        parryCounterCameraPendingUntilTick = -1;
        if (feedback != null && orangeImpact != null) feedback.CancelOwnedCameraEffects(orangeImpact, immediate);
        parryCameraActive = parryCameraImpacted = parryCameraReturning = false;
        parryCameraTarget = null; parryCameraHoldRemaining = -1;
    }
    private void AdvanceParryJitter(float seconds)
    {
        if (parryJitterAge < 0 && !parryEnemyRecoilPose) return;
        if (!parrySequenceActive || ShakeStrength <= 0 ||
            parryJitterTarget == null || !parryJitterTarget.IsAlive || !parryJitterTarget.gameObject.activeInHierarchy ||
            playerArt == null || !playerArt.isActiveAndEnabled ||
            (parryJitterArt != null && !parryJitterArt.isActiveAndEnabled))
        { ClearParryJitter(); return; }
        if (parryEnemyRecoilPose || !CombatTimeController.IsHitStopped)
        {
            if (parryRetreatMotor == null || Profile.parryEnemyReleaseLeanDegrees <= 0 ||
                parryJitterArt == null || !parryJitterArt.IsParryDeflectionOwnedBy(this))
            { ClearParryJitter(); return; }
            if (!parryEnemyRecoilPose)
            {
                playerArt.ReleaseImpactOffset(this);
                parryJitterAge = -1;
                parryEnemyRecoilPose = true;
            }
            // The body is already retreating on the combat clock; only the drawing's tilt is visual.
            parryJitterArt.SetImpactOffset(this, 0, Profile.parryEnemyReleaseLeanDegrees * ShakeStrength * parryJitterFacing);
            return;
        }
        if (!parryJitterFresh) parryJitterAge += seconds;
        parryJitterFresh = false;
        float offset = Profile.SampleParryJitter(parryJitterAge, ShakeStrength) * parryJitterFacing;
        playerArt.SetImpactOffset(this, -offset);
        if (parryJitterArt != null) parryJitterArt.SetImpactOffset(this, offset);
        if (parryJitterCue != null) parryJitterCue.SetImpactOffset(this, offset);
    }
    private void ClearParryJitter()
    {
        if (playerArt != null) playerArt.ReleaseImpactOffset(this);
        if (parryJitterArt != null) parryJitterArt.ReleaseImpactOffset(this);
        if (parryJitterCue != null) parryJitterCue.ReleaseImpactOffset(this);
        parryJitterAge = -1; parryEnemyRecoilPose = false; parryJitterTarget = null; parryJitterArt = null; parryJitterCue = null;
    }
    private void SampleCounter()
    {
        if (source != null && Profile != null && counter.IsActive) source.sprite = Profile.Sample(counter.ElapsedFrames);
    }
    private void OnBlink()
    {
        if (playerArt == null || playerArt.Artwork == null) return;
        playerArt.RefreshVisual(); var drawing = playerArt.Artwork;
        if (effects != null && VfxStrength > 0)
        {
            int sign = GetComponent<PlayerMotor2D>().FacingSign;
            Vector2 from = drawing.transform.position;
            Vector2 to = counter.HoverPosition + (Vector2)drawing.transform.localPosition;
            effects.EmitEyeTrail(drawing, sign);
            effects.EmitTransitSilhouette(drawing, sign, from, Vector2.one, .45f * VfxStrength);
            effects.EmitTransitSilhouette(drawing, sign, Vector2.Lerp(from, to, .5f), new Vector2(1.5f, .5f), .25f * VfxStrength);
        }
        hidden = true; drawing.enabled = false;
    }
    private void RestoreBody() { hidden = false; if (playerArt != null) playerArt.RefreshVisual(); }
    private void OnCharge()
    {
        RestoreBody(); charged = true; counterLightReady = true; chargeRealElapsed = 0;
        CombatTimeController.RequestSlow(counter, Profile.chargeScale, Profile.chargeRealSeconds);
        if (!counterCameraBridgeActive) FocusCounterCamera();
    }
    private void FocusCounterCamera() => feedback?.FocusCamera(this, transform,
        new Vector2(Profile.cameraFocusOffset.x * counter.KickFacingSign, Profile.cameraFocusOffset.y),
        Profile.cameraOrthographicSize, Profile.cameraBlendSeconds);
    private void AdvanceCounterCameraBridge(float seconds)
    {
        if (!counterCameraBridgeActive) return;
        if (!counter.IsActive || feedback == null || !feedback.IsCameraFocusOwnedBy(this)) { counterCameraBridgeActive = false; return; }
        if (ShakeStrength <= 0) { counterCameraBridgeActive = false; feedback.ReleaseCameraFocus(this, true); return; }
        counterCameraBridgeAge += seconds;
        float t = Mathf.SmoothStep(0, 1, counterCameraBridgeAge / Profile.parryCounterCameraBridgeSeconds);
        Vector3 target = counter.HoverPosition + new Vector2(Profile.cameraFocusOffset.x * counter.KickFacingSign, Profile.cameraFocusOffset.y);
        target.z = counterCameraBridgeFrom.z;
        feedback.SetCameraFocusPose(this, Vector3.Lerp(counterCameraBridgeFrom, target, t),
            Mathf.Lerp(counterCameraBridgeFromSize, Profile.cameraOrthographicSize, t));
        if (counterCameraBridgeAge >= Profile.parryCounterCameraBridgeSeconds && counter.ElapsedFrames >= Profile.chargeFrame)
        { counterCameraBridgeActive = false; FocusCounterCamera(); }
    }
    private void OnRelease() { charged = false; CombatTimeController.ReleaseSlow(counter); }
    private void OnHit(Damageable2D target, Vector2 contact)
    {
        OnRelease();
        CombatTimeController.RequestHitstop(counter, Profile.hitstopFrames / 60f);
        if (UsesEnhancedPresentation)
        {
            counterLight?.BeginImpact(target,contact,counter.KickFacingSign);
            feedback?.ShakeDirectional(.30f,.48f,new Vector2(counter.KickFacingSign,-.25f),1.75f);
            feedback?.PunchCamera(this);
        }
        if (effects != null && VfxStrength > 0) effects.Emit("ElectricKick", contact, 1.15f * VfxStrength, counter.KickFacingSign);
        var launchedEnemy = target != null ? target.GetComponent<EnemyCounterLaunch2D>() : null;
        if (launchedEnemy != null)
        {
            if (!launchedEnemies.Contains(launchedEnemy)) launchedEnemies.Add(launchedEnemy);
            launchedEnemy.BeginLaunch(Profile, counter.KickFacingSign);
        }
    }
    private void OnFinished(bool _)
    {
        // A tuning value longer than the action must not leave a custom focus override holding its lease.
        if (counterCameraBridgeActive && counter.LastEndReason == "Complete") FocusCounterCamera();
        counterCameraBridgeActive = false;
        RestoreBody(); OnRelease();
        counterLightReady = false; counterLight?.ResetLight();
        feedback?.ReleaseCameraFocus(this, counter.LastEndReason != "Complete");
        if (counter.LastEndReason != "Complete") ResetPresentation();
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if (parryLight != null && FlashStrength <= 0) { parryLightAge = -1; parryLight.ResetLight(); }
        if (ShakeStrength <= 0) ClearParryJitter();
        if (counterCameraBridgeActive && ShakeStrength <= 0)
        { counterCameraBridgeActive = false; feedback?.ReleaseCameraFocus(this, true); }
        if (parryCameraActive && (ShakeStrength <= 0 || Profile == null || !Profile.parryCameraEnabled ||
            parryCameraTarget == null || !parryCameraTarget.IsAlive || !parryCameraTarget.gameObject.activeInHierarchy ||
            state == null || state.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Alive ||
            state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.Hit)) CancelParryCamera(true);
        if (parrySequenceActive && (!ParrySourceIsValid || feedback == null || !feedback.isActiveAndEnabled ||
            state.ActionState != TechnicalSwordAction.PlayerState.PlayerActionState.ParrySuccess)) ClearParrySequence();
        if (CombatTimeController.IsPaused) { orangeImpact?.Advance(0, VfxStrength, FlashStrength); return; }
        if (Profile == null || !float.IsFinite(realSeconds) || realSeconds < 0) return;
        parryRealTime += realSeconds;
        if (state != null && state.LifeState == TechnicalSwordAction.PlayerState.PlayerLifeState.Dead)
        { ResetPresentation(); return; }
        AdvanceParryJitter(realSeconds);
        AdvanceParryCamera(realSeconds);
        AdvanceCounterCameraBridge(realSeconds);
        orangeImpact?.Advance(realSeconds, VfxStrength, FlashStrength);
        bool soft = Profile.synchronizedParryImpact;
        bool parryVisible = UsesEnhancedPresentation && parryLightAge >= 0 && parryLightAge < (soft ? Profile.ParryLightSeconds : 4f/60)-.000001f;
        if (parryLight != null)
        {
            parryLight.Intensity = FlashStrength * (soft ? Profile.SampleParryLight(parryLightAge) : 1);
            parryLight.BackgroundStrength = soft ? Profile.parryBackgroundDim : .99f;
            parryLight.ActorLightStrength = soft ? Profile.parryActorLight : 1;
            parryLight.BloomStrength = soft ? Profile.parryLegacyBloomStrength : 1;
        }
        if (counterLight != null) counterLight.Intensity = FlashStrength;
        parryLight?.Sample(parryVisible && FlashStrength > 0,VfxStrength > 0,true);
        parryLightAge = parryVisible ? parryLightAge+realSeconds : -1;
        if (!parryVisible && !parrySequenceActive) parrySource = null;
        if (charged && !CombatTimeController.IsHitStopped) chargeRealElapsed += realSeconds;
        bool dimming = charged && counter.IsActive && CombatTimeController.SlowScale < 1;
        dimBlend = Mathf.MoveTowards(dimBlend, dimming ? 1 : 0, realSeconds / Profile.dimFadeSeconds);
        if (effects != null)
        {
            effects.HybridGlow = FlashStrength > 0 && VfxStrength > 0;
            if (VfxStrength <= 0) effects.ResetEffects(); else effects.AdvanceVisuals(realSeconds);
        }
        SyncDrawing();
        if (counter.SelectedTarget == null || !counter.SelectedTarget.gameObject.activeInHierarchy) counterLightReady = false;
        float chargeSeconds = Mathf.Max(CombatTimeController.StepSeconds,(Profile.releaseFrame-Profile.chargeFrame)/60f/Profile.chargeScale);
        counterLight?.Sample(realSeconds,UsesEnhancedPresentation && counterLightReady && FlashStrength > 0 && VfxStrength > 0,
            charged,chargeRealElapsed/chargeSeconds,counter.SelectedTarget,counter.KickFacingSign);
        if (charged && !hidden && effects != null && VfxStrength > 0)
        {
            trailAge += realSeconds;
            if (trailAge >= .1f) { trailAge %= .1f; effects.EmitEyeTrail(playerArt.Artwork, counter.KickFacingSign); }
        }
        else trailAge = 0;
    }
    private void SyncDrawing()
    {
        if (Profile == null || playerArt == null || playerArt.Artwork == null) return;
        playerArt.RefreshVisual(); var drawing = playerArt.Artwork;
        if (hidden) drawing.enabled = false;
        effects?.SampleCounterEffect(drawing,counter.ElapsedFrames-Profile.BlinkArrivalFrame,
            counter.IsActive&&counter.ElapsedFrames>=Profile.BlinkArrivalFrame&&drawing.enabled,VfxStrength);
        if (dimmer == null) return;
        float alpha = dimBlend * Profile.dimStrength * FlashStrength;
        dimmer.color = new Color(0, 0, 0, alpha); dimmer.enabled = alpha > 0;
        float height = gameplayCamera.orthographicSize * 2;
        dimmer.transform.localScale = new Vector3(height * Mathf.Max(gameplayCamera.aspect, 16f / 9), height, 1);
        bool visible = alpha > 0 && drawing.enabled;
        lift.enabled = visible; lift.sprite = drawing.sprite; lift.flipX = drawing.flipX; lift.flipY = drawing.flipY;
        var tint = drawing.color; lift.color = new Color(tint.r, tint.g, tint.b, tint.a * dimBlend * Profile.heroLiftStrength * FlashStrength);
        eyes.sprite = Profile.art.EyesFor(source.sprite); eyes.flipX = drawing.flipX; eyes.flipY = drawing.flipY;
        eyes.enabled = visible && eyes.sprite != null;
        eyes.color = new Color(.85f, .96f, 1, dimBlend * FlashStrength);
    }
    private void EnsureDrawing()
    {
        if (Profile == null || playerArt == null || playerArt.Artwork == null || gameplayCamera == null || dimmer != null) return;
        solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
        solid.hideFlags = HideFlags.DontSave;
        var overlay = new GameObject("CounterDimmer") { hideFlags = HideFlags.DontSave };
        overlay.transform.SetParent(gameplayCamera.transform, false);
        overlay.transform.localPosition = new Vector3(0, 0, gameplayCamera.nearClipPlane + .01f);
        dimmer = overlay.AddComponent<SpriteRenderer>(); dimmer.sprite = solid; dimmer.sortingOrder = 1000; dimmer.enabled = false;
        var subject = new GameObject("CounterSubjectLift") { hideFlags = HideFlags.DontSave };
        subject.transform.SetParent(playerArt.Artwork.transform, false);
        lift = subject.AddComponent<SpriteRenderer>(); lift.sortingOrder = 1010; lift.enabled = false;
        liftMaterial = new Material(Profile.subjectLiftShader) { hideFlags = HideFlags.DontSave }; lift.sharedMaterial = liftMaterial;
        var eyeObject = new GameObject("CounterEyes") { hideFlags = HideFlags.DontSave };
        eyeObject.transform.SetParent(subject.transform, false);
        eyes = eyeObject.AddComponent<SpriteRenderer>(); eyes.sortingOrder = 1013; eyes.enabled = false;
        if (Profile.enhancedPresentation)
        {
            orangeImpact = gameplayCamera.gameObject.AddComponent<ParryOrangeImpact>(); orangeImpact.hideFlags = HideFlags.DontSave;
            orangeImpact.Configure(gameplayCamera, Profile.glowMaterial);
            parryCameraAnchor = new GameObject("ParryCameraContactAnchor") { hideFlags = HideFlags.DontSave }.transform;
            parryLight = gameplayCamera.gameObject.AddComponent<VideoParryBacklight>(); parryLight.hideFlags = HideFlags.DontSave;
            parryLight.Configure(gameplayCamera,playerArt.Artwork,Profile.parryBacklightMaterial);
            counterLight = gameplayCamera.gameObject.AddComponent<VideoCounterLighting>(); counterLight.hideFlags = HideFlags.DontSave;
            counterLight.Configure(gameplayCamera,playerArt.Artwork,Profile.counterLightingMaterial);
        }
    }
    private void LateUpdate()
    {
        if (clock != null && clock.ManualAdvanceOnly) SyncDrawing();
        else AdvanceVisuals(Time.unscaledDeltaTime);
    }
    public void ResetPresentation()
    {
        counterCameraBridgeActive = false;
        ClearParrySequence(); parryRealTime = nextParryLightTime = 0;
        RestoreBody(); charged = false; dimBlend = trailAge = 0;
        counterLightReady = false; parrySource = null; parryLightAge = -1; chargeRealElapsed = 0;
        ParryImpactTick = -1;
        parryLight?.ResetLight(); counterLight?.ResetLight(); CombatTimeController.ReleaseOwner(this);
        feedback?.ReleaseCameraFocus(this, true);
        if (counter != null) CombatTimeController.ReleaseOwner(counter);
        if (effects != null) effects.ResetEffects();
        foreach (var enemy in launchedEnemies) if (enemy != null && enemy.IsActive) enemy.CancelLaunch();
        launchedEnemies.Clear();
        if (dimmer != null) dimmer.enabled = false;
        if (lift != null) lift.enabled = false;
        if (eyes != null) eyes.enabled = false;
    }
    private void OnSceneChanged(Scene _, Scene __) => ResetPresentation();
    private void OnDisable()
    {
        if (counter != null)
        {
            counter.Started -= OnStarted; counter.BlinkStarted -= OnBlink; counter.BlinkArrived -= RestoreBody;
            counter.ChargeStarted -= OnCharge; counter.ChargeReleased -= OnRelease;
            counter.HitConfirmed -= OnHit; counter.HitMissed -= OnRelease; counter.Finished -= OnFinished;
        }
        CombatTimeController.SessionReset -= ResetPresentation; SceneManager.activeSceneChanged -= OnSceneChanged;
        CombatTimeController.Unregister(this); ResetPresentation();
    }
    private void OnDestroy()
    {
        if (dimmer != null) Destroy(dimmer.gameObject);
        if (lift != null) Destroy(lift.gameObject);
        if (solid != null) Destroy(solid);
        if (liftMaterial != null) Destroy(liftMaterial);
        if (parryLight != null) Destroy(parryLight);
        if (orangeImpact != null) Destroy(orangeImpact);
        if (parryCameraAnchor != null) Destroy(parryCameraAnchor.gameObject);
        if (counterLight != null) Destroy(counterLight);
    }
}
