# Contributing Guide

## Branch Strategy
- `main`を唯一の長期ブランチとし、プレイ可能な統合状態を保ちます。直接コミットは禁止です。
- 実装、修正、テスト、文書更新を始める前に、対応するGitHub Issueを作成または選択します。
- 原則として`1 Issue = 1 branch = 1 Pull Request`とします。
- 作業ブランチ名は`issue/<Issue番号>-<短い英字説明>`に統一します。
- `<Issue番号>`はゼロ埋めしない数字、説明は小文字ASCIIのkebab-caseにします。
- 例: `issue/41-dash-interact`、`issue/50-player-motions`
- TrackerやMilestoneのような大きなIssueへ直接ブランチを作らず、実装可能な子Issueへ分割します。
- 原則としてIssueごとにチャット／タスクも分けます。一括実装をユーザーが明示した場合だけ、対象Issue・依存ブランチ・例外理由をPRへ記録します。
- 作業場所、UnityYAMLMerge、差分監査、保護設定の詳細は[リポジトリ運用](docs/repository-workflow.md)を参照します。

## Recommended Flow
1. Issueの目的、実装範囲、対象外、受入条件を確認します。
2. 指定フォルダでブランチと`git status`を確認し、既存の未コミット変更を保持したうえでローカルの`main`を更新します。作業中変更を巻き込む切替・pull・全件addを行いません。
3. 最新の`main`から`issue/<Issue番号>-<短い英字説明>`を作成します。
4. 一つのIssueに必要な変更だけを実装します。
5. 下記の規則に従い、区切りのよいところでこまめにコミットします。
6. 実装開始後の早い段階で`main`向けDraft PRを作成し、`Closes #<Issue番号>`を記載します。
7. 受入条件とチェックリストを満たしたら、原則Squash mergeし、作業ブランチを削除します。

## Commit Convention
Use this format:

`<type>(<scope>): <summary> (#<Issue番号>)`

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
- `feat(input): route shared B input (#41)`
- `fix(player): prevent double jump on ledge (#49)`
- `docs(workflow): document asset review flow (#38)`

### Commit Granularity

- 完成時の一括コミットにせず、テスト追加、実装の一段階、Unity Assetの接続、文書更新など、独立して説明・確認できる区切りでコミットします。
- 各コミットは可能な限りコンパイル可能で、無関係な変更を含まない状態にします。
- Assetと対応する`.meta`、または相互依存するScene・Prefab・設定は、壊れた中間状態を作らないよう同じコミットへ含めます。
- 一時ファイル、キャッシュ、個人設定、検証用生成物はコミットしません。
- 作業ブランチ内の細かな履歴は復旧とレビューに使い、`main`へはSquash mergeしてよいものとします。

## Pull Request Rules
- Keep PRs small and focused.
- One gameplay intent per PR.
- Include a test plan and result.
- Attach short clips or screenshots for gameplay changes when possible.
- Branch name must follow `issue/<Issue番号>-<短い英字説明>`.
- Include `Closes #<Issue番号>` and link any related issues.

## Definition of Done
- Feature works in play mode without console errors.
- No unintended regression in core combat loop.
- Test steps documented in PR.
- Reviewer can reproduce expected behavior.
- Compileのエラー／警告数、EditMode、PlayMode、Console、変更Assetと.meta、生成物除外を記録します。手動確認待ちは合格扱いしません。

## Daily Playtest Habit
- Run at least 10 minutes of playtesting after changes.
- Record:
  - parry success rate (rough %)
  - unfair hit count
  - one sentence on game feel
