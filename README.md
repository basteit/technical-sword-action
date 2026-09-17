# Technical Sword Action RPG (Prototype)

## Development Planning

期間・稼働・完成までの見通しは[完成ロードマップ（2026-09-05）](docs/planning/completion-roadmap-2026-09-05.md)を参照してください。従来の52週は固定納期とせず、素材納期とステージ1の実績から更新します。新規作業はIssueごとの `issue/<番号>-<短い内容>` ブランチ、小さなコミット、1 Issue = 1 PRで進めます（運用移行は#38）。

## First Setup Checklist
1. Create a Unity project in this folder with Unity Hub (2D URP recommended).
2. Open project once and confirm Unity generates `Assets`, `Packages`, and `ProjectSettings`.
3. In GitHub Desktop, add this local repository.
4. Commit initial files.
5. Publish repository to GitHub.
6. Create and switch to `develop` branch.

## Branch Rules
- `main`: stable playable milestones
- `develop`: daily integration
- `feature/*`: feature implementation
- `fix/*`: bug fix

See `CONTRIBUTING.md` for full workflow.

## Art Workflow

主人公ドット絵のサイズ、足裏基準、Asepriteタグ、Unityへの追加・差し替え手順は
[`Assets/Art/Player/Prototype/README.md`](Assets/Art/Player/Prototype/README.md) を参照してください。
