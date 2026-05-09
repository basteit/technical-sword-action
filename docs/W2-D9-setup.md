# W2-D9 Setup (Parry Success: Stun + Hit Stop)

## Updated
- `Assets/Scripts/Combat/Damageable2D.cs`
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`

## Behavior
- Normal parry: shorter enemy stun + short hit stop
- Just parry: longer enemy stun + stronger hit stop

## Inspector Tuning
- `PlayerDamageReceiver2D.normalParryHitStop`: start `0.04`
- `PlayerDamageReceiver2D.justParryHitStop`: start `0.07`
- `Damageable2D.normalParryStunDuration`: start `0.22`
- `Damageable2D.justParryStunDuration`: start `0.34`

## Validation
1. Successful parry causes nearby attacker to pause/stun.
2. Just parry pauses longer than normal parry.
3. Failed parry should not trigger stun/hit stop.
4. Time scale returns to normal after hit stop.
