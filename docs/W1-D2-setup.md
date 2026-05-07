# W1-D2 Setup (Move + Jump + Dash)

## Added Script
- `Assets/Scripts/Player/PlayerMotor2D.cs`

## Dash Spec Reflected
- Forward quick dash
- Cooldown
- Usable in air
- Not cancelable during dash
- Gravity ignored during dash

## Scene Setup Steps
1. Create `Player` GameObject.
2. Add `Rigidbody2D` and `CapsuleCollider2D` (or BoxCollider2D).
3. Add `PlayerMotor2D` component.
4. Create child object `GroundCheck` under Player, place it at feet.
5. Assign `GroundCheck` to script field.
6. Create a ground layer (example: `Ground`) and assign floor colliders.
7. Set script `Ground Layer` to that layer mask.
8. Ensure Player localScale.x is positive initially.

## Controls (current)
- Move: A/D or Left/Right
- Jump: Space
- Dash: LeftShift or RightShift

## Validation
1. Ground move works smoothly.
2. Jump only from grounded state.
3. Dash works on ground and in air.
4. During dash, jump and move input do not interrupt dash.
5. Dash cannot be reused until cooldown ends.
