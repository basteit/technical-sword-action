# W1-D3 Setup (Normal Attack 1-2 Combo)

## Added Scripts
- `Assets/Scripts/Player/PlayerAttack2D.cs`
- `Assets/Scripts/Combat/Damageable2D.cs`

## Controls (current)
- Attack: `J`

## Player Setup
1. Select `Player` object.
2. Add `PlayerAttack2D`.
3. Create child object `HitPoint` and place it in front of player.
4. Assign `HitPoint` to `PlayerAttack2D.hitPoint`.
5. Set `Target Layers` to include enemy layer (example: `Enemy`).

## Enemy Dummy Setup
1. Create `EnemyDummy` object with `SpriteRenderer`.
2. Add `Collider2D` (not trigger).
3. Add `Rigidbody2D` (Dynamic or Kinematic as needed).
4. Add `Damageable2D`.
5. Set layer to `Enemy`.

## Validation
1. Press `J` once: first hit should apply.
2. Press `J` twice quickly: second hit should apply stronger damage.
3. Wait longer than combo reset time, press `J`: combo returns to first hit.
4. Confirm hit only occurs when target is inside hit radius.
