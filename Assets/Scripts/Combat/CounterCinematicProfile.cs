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
    [Min(0)] public float targetHeadOffsetX = 7f / 32f;
    [Min(.01f)] public float hitRadius = .5f;
    [Range(.01f, 1)] public float chargeScale = .15f;
    [Min(.01f)] public float chargeRealSeconds = .95f;
    [Min(0)] public int hitstopFrames = 8;
    [Range(0, 1)] public float dimStrength = .965f;
    [Range(0, 1)] public float heroLiftStrength = .6f;
    [Min(.001f)] public float dimFadeSeconds = 3f / 60f;
    [Header("Gameplay camera close-up")]
    [Min(.01f)] public float cameraOrthographicSize = 1.875f;
    public Vector2 cameraFocusOffset = new Vector2(.45f, .4f);
    [Min(.001f)] public float cameraBlendSeconds = .1f;
    [Min(0)] public float launchDistance = 3.2f;
    [Min(0)] public float launchHeight = .65f;
    [Min(1)] public int launchFrames = 22;
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
        Finite(dimStrength) && dimStrength >= 0 && dimStrength <= 1 &&
        Finite(heroLiftStrength) && heroLiftStrength >= 0 && heroLiftStrength <= 1 &&
        Finite(dimFadeSeconds) && dimFadeSeconds > 0 && Valid(Transfer) && Valid(Kick) &&
        Finite(cameraOrthographicSize) && cameraOrthographicSize > 0 &&
        Finite(cameraFocusOffset.x) && Finite(cameraFocusOffset.y) && Finite(cameraBlendSeconds) && cameraBlendSeconds > 0 &&
        Transfer.TotalFrames == BlinkArrivalFrame && Transfer.TotalFrames + Kick.TotalFrames == endFrame &&
        Valid(art.Find("Fx_ElectricKick")) && subjectLiftShader != null && glowMaterial != null;
    public Sprite Sample(int frame) => frame < BlinkArrivalFrame ? Transfer.At(frame) : Kick.At(frame - BlinkArrivalFrame);
    private static bool Finite(float value) => float.IsFinite(value);
    private static bool Valid(VideoArtLibrary.Motion motion) => motion != null && motion.frames != null &&
        motion.durations != null && motion.frames.Length > 0 && motion.frames.Length == motion.durations.Length &&
        motion.frames.All(sprite => sprite != null) && motion.durations.All(duration => duration > 0);
}
