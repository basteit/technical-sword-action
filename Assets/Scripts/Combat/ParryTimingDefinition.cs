using UnityEngine;

[CreateAssetMenu(menuName = "Technical Sword Action/Parry Timing")]
public sealed class ParryTimingDefinition : ScriptableObject
{
    [Min(0)] public int startFrame = 5;
    [Min(0)] public int justEndFrame = 8;
    [Min(1)] public int endFrame = 18;
    [Min(1)] public int failEndFrame = 26;
    [Min(1)] public int successFrames = 10;
    [Min(1)] public int justSuccessFrames = 8;
    [Min(0)] public int cooldownFrames = 14;
    public bool IsValid => startFrame >= 0 && justEndFrame >= startFrame &&
        endFrame >= justEndFrame && failEndFrame >= endFrame && successFrames > 0 && justSuccessFrames > 0;
}
