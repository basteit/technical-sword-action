# W1-D6 Setup (Placeholder Visuals + Minimal SFX)

## Updated Scripts
- `Assets/Scripts/Player/PlayerAttack2D.cs`
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`
- `Assets/Scripts/Combat/Damageable2D.cs`

## Goal
- Attack swing sound
- Hit confirm sound
- Player hit sound
- Minimal hit flash for readability

## Quick Asset Prep
1. Create folders if needed:
   - `Assets/Art/Placeholders`
   - `Assets/Audio/SFX`
2. Put temporary sprites and SFX clips in those folders.

## Player Setup
1. `Player` needs `AudioSource`.
2. In `PlayerAttack2D` assign:
   - `Attack Swing Clip`
   - `Hit Confirm Clip`
3. In `PlayerDamageReceiver2D` assign:
   - `Hit Clip`
   - `Sprite Renderer` (player visual)

## Enemy Dummy Setup
1. `EnemyDummy` needs `AudioSource` (optional but recommended).
2. In `Damageable2D` assign:
   - `Hit Clip`
   - `Sprite Renderer` (enemy visual)

## Validation
1. Attack with `J` plays swing sound.
2. When hit connects, hit-confirm sound plays.
3. On player hit, player hit sound + brief flash occurs.
4. On enemy hit, enemy flash occurs (and optional hit sound).
