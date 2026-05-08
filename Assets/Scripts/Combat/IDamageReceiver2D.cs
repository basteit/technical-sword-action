using UnityEngine;

public interface IDamageReceiver2D
{
    bool TryReceiveHit(int damage, Vector2 sourcePosition, float knockbackForce);
}
