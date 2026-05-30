# W3-D19 Setup (Combat Camera / Shake Feedback)

## Added
- `Assets/Scripts/Combat/CombatCameraFeedback2D.cs`

## Updated
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`
- `Assets/Scripts/Combat/Damageable2D.cs`

## Scene Setup
1. Add an empty object `CombatFeedback`.
2. Add `CombatCameraFeedback2D`.
3. Leave `Camera Root` empty to auto-bind `Camera.main`, or assign manually.

## Feedback Routing
- Player damaged: `PlayHitShake()`
- Enemy damaged: `PlayHitShake()`
- Parry success: `PlayParryShake(result)`

## Recommended Initial Values (Low Motion Sickness)
- `Default Duration`: 0.08
- `Default Magnitude`: 0.08
- `Parry Duration`: 0.12
- `Parry Magnitude`: 0.14
- `Just Parry Duration`: 0.16
- `Just Parry Magnitude`: 0.20
- `Damping`: 20

## Validation (Issue #27 DoD)
1. Strong actions produce clear visual feedback.
2. Normal play remains readable (no excessive shake).
3. 10+ minute play does not cause strong discomfort.
