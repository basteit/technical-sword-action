# W3-D17 Setup (Break / Stagger Resistance)

## Updated
- `Assets/Scripts/Combat/Damageable2D.cs`

## Added Parameters
- `Toughness Preset`: `MeleeMob` / `RangedMob` / `Boss`
- `Break Threshold`
- `Break Decay Per Second`
- `Hit Break Multiplier`
- `Parry Normal Break Gain`
- `Parry Just Break Gain`
- `Break Stun Duration`
- `Break Resist After Stun`

## Behavior
- Hits and parry-success both add break value.
- Break value decays over time while not stunned.
- When threshold is reached, target enters break-stun and break value resets.
- Short resist window prevents immediate re-break loop.

## Validation (Issue #25 DoD)
1. Hit / parry success updates break-related value.
2. Threshold reach causes clear state change (stun).
3. Tuning values creates visible behavior difference across enemy presets.
