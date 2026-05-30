# W3-D18 Setup (Simple Boss: 3 Actions + Phase Change)

## Added
- `Assets/Scripts/Enemy/EnemyBossController2D.cs`

## Related Update
- `Assets/Scripts/Combat/Damageable2D.cs` (HP read-only properties for phase checks)

## Boss Setup
1. Create `EnemyBoss` object.
2. Add `Collider2D`, `Rigidbody2D`, `Damageable2D`.
3. Add `EnemyBossController2D`.
4. (Optional ranged actions) Create child `Muzzle` and assign it.
5. Assign `Projectile Prefab` and layer masks.
6. Set `Damageable2D` preset to `Boss`.

## Implemented Actions
1. `MeleeArc`: close-range area hit
2. `TripleShot`: front spread projectile x3
3. `ChargeStrike`: short high-pressure dash hit

## Phase Change
- Trigger: `HpNormalized <= 0.5` (default)
- Effect:
  - action interval becomes faster
  - action damage increases

## Validation (Issue #26 DoD)
1. All 3 actions are observable in loop.
2. One phase change occurs at HP threshold.
3. No contact-only damage (damage appears only in action timing).
