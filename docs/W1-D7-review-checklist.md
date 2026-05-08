# W1-D7 Review & Tuning Checklist

Date: 2026-05-08

## Implemented for Review
- Added `PlayerDebugOverlay` for state/CD/HP/FPS visibility.
- Updated `PlayerMotor2D` to optionally apply low-latency Rigidbody2D settings:
  - Interpolation: Interpolate
  - Collision Detection: Continuous

## Setup
1. Add `PlayerDebugOverlay` to Player.
2. Play and verify overlay values change with actions.
3. Keep `applyRecommendedPhysicsSettings` enabled in `PlayerMotor2D`.

## 10-Minute Playtest Log
- Session A
  - Unfair hits count:
  - Input delay feel (1-5):
  - Notes:
- Session B
  - Unfair hits count:
  - Input delay feel (1-5):
  - Notes:
- Session C
  - Unfair hits count:
  - Input delay feel (1-5):
  - Notes:

## Pass Criteria
- No critical control stutter during move/jump/dash.
- Dash cooldown and hit-lock behavior are readable.
- State transitions match expected action flow.

## Next Week Carry-over Candidates
- Add coyote time / jump buffer if jump timing feels strict.
- Add simple hit-stop on attack connect.
- Start parry prototype branch (W2-D8).
