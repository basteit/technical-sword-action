# W2-D13 Setup (Special Gauge Gain)

## Added
- `Assets/Scripts/Player/PlayerSpecialGauge.cs`

## Updated
- `Assets/Scripts/Player/PlayerAttack2D.cs` (gain on attack hit)
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs` (gain on damaged / parry)
- `Assets/Scripts/Player/PlayerDebugOverlay.cs` (gauge display)

## Setup
1. Add `PlayerSpecialGauge` to Player.
2. Ensure PlayerAttack2D and PlayerDamageReceiver2D are on same Player object.
3. Open debug overlay and confirm `Special Gauge` line is visible.

## Validation
1. Hit enemy: gauge increases.
2. Get damaged: gauge increases.
3. Parry success: gauge increases (Just > Normal).
4. Gauge never exceeds max or drops below 0.
