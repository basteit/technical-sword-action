# W2-D12 Setup (Projectile Reflection)

## Updated
- `Assets/Scripts/Enemy/EnemyRangedShooter2D.cs`
- `Assets/Scripts/Enemy/EnemyProjectile2D.cs`

## Behavior
- If player parries projectile successfully, projectile is not destroyed.
- Projectile direction flips toward shooter (owner).
- Projectile ownership target layer is switched to `reflectedHitLayers`.
- Reflected projectile ignores immediate collision with player collider.

## Inspector Setup
1. On `EnemyRangedShooter2D`:
   - `Projectile Target Layers` = Player layer
   - `Reflected Hit Layers` = Enemy layer (or enemy hurtbox layer)
2. Keep projectile collider `Is Trigger = ON`.

## Validation
1. Normal hit: projectile damages player.
2. Parry hit: projectile reflects and survives.
3. Reflected projectile damages enemy side.
4. Reflected projectile does not instantly re-hit player.
