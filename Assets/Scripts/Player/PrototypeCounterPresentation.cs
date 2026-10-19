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
    private CounterCinematicProfile Profile => counter != null ? counter.CinematicProfile : null;
    private float FlashStrength => feedback != null && feedback.Settings != null ? Mathf.Clamp01(feedback.Settings.flash) : 1;
    private float VfxStrength => feedback != null && feedback.Settings != null ? Mathf.Clamp01(feedback.Settings.vfx) : 1;
    public int CombatTickOrder => 400;
    public float DimAlpha => dimmer != null && dimmer.enabled ? dimmer.color.a : 0;
    public bool IsCharged => charged;
    public bool IsBodyHidden => hidden;
    public bool IsSpotlightVisible => lift != null && lift.enabled;
    public int ActiveEffectCount => effects != null ? effects.ActiveCount : 0;
    public void Configure(PlayerParryCounter2D action, PrototypePlayerArtPresentation art, CombatFeedback2D common, VideoVfxRenderer vfx, Camera camera)
    { counter = action; playerArt = art; feedback = common; effects = vfx; gameplayCamera = camera; }
    private void Awake()
    {
        source = GetComponent<SpriteRenderer>(); clock = Object.FindFirstObjectByType<CombatTimeController>();
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
    private void OnStarted() { launchedEnemies.RemoveAll(enemy => enemy == null || !enemy.IsActive); SampleCounter(); }
    public void CombatTick() { }
    public void CombatTickTimers() => SampleCounter();
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
        RestoreBody(); charged = true;
        CombatTimeController.RequestSlow(counter, Profile.chargeScale, Profile.chargeRealSeconds);
        feedback?.FocusCamera(this, transform,
            new Vector2(Profile.cameraFocusOffset.x * counter.KickFacingSign, Profile.cameraFocusOffset.y),
            Profile.cameraOrthographicSize, Profile.cameraBlendSeconds);
    }
    private void OnRelease() { charged = false; CombatTimeController.ReleaseSlow(counter); }
    private void OnHit(Damageable2D target, Vector2 contact)
    {
        OnRelease();
        CombatTimeController.RequestHitstop(counter, Profile.hitstopFrames / 60f);
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
        RestoreBody(); OnRelease();
        feedback?.ReleaseCameraFocus(this, counter.LastEndReason != "Complete");
        if (counter.LastEndReason != "Complete") ResetPresentation();
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if (CombatTimeController.IsPaused || Profile == null || realSeconds < 0) return;
        bool dimming = charged && counter.IsActive && CombatTimeController.SlowScale < 1;
        dimBlend = Mathf.MoveTowards(dimBlend, dimming ? 1 : 0, realSeconds / Profile.dimFadeSeconds);
        if (effects != null)
        {
            effects.HybridGlow = FlashStrength > 0 && VfxStrength > 0;
            if (VfxStrength <= 0) effects.ResetEffects(); else effects.AdvanceVisuals(realSeconds);
        }
        SyncDrawing();
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
    }
    private void LateUpdate()
    {
        if (clock != null && clock.ManualAdvanceOnly) SyncDrawing();
        else AdvanceVisuals(Time.unscaledDeltaTime);
    }
    public void ResetPresentation()
    {
        RestoreBody(); charged = false; dimBlend = trailAge = 0;
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
    }
}
