# ケラウノス紹介サイト

公開する静的ファイルは `dist/` にあります。ローカルでは `dist/index.html` をブラウザで開けます。

## 試遊版の更新

UnityでWindows試遊版をビルドし、既存の `Tools/Build/Package-Prototype.ps1` でZIPを作成します。起動確認後、変更点と既知の問題を記したUTF-8のテキストを用意して公開します。

```powershell
./Tools/Build/Publish-Playtest.ps1 -ZipPath '<検証済みZIPのパス>' -Tag 'playtest-<日付と版>' -NotesFile '<更新内容のパス>'
```

添付名は毎回 `Keraunos-Windows.zip` になります。サイトのボタンはGitHubの最新Releaseへ接続します。公開済みタグは再利用せず、新しいタグで履歴を残します。`-WhatIf` でZIPの識別情報を確認してから公開処理を止められます。

## サイトの更新

このリポジトリ内でサイトの変更をコミットし、公開用ブランチへ反映します。

```powershell
$siteCommit = git subtree split --prefix=sites/keraunos/dist HEAD
git push origin "${siteCommit}:refs/heads/gh-pages"
```

GitHub Pagesの公開元は `gh-pages` のルートです。

## 掲載素材

- `assets/key-visual.png` は提供画像 `20261106.png` の同一コピーです。
- `assets/logo.svg` はこのチャットで作成した仮タイトルロゴです。
- 戦闘映像は2026-10-25に採用された撮影用の演出サンプルです。
- 街並みはこのチャットで使用した世界観コンセプトのラフです。
