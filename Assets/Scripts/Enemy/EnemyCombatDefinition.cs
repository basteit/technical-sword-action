using UnityEngine;

public enum EnemyCombatRank { Normal, Variant, Elite, MidBoss, Boss }
public enum EnemyHitStrength { Light, Strong }

/// <summary>Combat values for a readable melee attack, in inclusive 60 Hz frames.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Enemy Combat Definition")]
public sealed class EnemyCombatDefinition : ScriptableObject
{
    public EnemyCombatRank rank = EnemyCombatRank.Normal;
    [Min(1)] public int maxHp = 30;
    [Header("Individual tuning (not an instant-kill rule)")]
    public bool overrideCounterMultiplier;
    [Min(.01f)] public float counterMultiplier = 3f;
    [Min(0)] public int lightHitStunFrames = 6;
    [Min(0)] public int strongHitStunFrames = 16;
    [Min(0)] public int lightStunResistanceFrames = 18;
    [Min(0)] public float hitKnockback = .5f;
    [Min(0)] public int normalParryStunFrames = 32;
    [Min(0)] public int justParryStunFrames = 44;
    [Tooltip("Counter opportunity and visible stagger after either successful parry, in 60 Hz combat ticks.")]
    [Min(1)] public int parryStaggerFrames = 180;
    [Tooltip("Optional attack sequence. Empty preserves the original single-attack settings below.")]
    public EnemyAttackDefinition[] attacks = new EnemyAttackDefinition[0];
    [Min(0)] public float moveSpeed = 2.2f;
    [Min(1)] public int windupFrames = 30;
    [Min(1)] public int activeFrames = 3;
    [Min(1)] public int recoveryFrames = 30;
    [Min(1)] public int cooldownFrames = 32;
    [Min(1)] public int damage = 10;
    [Min(0)] public float knockback = 4.5f;
    public bool parryable = true;
    [Tooltip("Fallback reaction for the single attack below. Per-attack definitions override this value.")]
    public ParryReactionWeight parryReaction = ParryReactionWeight.Light;
    public Vector2 hitOffset = new Vector2(.7f, 0);
    public Vector2 hitSize = new Vector2(1.4f, 1.3f);
    [Min(.1f)] public float attackRange = 1.45f;
    public LayerMask targetLayers;
    public LayerMask obstacleLayers;
    public bool allowOffscreenStart;
    public string countermeasure = "Parry or Dash; punish recovery";

    public float CounterMultiplier => overrideCounterMultiplier ? counterMultiplier : DefaultCounterMultiplier(rank);
    public static float DefaultCounterMultiplier(EnemyCombatRank value) => value == EnemyCombatRank.Elite ? 2f :
        value == EnemyCombatRank.MidBoss || value == EnemyCombatRank.Boss ? 1.5f : 3f;

    public bool IsValid
    {
        get
        {
            if (maxHp <= 0 || !float.IsFinite(CounterMultiplier) || CounterMultiplier <= 0 ||
                lightHitStunFrames < 0 || strongHitStunFrames < 0 || lightStunResistanceFrames < 0 || parryStaggerFrames <= 0) return false;
            if (attacks != null && attacks.Length > 0)
            {
                foreach (var attack in attacks) if (attack == null || !attack.IsValid) return false;
                return true;
            }
            return windupFrames > 0 && activeFrames > 0 && recoveryFrames > 0 && cooldownFrames > 0 && damage > 0 &&
                hitSize.x > 0 && hitSize.y > 0 && !string.IsNullOrWhiteSpace(countermeasure);
        }
    }
}
