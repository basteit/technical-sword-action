# GitHub Flow・Unity資産の運用（#38）

## 通常の作業単位

1 Issue = 1チャット／タスク = 1ブランチ = 1 PRを原則とする。ユーザーが複数Issueの一括実装を明示した場合だけ例外を記録し、Issueごとの意図で小さくコミットする。別worktreeはユーザー指定時だけ使う。

開始時に指定フォルダでブランチと `git status` を確認する。未コミット変更がある状態で機械的なcheckout、pull、stash、全件addをせず、変更の所有と依存を確認する。新規作業は最新mainから `issue/<番号>-<小文字ASCIIのkebab-case>` を作る。進行中の依存ブランチは途中で改名・強制rebaseせず、基点SHAと未統合PRを記録する。

独立したロジック＋テスト、Unity Assetの接続、文書＋検証記録など、説明・確認できる区切りで `<type>(<scope>): <summary> (#<番号>)` のコミットを作る。Assetと.meta、相互依存する設定は同時に含める。今回の明示例外は `(#33, #52, #38)` のように関連番号を併記してよい。既存の無関係な差分は含めない。

早期にmain向けDraft PRを作り、Closes、依存、目的、悪化し得る点、受入条件、検証結果を記録する。人の未実施テストはチェックを入れない。合格後にDraft解除、Squash merge、作業ブランチ削除を行う。mainへの直接push、force push、未検証のdevelop統合は行わない。

## Unity資産

UnityはVisible Meta Files / Force Textを使う。`.unity`、`.prefab`、`.asset`、`.controller`、`.anim`などは `.gitattributes` のUnityYAMLMergeドライバーで補助する。各端末で次を一度実行する。

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Repository\Configure-UnityMerge.ps1
```

別のEditor配置では `-UnityEditor 'D:\Unity\6000.3.9f1\Editor'` を指定する。設定は `.git/config` のローカル設定であり、絶対パスや認証情報を共有ファイルへ保存しない。Smart Mergeが見つからない場合は設定せず停止する。解決後は必ずUnityで再インポート・コンパイル・参照・PlayModeを確認する。競合したScene／Prefab／Animatorは一時的に編集担当を一人へ絞る。

`.meta`のGUIDを再生成して競合を隠さない。Aseprite、PNG、音素材はバイナリとして扱い、自動テキストマージしない。生成物のLibrary、Temp、Build、ログ、`.uloop/outputs/`、Pythonキャッシュは追跡しない。残すテスト要約だけを `docs/validation/` へ選んで記録する。Git LFSの一括移行はしない。

## 差分の確認

```powershell
# addしたファイルだけ確認する。未コミットの他作業は巻き込まない。
powershell -ExecutionPolicy Bypass -File .\Tools\Repository\Test-Repository.ps1 -Staged

# 今回の作業の基点以降だけを監査する例
powershell -ExecutionPolicy Bypass -File .\Tools\Repository\Test-Repository.ps1 -BaseRef 10ec28e

# Unityはcompile → 設定参照 → EditMode → PlayMode → Consoleを逐次確認
powershell -ExecutionPolicy Bypass -File .\Tools\Prototype\Validate-Combat.ps1
```

基点監査は選んだ範囲だけの検証であり、PR全体に未統合依存がある場合はそれもレビューする。単純なファイル存在監査は実際のScene参照検証の代わりにならない。

## GitHub設定

`.github/main-ruleset.json` がこのリポジトリ向けの宣言。mainのみを対象にPR経由、削除禁止、force push禁止、線形履歴、会話解決を要求する。承認人数は0、Code Owner／最新pushの別人承認／未安定CIのStatus Checkは要求しない。マージ方式はSquash、統合後の作業ブランチは自動削除を基本とする。

設定変更は管理権限を持つ認証で行い、適用後のAPI読取りを記録する。PRルールに自分自身の承認を必要とする条件を追加しない。CIが安定してから別Issueで必須チェックへ昇格する。

旧developの廃止は、最新のremoteを取得し、mainにない固有コミット0件、default branchでないこと、開いているPRのbase/headでないこと、Actions等の実行設定に参照がないことを確認してから行う。歴史資料での言及は運用上の参照とは区別する。mainへの旧developマージやforce削除は行わない。
