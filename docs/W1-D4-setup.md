# W1-D4 Setup (Player State Machine)

## Added Script
- `Assets/Scripts/Player/PlayerStateMachine.cs`

## Updated Scripts
- `Assets/Scripts/Player/PlayerMotor2D.cs` (state reference properties)
- `Assets/Scripts/Player/PlayerAttack2D.cs` (state reference properties)

## States (current)
- Idle
- Move
- Jump
- Fall
- Dash
- Attack
- Hit (reserved)

## Setup
1. Select `Player` object.
2. Add `PlayerStateMachine` component.
3. Ensure `PlayerMotor2D` and `PlayerAttack2D` are on the same object.
4. (Optional) Enable `Debug Log State Change` for transition logs.

## Validation
1. Stand still -> `Idle`
2. Move on ground -> `Move`
3. Upward in air -> `Jump`
4. Downward in air -> `Fall`
5. During dash -> `Dash`
6. During attack window -> `Attack`
