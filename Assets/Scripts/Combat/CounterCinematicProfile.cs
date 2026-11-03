using System.Linq;
using UnityEngine;

/// <summary>Opt-in gameplay snapshot; no runtime dependency on the filming director or its mutable library.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Counter Cinematic Profile")]
public sealed class CounterCinematicProfile : ScriptableObject
{
    public VideoArtLibrary art;
    public Material glowMaterial;
    public Shader subjectLiftShader;
    [Min(0)] public int blinkStartFrame = 10;
    [Min(1)] public int blinkTravelFrames = 2;
    [Min(1)] public int chargeFrame = 12;
    [Min(1)] public int releaseFrame = 20;
    [Min(1)] public int hitFrame = 21;
    [Min(1)] public int endFrame = 45;
    public Vector2 contactOffset = new Vector2(51f / 32f, .325f);
    [Min(0)] public float hoverHeight = .125f;
    [Min(0)] public float impactHeight = .0625f;
    [Min(0)] public float targetHeadOffsetX = 17f / 32f;
    [Min(.01f)] public float hitRadius = .5f;
    [Range(.01f, 1)] public float chargeScale = .15f;
    [Min(.01f)] public float chargeRealSeconds = .95f;
    [Min(0)] public int hitstopFrames = 8;
    [Range(0, 1)] public float dimStrength = .965f;
    [Range(0, 1)] public float heroLiftStrength = .6f;
    [Min(.001f)] public float dimFadeSeconds = 3f / 60f;
    [Header("Gameplay camera close-up")]
    [Min(.01f)] public float cameraOrthographicSize = 3.75f;
    public Vector2 cameraFocusOffset = new Vector2(.8f, .4f);
    [Min(.001f)] public float cameraBlendSeconds = .1f;
    [Min(0)] public float launchDistance = 3.2f;
    [Min(0)] public float launchHeight = .65f;
    [Min(1)] public int launchFrames = 22;
    [Range(0, 180)] public float launchRotationDegrees = 95;
    [Header("Shared impact lighting (opt-in gameplay presentation)")]
    public bool enhancedPresentation;
    public Material parryBacklightMaterial;
    public Material counterLightingMaterial;
    public Sprite parryContactSprite;
    public Sprite parryWaitSprite;
    [Range(0,1)] public float parrySuccessStartNormalized;
    public Vector2 parryContactOffset;
    [Min(0)] public int parryHitstopFrames = 9;
    [Min(0)] public int justParryHitstopFrames = 10;
    [Header("Adaptive parry impact trial (gameplay only)")]
    public bool synchronizedParryImpact;
    [Min(0)] public int parryTrialHitstopFrames = 30;
    [Min(0)] public int parryApproachMaxFrames = 8;
    [Min(1)] public int parryFollowThroughFrames = 8;
    [Range(0, .5f)] public float parryBackgroundDim = .4f;
    [Range(0, 1)] public float parryActorLight = .08f;
    [Range(0, 1)] public float parryLegacyBloomStrength;
    [Min(.001f)] public float parryLightRiseSeconds = 2f / 60f;
    [Min(0)] public float parryLightHoldSeconds = 2f / 60f;
    [Min(.001f)] public float parryLightFadeSeconds = .2f;
    [Min(0)] public float parryLightRepeatSeconds = .4f;
    [Min(.01f)] public float parryWaveSeconds = .4f;
    [Min(0)] public float parryWaveGain = 1.55f;
    [Min(0)] public float parryWavePixels = 32;
    [Header("Additional orange parry accent (gameplay only)")]
    public bool parryOrangeAccent = true;
    public Color parrySparkColor = new Color(1, .36f, .035f, 1);
    public Color parryGlowColor = new Color(1, .25f, .025f, .8f);
    [Min(0)] public float parryGlowGain = 3;
    [Min(.01f)] public float parryGlowRadius = 1.6f;
    [Min(.01f)] public float parrySparkSeconds = .32f;
    [Min(.01f)] public float parryGlowSeconds = .18f;
    [Min(.01f)] public float parrySparkRadius = 1.2f;
    [Range(0, 4)] public float parryActorJitterPixels = 2;
    [Min(1)] public int parryActorJitterStepFrames = 2;
    [Range(0, 8)] public float parryEnemyReleasePixels;
    [Range(0, 30)] public float parryEnemyReleaseLeanDegrees;
    [Header("Parry contact camera (gameplay only)")]
    public bool parryCameraEnabled = true;
    public bool parryCameraReturnOnHitstopEnd = true;
    public bool parryCounterCameraBridge = true;
    [Min(.001f)] public float parryCounterCameraBridgeSeconds = .2f;
    [Min(.01f)] public float parryCameraPrepareSize = 4.75f;
    [Min(.01f)] public float parryCameraImpactSize = 4.25f;
    [Min(.001f)] public float parryCameraApproachSeconds = .1f;
    [Min(0)] public float parryCameraHoldSeconds = .2f;
    [Min(.001f)] public float parryCameraReturnSeconds = .18f;
    [Min(.001f)] public float parryImpactShakeSeconds = .1f;
    [Min(0)] public float parryImpactShakeStrength = .32f;
    [Header("Expressive parry trial; previous presentation is retained")]
    public bool parryExpressiveCamera = true;
    [Min(0)] public float parryCameraArcHeight = .2f;
    [Min(0)] public int parryCameraPositionLeadFrames = 2;
    [Min(0)] public int parryCameraZoomDelayFrames = 2;
    public AnimationCurve parryCameraPositionCurve = CreateParryPositionCurve();
    public AnimationCurve parryCameraZoomCurve = CreateParryZoomCurve();
    public AnimationCurve parryCameraReturnCurve = CreateParryReturnCurve();
    public bool parryReferenceAccent = true;
    public bool parryDirectionalSparks;
    [Min(.01f)] public float parryReferenceSparkRadius = 3.2f;
    [Min(.01f)] public float parryFlareLength = 10.5f;
    [Min(.01f)] public float parryFlareSeconds = .1f;
    public static AnimationCurve CreateParryPositionCurve() => AnimationCurve.EaseInOut(0, 0, 1, 1);
    public static AnimationCurve CreateParryZoomCurve() => new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
    public static AnimationCurve CreateParryReturnCurve() => new AnimationCurve(new Keyframe(0, 0, .25f, .25f), new Keyframe(1, 1, 0, 0));
    public Vector3 SampleParryCameraPosition(Vector3 from, Vector3 to, int elapsed, int duration)
    {
        float t = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / (float)Mathf.Max(1, duration - parryCameraPositionLeadFrames));
        float p = EvaluateCameraCurve(parryCameraPositionCurve, t);
        return Vector3.Lerp(from, to, p) + Vector3.up * (4 * p * (1 - p) * parryCameraArcHeight);
    }
    public float SampleParryCameraSize(float from, int elapsed, int duration)
    {
        int delay = Mathf.Min(parryCameraZoomDelayFrames, Mathf.Max(0, duration - 1));
        float t = duration <= 0 ? 1 : Mathf.Clamp01((elapsed - delay) / (float)Mathf.Max(1, duration - delay));
        return Mathf.Lerp(from, parryCameraImpactSize, EvaluateCameraCurve(parryCameraZoomCurve, t));
    }
    public float SampleParryCameraReturn(float progress) => EvaluateCameraCurve(parryCameraReturnCurve, Mathf.Clamp01(progress));
    private static float EvaluateCameraCurve(AnimationCurve curve, float progress)
    {
        float value = curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
        return float.IsFinite(value) ? Mathf.Clamp01(value) : Mathf.Clamp01(progress);
    }
    public VideoArtLibrary.Motion ParryReaction => art != null ? art.Find("ParrySuccessLight") : null;
    public float ParryLightSeconds => parryLightRiseSeconds + parryLightHoldSeconds + parryLightFadeSeconds;
    public bool IsParryTuningValid => parryTrialHitstopFrames >= 0 && parryApproachMaxFrames >= 0 && parryFollowThroughFrames > 0 && Finite(ParryLightSeconds) &&
        Finite(parryBackgroundDim) && parryBackgroundDim >= 0 && parryBackgroundDim <= .5f &&
        Finite(parryActorLight) && parryActorLight >= 0 && parryActorLight <= 1 &&
        Finite(parryLegacyBloomStrength) && parryLegacyBloomStrength >= 0 && parryLegacyBloomStrength <= 1 &&
        ValidColor(parrySparkColor) && ValidColor(parryGlowColor) &&
        Finite(parryGlowGain) && parryGlowGain >= 0 && Finite(parryGlowRadius) && parryGlowRadius > 0 &&
        Finite(parryCameraPrepareSize) && parryCameraPrepareSize > 0 && Finite(parryCameraImpactSize) &&
        parryCameraImpactSize > 0 && parryCameraImpactSize <= parryCameraPrepareSize &&
        Finite(parryCameraApproachSeconds) && parryCameraApproachSeconds > 0 &&
        Finite(parryCameraHoldSeconds) && parryCameraHoldSeconds >= 0 &&
        Finite(parryCameraReturnSeconds) && parryCameraReturnSeconds > 0 &&
        Finite(parryCounterCameraBridgeSeconds) && parryCounterCameraBridgeSeconds > 0 &&
        Finite(parryImpactShakeSeconds) && parryImpactShakeSeconds > 0 &&
        Finite(parryImpactShakeStrength) && parryImpactShakeStrength >= 0 &&
        Finite(parryCameraArcHeight) && parryCameraArcHeight >= 0 && parryCameraPositionLeadFrames >= 0 && parryCameraZoomDelayFrames >= 0 &&
        ValidCameraCurve(parryCameraPositionCurve) && ValidCameraCurve(parryCameraZoomCurve) && ValidCameraCurve(parryCameraReturnCurve) &&
        Finite(parryReferenceSparkRadius) && parryReferenceSparkRadius > 0 && Finite(parryFlareLength) && parryFlareLength > 0 &&
        Finite(parryFlareSeconds) && parryFlareSeconds > 0 &&
        Finite(parrySparkSeconds) && parrySparkSeconds > 0 && Finite(parryGlowSeconds) && parryGlowSeconds > 0 &&
        Finite(parrySparkRadius) && parrySparkRadius > 0 &&
        Finite(parryActorJitterPixels) && parryActorJitterPixels >= 0 && parryActorJitterPixels <= 4 && parryActorJitterStepFrames > 0 &&
        Finite(parryEnemyReleasePixels) && parryEnemyReleasePixels >= 0 && parryEnemyReleasePixels <= 8 &&
        Finite(parryEnemyReleaseLeanDegrees) && parryEnemyReleaseLeanDegrees >= 0 && parryEnemyReleaseLeanDegrees <= 30 &&
        Finite(parryLightRiseSeconds) && parryLightRiseSeconds > 0 &&
        Finite(parryLightHoldSeconds) && parryLightHoldSeconds >= 0 &&
        Finite(parryLightFadeSeconds) && parryLightFadeSeconds > 0 &&
        Finite(parryLightRepeatSeconds) && parryLightRepeatSeconds >= 0 &&
        Finite(parryWaveSeconds) && parryWaveSeconds > 0 && Finite(parryWaveGain) && parryWaveGain >= 0 &&
        Finite(parryWavePixels) && parryWavePixels >= 0;
    public float SampleParryLight(float age)
    {
        if (!IsParryTuningValid || !float.IsFinite(age) || age < 0 || age >= ParryLightSeconds) return 0;
        if (age < parryLightRiseSeconds) return Mathf.SmoothStep(0, 1, age / parryLightRiseSeconds);
        float fade = age - parryLightRiseSeconds - parryLightHoldSeconds;
        return fade <= 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, fade / parryLightFadeSeconds);
    }
    public float SampleParryJitter(float age, float strength)
    {
        float duration = parryTrialHitstopFrames / 60f;
        if (!IsParryTuningValid || !Finite(age) || !Finite(strength) || age < 0 || age >= duration || strength <= 0) return 0;
        int step = Mathf.FloorToInt(age * 60f / parryActorJitterStepFrames + .0001f);
        float pixels = Mathf.Round(parryActorJitterPixels * Mathf.Clamp01(strength) * (1 - .65f * age / duration));
        return (step % 2 == 0 ? 1 : -1) * pixels / 32f;
    }
    public int BlinkArrivalFrame => blinkStartFrame + blinkTravelFrames;
    public VideoArtLibrary.Motion Transfer => art != null ? art.Find("CounterTransfer") : null;
    public VideoArtLibrary.Motion Kick => art != null ? art.Find("CounterKick") : null;
    public Sprite ContactSprite => Kick?.At(hitFrame - BlinkArrivalFrame);
    public bool IsValid => blinkStartFrame >= 0 && blinkTravelFrames > 0 &&
        chargeFrame == BlinkArrivalFrame && releaseFrame > chargeFrame && hitFrame > releaseFrame &&
        launchFrames > 0 && endFrame >= hitFrame + launchFrames && hitstopFrames >= 0 &&
        Finite(contactOffset.x) && Finite(contactOffset.y) && Finite(hoverHeight) && hoverHeight >= 0 &&
        Finite(impactHeight) && impactHeight >= 0 && Finite(targetHeadOffsetX) && targetHeadOffsetX >= 0 &&
        Finite(hitRadius) && hitRadius > 0 && Finite(chargeScale) && chargeScale > 0 && chargeScale <= 1 &&
        Finite(chargeRealSeconds) && chargeRealSeconds >= (releaseFrame - chargeFrame) / (60f * chargeScale) &&
        Finite(launchDistance) && launchDistance >= 0 && Finite(launchHeight) && launchHeight >= 0 &&
        Finite(launchRotationDegrees) && launchRotationDegrees >= 0 && launchRotationDegrees <= 180 &&
        Finite(dimStrength) && dimStrength >= 0 && dimStrength <= 1 &&
        Finite(heroLiftStrength) && heroLiftStrength >= 0 && heroLiftStrength <= 1 &&
        Finite(dimFadeSeconds) && dimFadeSeconds > 0 && Valid(Transfer) && Valid(Kick) &&
        Finite(cameraOrthographicSize) && cameraOrthographicSize > 0 &&
        Finite(cameraFocusOffset.x) && Finite(cameraFocusOffset.y) && Finite(cameraBlendSeconds) && cameraBlendSeconds > 0 &&
        Transfer.TotalFrames == BlinkArrivalFrame && Transfer.TotalFrames + Kick.TotalFrames == endFrame &&
        Valid(art.Find("Fx_ElectricKick")) &&
        (art.Find("CounterKickEffect")==null||Valid(art.Find("CounterKickEffect"))&&art.Find("CounterKickEffect").TotalFrames==Kick.TotalFrames) &&
        subjectLiftShader != null && glowMaterial != null &&
        (!enhancedPresentation || parryBacklightMaterial != null && counterLightingMaterial != null &&
            parryContactSprite != null && parryWaitSprite != null && Finite(parrySuccessStartNormalized) &&
            parrySuccessStartNormalized >= 0 && parrySuccessStartNormalized < 1 &&
            Finite(parryContactOffset.x) && Finite(parryContactOffset.y) &&
            parryHitstopFrames >= 0 && justParryHitstopFrames >= 0) &&
        (!synchronizedParryImpact || IsParryTuningValid && Valid(ParryReaction) && ParryReaction.hitFrame > 0 && ParryReaction.hitFrame < ParryReaction.TotalFrames);
    public Sprite Sample(int frame) => frame < BlinkArrivalFrame ? Transfer.At(frame) : Kick.At(frame - BlinkArrivalFrame);
    private static bool Finite(float value) => float.IsFinite(value);
    private static bool ValidCameraCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length < 2 || curve.length > 32) return false;
        if (Mathf.Abs(curve[0].time) > .0001f || Mathf.Abs(curve[0].value) > .0001f ||
            Mathf.Abs(curve[curve.length - 1].time - 1) > .0001f || Mathf.Abs(curve[curve.length - 1].value - 1) > .0001f) return false;
        for (int i = 0; i < curve.length; i++)
        {
            var key = curve[i];
            if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent) ||
                key.time < 0 || key.time > 1 || key.value < 0 || key.value > 1 || i > 0 && key.time <= curve[i - 1].time) return false;
        }
        return true;
    }
    private static bool ValidColor(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a) &&
        value.r >= 0 && value.r <= 1 && value.g >= 0 && value.g <= 1 && value.b >= 0 && value.b <= 1 && value.a >= 0 && value.a <= 1;
    private static bool Valid(VideoArtLibrary.Motion motion) => motion != null && motion.frames != null &&
        motion.durations != null && motion.frames.Length > 0 && motion.frames.Length == motion.durations.Length &&
        motion.frames.All(sprite => sprite != null) && motion.durations.All(duration => duration > 0);
}
