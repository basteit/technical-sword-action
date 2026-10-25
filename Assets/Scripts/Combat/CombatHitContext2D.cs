/// <summary>Attack-authored player reaction, independent of normal/just parry timing.</summary>
public enum ParryReactionWeight { Light = 0, Heavy = 1 }

/// <summary>Identity of the actual attack; positions are never used to search for its owner.</summary>
public readonly struct CombatHitContext2D
{
    public readonly Damageable2D Source;
    public readonly long AttackId;
    public readonly bool Parryable;
    public readonly ParryReactionWeight ParryReaction;
    public CombatHitContext2D(Damageable2D source, long attackId, bool parryable = true)
        : this(source, attackId, parryable, ParryReactionWeight.Light) { }
    public CombatHitContext2D(Damageable2D source, long attackId, bool parryable, ParryReactionWeight parryReaction)
    { Source = source; AttackId = attackId; Parryable = parryable; ParryReaction = parryReaction; }
}

public interface IContextDamageReceiver2D : IDamageReceiver2D
{
    bool TryReceiveContextHit(int damage, UnityEngine.Vector2 sourcePosition, float knockbackForce, CombatHitContext2D context);
}
