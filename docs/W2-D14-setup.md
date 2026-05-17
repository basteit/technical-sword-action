# W2-D14 Setup (Special Skill)

## Added
- `Assets/Scripts/Player/PlayerSpecialSkill2D.cs`

## Updated
- `Assets/Scripts/Player/PlayerMotor2D.cs`
- `Assets/Scripts/Player/PlayerParry2D.cs`
- `Assets/Scripts/Player/PlayerAttack2D.cs`
- `Assets/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Scripts/Player/PlayerDebugOverlay.cs`

## Controls
- Special skill: `L`

## Required Setup
1. Add `PlayerSpecialSkill2D` to Player.
2. Assign `Special Gauge` reference (or keep auto-find on same object).
3. Create child `SkillPoint` and assign it.
4. Set `Target Layers` to enemy damageable layers.

## Validation
1. If gauge is below required amount, skill does not trigger.
2. If gauge is enough, skill starts and consumes gauge.
3. During skill lock, move/jump/dash/attack/parry are blocked.
4. Skill deals high area damage near `SkillPoint`.
5. `Special Active` and lock remaining are visible on HUD.
