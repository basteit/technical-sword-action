# W3-D15 Setup (Melee Enemy AI: Approach -> Attack -> Recovery)

## Added
- `Assets/Scripts/Enemy/EnemyMeleeChaser2D.cs`

## Enemy Setup
1. Create `EnemyMelee` object.
2. Add `Collider2D` (non-trigger) for body collision.
3. Add `Damageable2D`.
4. Add `EnemyMeleeChaser2D`.
5. Set `Attack Target Layers` to include the Player layer.
6. Ensure Player object has tag `Player` (or assign `Target` manually).

## Recommended Initial Parameters
- `Detect Range`: 10
- `Lose Target Range`: 14
- `Move Speed`: 3.2
- `Preferred Min Distance`: 1.05
- `Preferred Max Distance`: 1.7
- `Attack Range`: 1.25
- `Attack Windup Duration`: 0.18
- `Recovery Duration`: 0.45
- `Cooldown Duration`: 0.55

## Contact Damage Policy
- Enemy body contact should not damage the player.
- Keep `Disable Contact Damage Sources` enabled on `EnemyMeleeChaser2D`.
- Damage is applied only during attack execution timing.

## Validation (Issue #23 DoD)
1. Enemy approaches the player in detection range.
2. Enemy keeps a readable distance band and does not stick to the player continuously.
3. After attack, there is a clear punish window (recovery + cooldown).
4. Enemy does not loop instant attacks (cooldown enforces spacing).
