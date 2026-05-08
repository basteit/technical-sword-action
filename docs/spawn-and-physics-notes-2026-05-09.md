# Spawn & Physics Notes (2026-05-09)

## Fixes Applied
1. Dash now can ghost through selected enemy layers.
2. Enemy no longer forced to Kinematic by default; gravity/fall/movement stays natural.
3. Contact push from player body is blocked without freezing enemy world physics.
4. Ground snap helper added for start placement.

## Inspector Settings Required
- Player `PlayerMotor2D.ignoreCollisionLayersWhileDashing`
  - Include enemy body/hitbox layers you want to pass through during dash.
- Enemy `Damageable2D.playerBodyLayers`
  - Include player body layer (usually `Player`).
- Ground snap users `GroundSnapOnStart.groundLayer`
  - Set to your floor layer.

## Recommended Spawn Pattern (general)
- Keep a `SpawnPoint` transform list per stage.
- Spawn actor prefab at spawn point.
- Immediately run one of:
  - Ground snap raycast (for ground units)
  - Air spawn (skip ground snap for flying units)
- For enemies, separate body collider and attack trigger where needed.

## Quick Check
1. Dash through enemy body works.
2. Enemy falls/moves naturally again.
3. Player touching enemy body does not push enemy around.
4. Ground units start on floor after scene load.
