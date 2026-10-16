using UnityEngine;

/// <summary>One readable attack; all durations are inclusive 60 Hz combat frames.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Enemy Attack Definition")]
public sealed class EnemyAttackDefinition : ScriptableObject
{
    public string attackId = "melee-slash";
    [Min(1)] public int windupFrames = 36;
    [Min(1)] public int activeFrames = 3;
    [Min(1)] public int recoveryFrames = 32;
    [Min(1)] public int cooldownFrames = 36;
    [Min(1)] public int damage = 14;
    [Min(0)] public float knockback = 4.5f;
    public bool parryable = true;
    public bool resistHitStunWhileAttacking;
    [Min(.1f)] public float attackRange = 1.6f;
    public Vector2 hitOffset = new Vector2(.8f, 0);
    public Vector2 hitSize = new Vector2(1.6f, 1.3f);
    public string countermeasure = "Parry or Dash; punish recovery";
    public AudioClip windupClip;

    public bool IsValid => !string.IsNullOrWhiteSpace(attackId) && windupFrames > 0 && activeFrames > 0 &&
        recoveryFrames > 0 && cooldownFrames > 0 && damage > 0 && attackRange > 0 &&
        hitSize.x > 0 && hitSize.y > 0 && !string.IsNullOrWhiteSpace(countermeasure);
}
