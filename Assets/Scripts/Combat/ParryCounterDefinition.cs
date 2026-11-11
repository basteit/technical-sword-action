using UnityEngine;

[CreateAssetMenu(menuName = "Technical Sword Action/Parry Counter Definition")]
public sealed class ParryCounterDefinition : ScriptableObject
{
    [Min(1)] public int baseDamage = 30;
    [Min(1)] public int hitFrame = 18;
    [Min(1)] public int endFrame = 35;
    [Min(0)] public int cancelStartFrame = 25;
    [Min(0)] public int chainBufferFrames = 6;
    [Min(0)] public int gaugeGain = 3;
    [Min(0)] public float approachDistance = .6f;
    [Min(1)] public int approachFrames = 6;
    public Vector2 hitSize = new Vector2(2, 1.2f);
    public LayerMask obstacleLayers;
    public bool IsValid => hitFrame > 0 && endFrame >= hitFrame && cancelStartFrame > hitFrame &&
        cancelStartFrame <= endFrame && chainBufferFrames >= 0 && baseDamage > 0 && hitSize.x > 0 && hitSize.y > 0;
}
