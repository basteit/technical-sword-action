# W2-D8 Setup (Parry Window: Normal / Just)

## Added
- `Assets/Scripts/Player/PlayerParry2D.cs`

## Updated
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`
- `Assets/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Scripts/Player/PlayerDebugOverlay.cs`

## Controls
- Parry: `K`

## Required Setup
1. Add `PlayerParry2D` to Player.
2. Ensure `PlayerDamageReceiver2D` is on same object.
3. Ensure `PlayerDebugOverlay` is on Player to see parry result.

## Window Defaults
- Parry window: `0.18s`
- Just window: `0.06s` (window start from parry input)
- Cooldown: `0.12s`

## Validation
1. Press `K` then get hit immediately -> `Parry Result: Just`.
2. Press `K` and get hit near end of window -> `Parry Result: Normal`.
3. Get hit outside window -> normal damage.
4. HUD shows `Parry Active` and remaining time.
