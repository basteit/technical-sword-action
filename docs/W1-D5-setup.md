# W1-D5 Setup (Hit + Knockback + Invincibility)

## Added Scripts
- `Assets/Scripts/Combat/IDamageReceiver2D.cs`
- `Assets/Scripts/Combat/DamageSource2D.cs`
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`

## Updated
- `Assets/Scripts/Player/PlayerMotor2D.cs`
- `Assets/Scripts/Player/PlayerStateMachine.cs`

## Player Setup
1. Select `Player` object.
2. Add `PlayerDamageReceiver2D`.
3. Ensure `Rigidbody2D` exists.
4. Keep `PlayerMotor2D` and `PlayerStateMachine` on same object.

## Enemy Attack Dummy Setup
1. Create `EnemyHitbox` object.
2. Add `Collider2D` and set `Is Trigger = ON`.
3. Add `DamageSource2D`.
4. Set `Target Layers` to include Player layer.
5. Move this hitbox to collide with player while testing.

## Validation
1. First hit reduces HP and applies knockback.
2. During invincibility window, overlapping hitbox should not multi-hit.
3. Short hit lock occurs and movement pauses briefly.
4. `PlayerStateMachine` becomes `Hit` during hit lock.
