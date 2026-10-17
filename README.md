# Technical Sword Action RPG (Prototype)

## Development Planning

期間・稼働・完成までの見通しは[完成ロードマップの計画PR #62](https://github.com/basteit/technical-sword-action/pull/62)を参照してください。従来の52週は固定納期とせず、素材納期とステージ1の実績から更新します。新規作業はIssueごとの `issue/<番号>-<短い内容>` ブランチ、小さなコミット、1 Issue = 1 PRで進めます。

## First Setup Checklist
1. Create a Unity project in this folder with Unity Hub (2D URP recommended).
2. Open project once and confirm Unity generates `Assets`, `Packages`, and `ProjectSettings`.
3. In GitHub Desktop, add this local repository.
4. Commit initial files.
5. Publish repository to GitHub.
6. 開発作業を始める前にGitHub Issueを作成し、最新の`main`からIssue用ブランチを作成する。

## Branch Rules
- 長期ブランチは`main`だけとし、直接コミットしない。
- すべての実装・修正・文書作業は、原則として`1 Issue = 1 branch = 1 Pull Request`で進める。
- 作業ブランチ名は`issue/<Issue番号>-<短い英字説明>`とする。
- 例: `issue/41-dash-interact`、`issue/50-player-motions`
- 区切りのよい作業単位でこまめにコミットし、PRは`main`を対象にする。

See `CONTRIBUTING.md` for full workflow.

UnityYAMLMergeの端末設定、Assetと.metaの監査、main保護とSquash mergeの規則は[リポジトリ運用](docs/repository-workflow.md)を参照してください。Issueごとにチャット／タスクを分け、一括実装は明示的に依頼された場合だけ例外として記録します。

戦闘プロトタイプの確認は[人間向け統合チェックリスト](docs/prototype-human-acceptance.md)を参照してください。

## Art Workflow

主人公ドット絵のサイズ、足裏基準、Asepriteタグ、Unityへの追加・差し替え手順は
[`Assets/Art/Player/Prototype/README.md`](Assets/Art/Player/Prototype/README.md) を参照してください。
