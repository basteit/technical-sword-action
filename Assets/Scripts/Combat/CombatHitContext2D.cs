/// <summary>Identity of the actual attack; positions are never used to search for its owner.</summary>
public readonly struct CombatHitContext2D
{
    public readonly Damageable2D Source;
    public readonly long AttackId;
    public readonly bool Parryable;
    public CombatHitContext2D(Damageable2D source, long attackId, bool parryable = true)
    { Source = source; AttackId = attackId; Parryable = parryable; }
}

public interface IContextDamageReceiver2D : IDamageReceiver2D
{
    bool TryReceiveContextHit(int damage, UnityEngine.Vector2 sourcePosition, float knockbackForce, CombatHitContext2D context);
}
