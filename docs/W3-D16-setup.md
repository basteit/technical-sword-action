# W3-D16 Setup (Ranged Enemy AI: Spacing + Shooting)

## Updated
- `Assets/Scripts/Enemy/EnemyRangedShooter2D.cs`

## Behavior
- Keeps distance band from player (`Preferred Min Distance` to `Preferred Max Distance`)
- Too close: retreats
- Too far: approaches
- Shoots only while inside preferred band

## Recommended Initial Parameters
- `Detect Range`: 12
- `Lose Target Range`: 16
- `Move Speed`: 2.6
- `Preferred Min Distance`: 4.5
- `Preferred Max Distance`: 7.2
- `Near Stop Distance`: 0.75
- `Shoot Interval`: 1.2
- `Projectile Speed`: 10

## Validation (Issue #24 DoD)
1. Ranged enemy keeps a readable spacing and does not stick to the player.
2. Player can choose evade / parry / reflect against projectiles.
3. With melee enemy present, ranged behavior still keeps spacing and continues firing cycles.
