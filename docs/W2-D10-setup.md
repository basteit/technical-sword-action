# W2-D10 Setup (Parry Fail Risk)

## Updated
- `Assets/Scripts/Player/PlayerParry2D.cs`
- `Assets/Scripts/Player/PlayerMotor2D.cs`
- `Assets/Scripts/Player/PlayerAttack2D.cs`
- `Assets/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Scripts/Player/PlayerDebugOverlay.cs`

## Behavior
- If parry window ends without success, fail lock starts.
- During fail lock, move/jump/dash/attack are disabled.
- State becomes `ParryFail` while fail lock is active.

## Tuning
- `PlayerParry2D.failLockDuration` start: `0.20`

## Validation
1. Press `K` and do not receive attack -> fail lock occurs.
2. During fail lock, no actions are accepted.
3. HUD `Parry FailLock` counts down.
4. After fail lock ends, controls recover normally.
