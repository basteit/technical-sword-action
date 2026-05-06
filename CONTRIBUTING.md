# Contributing Guide

## Branch Strategy
- `main`: stable branch for playable builds.
- `develop`: integration branch for daily development.
- `feature/<topic>`: feature work branch (example: `feature/parry-window`).
- `fix/<topic>`: bug fix branch (example: `fix/hitstop-jitter`).

## Recommended Flow
1. Update `develop` locally.
2. Create a new branch from `develop`.
3. Implement one focused change.
4. Commit with the commit convention below.
5. Open a PR into `develop`.
6. Merge after checklist is satisfied.
7. Periodically merge `develop` into `main` for stable milestones.

## Commit Convention
Use this format:

`<type>(<scope>): <summary>`

Types:
- `feat`: new feature
- `fix`: bug fix
- `refactor`: internal improvement without behavior change
- `perf`: performance improvement
- `test`: test addition/update
- `docs`: documentation update
- `chore`: tooling/config/maintenance

Scopes (Unity project examples):
- `player`
- `enemy`
- `combat`
- `ui`
- `audio`
- `vfx`
- `build`

Examples:
- `feat(combat): add parry success stun state`
- `fix(player): prevent double jump on ledge`
- `docs(workflow): add playtest checklist`

## Pull Request Rules
- Keep PRs small and focused.
- One gameplay intent per PR.
- Include a test plan and result.
- Attach short clips or screenshots for gameplay changes when possible.
- Link related issue(s).

## Definition of Done
- Feature works in play mode without console errors.
- No unintended regression in core combat loop.
- Test steps documented in PR.
- Reviewer can reproduce expected behavior.

## Daily Playtest Habit
- Run at least 10 minutes of playtesting after changes.
- Record:
  - parry success rate (rough %)
  - unfair hit count
  - one sentence on game feel
