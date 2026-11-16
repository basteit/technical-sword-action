using System;
using UnityEngine;

/// <summary>Heavy contact pose and collision-limited recoil; does not change normal/just acceptance.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Heavy Parry Profile")]
public sealed class HeavyParryProfile : ScriptableObject
{
    public VideoArtLibrary.Motion reaction;
    public AnimationClip clip;
    public Vector2 contactOffset = new Vector2(1, .25f);
    [Min(1)] public int followThroughFrames = 18;
    [Min(1)] public int recoilFrames = 14;
    [Min(0)] public float recoilDistance = .625f;
    public bool holdAirborneHeight = true;
    public LayerMask obstacleLayers;
    public Sprite ContactSprite => reaction?.At(reaction.hitFrame);
    public bool IsValid => reaction != null && reaction.frames != null && reaction.durations != null &&
        reaction.frames.Length == 8 && reaction.durations.Length == reaction.frames.Length &&
        Array.TrueForAll(reaction.frames, sprite => sprite != null) && Array.TrueForAll(reaction.durations, duration => duration > 0) &&
        reaction.hitFrame > 0 && reaction.hitFrame < reaction.TotalFrames && followThroughFrames > 0 &&
        recoilFrames > 0 && recoilFrames <= followThroughFrames && float.IsFinite(recoilDistance) && recoilDistance >= 0 &&
        float.IsFinite(contactOffset.x) && float.IsFinite(contactOffset.y);
}
