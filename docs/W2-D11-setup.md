# W2-D11 Setup (Ranged Enemy + Straight Projectile)

## Added
- `Assets/Scripts/Enemy/EnemyRangedShooter2D.cs`
- `Assets/Scripts/Enemy/EnemyProjectile2D.cs`

## Updated
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs` (parry success stun routing to projectile owner)

## Enemy Setup
1. Create `EnemyRanged` object.
2. Add `Damageable2D`.
3. Add `EnemyRangedShooter2D`.
4. Create child `Muzzle` and assign it.
5. Assign `Projectile Prefab` and set `Target Layers` to Player layer.
6. Ensure Player object has tag `Player` (or set target manually).

## Projectile Prefab Setup
1. Create prefab with `EnemyProjectile2D`.
2. Add `Collider2D` and set `Is Trigger = ON`.
3. Optional: add SpriteRenderer for visibility.

## Validation
1. Enemy shoots straight projectile at interval.
2. Projectile damages player through existing hit system.
3. Projectile disappears on hit or lifetime timeout.
4. Parry success triggers hit stop and stuns the shooter side.
