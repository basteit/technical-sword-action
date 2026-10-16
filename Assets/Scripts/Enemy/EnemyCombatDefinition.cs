using UnityEngine;

public enum EnemyCombatRank { Normal, Variant, Elite, MidBoss, Boss }

/// <summary>Combat values for a readable melee attack, in inclusive 60 Hz frames.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Enemy Combat Definition")]
public sealed class EnemyCombatDefinition : ScriptableObject
{
    public EnemyCombatRank rank = EnemyCombatRank.Normal;
    [Min(1)] public int maxHp = 30;
    [Min(0)] public float moveSpeed = 2.2f;
    [Min(1)] public int windupFrames = 30;
    [Min(1)] public int activeFrames = 3;
    [Min(1)] public int recoveryFrames = 30;
    [Min(1)] public int cooldownFrames = 32;
    [Min(1)] public int damage = 10;
    [Min(0)] public float knockback = 4.5f;
    public bool parryable = true;
    public Vector2 hitOffset = new Vector2(.7f, 0);
    public Vector2 hitSize = new Vector2(1.4f, 1.3f);
    [Min(.1f)] public float attackRange = 1.45f;
    public LayerMask targetLayers;
    public LayerMask obstacleLayers;
    public bool allowOffscreenStart;
    public string countermeasure = "Parry or Dash; punish recovery";

    public bool IsValid => maxHp > 0 && windupFrames > 0 && activeFrames > 0 &&
        recoveryFrames > 0 && cooldownFrames > 0 && damage > 0 &&
        hitSize.x > 0 && hitSize.y > 0 && !string.IsNullOrWhiteSpace(countermeasure);
}
