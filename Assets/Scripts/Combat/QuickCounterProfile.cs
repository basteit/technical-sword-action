using System;
using UnityEngine;

/// <summary>Prototype-only pass-through and single sheathing strike. Original source timing is retained in ArtSource.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Quick Counter Profile")]
public sealed class QuickCounterProfile : ScriptableObject
{
    public VideoArtLibrary.Motion transfer;
    public VideoArtLibrary.Motion strike;
    public AnimationClip clip;
    [Min(1)] public int transferFrames = 8;
    [Min(0)] public int travelStartFrame = 4;
    [Min(1)] public int travelFrames = 4;
    [Min(1)] public int hitFrame = 16;
    [Min(0)] public int hitstopFrames = 8;
    [Min(.1f)] public float targetSpacing = 1.2f;
    [Min(.1f)] public float hitRange = 2.2f;
    public int EndFrame => transferFrames + (strike != null ? strike.TotalFrames : 0);
    public Sprite ContactSprite => Sample(hitFrame);
    public bool IsValid => Valid(transfer) && Valid(strike) && transferFrames > 0 && travelStartFrame >= 0 &&
        travelFrames > 0 && travelStartFrame + travelFrames == transferFrames &&
        hitFrame >= transferFrames && hitFrame < EndFrame && hitstopFrames >= 0 &&
        float.IsFinite(targetSpacing) && targetSpacing > 0 && float.IsFinite(hitRange) && hitRange >= targetSpacing;
    public Sprite Sample(int frame) => frame < transferFrames
        ? transfer.At(Mathf.Clamp(frame, 0, transferFrames - 1) * transfer.TotalFrames / transferFrames)
        : strike.At(frame - transferFrames);
    private static bool Valid(VideoArtLibrary.Motion motion) => motion != null && motion.frames != null && motion.durations != null &&
        motion.frames.Length > 0 && motion.frames.Length == motion.durations.Length &&
        Array.TrueForAll(motion.frames, sprite => sprite != null) && Array.TrueForAll(motion.durations, duration => duration > 0);
}
