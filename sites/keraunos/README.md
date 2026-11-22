# ケラウノス紹介サイト

[公開サイト](https://basteit.github.io/technical-sword-action/)

公開する静的ファイルは `dist/` にあります。ローカルでは `dist/index.html` をブラウザで開けます。

## タイトル画面の確認

[確認ページ](https://basteit.github.io/technical-sword-action/title-preview/?v=dim4)に、部屋を暗くしてロゴの通常発光を微弱にしたタイトルの12秒・無音動画と、登場直後／通常時の静止画を掲載しています。映像は1280×720・30fpsのH.264 MP4です。動画と画像は版ごとのファイル名にして、前回のキャッシュと区別しています。

ゲーム内では `EggTitleRoomProfile` の画面矩形、ロゴの発光、登場時の強調、部屋の光を調整します。`EggTitlePreviewCapture` はPlayModeの通常GameViewを固定の時間刻みで直接撮影します。撮影用カメラへの切り替えやCanvasの描画方式の変更は行いません。照明座標は `EggRoomLightingCoordinates` がUIメッシュのUV1へ格納し、Overlay描画や拡大時の座標ずれを防ぎます。

## 試遊版の更新

UnityでWindows試遊版をビルドし、既存の `Tools/Build/Package-Prototype.ps1` でZIPを作成します。起動確認後、変更点と既知の問題を記したUTF-8のテキストを用意して公開します。

```powershell
./Tools/Build/Publish-Playtest.ps1 -ZipPath '<検証済みZIPのパス>' -Tag 'playtest-<日付と版>' -NotesFile '<更新内容のパス>'
```

添付名は毎回 `Keraunos-Windows.zip` になります。サイトのボタンはGitHubの最新Releaseへ接続します。公開済みタグは再利用せず、新しいタグで履歴を残します。`-WhatIf` でZIPの識別情報を確認してから公開処理を止められます。

## サイトの更新

このリポジトリ内でサイトの変更をコミットし、公開用ブランチへ反映します。

```powershell
$siteCommit = git subtree split --quiet --prefix=sites/keraunos/dist HEAD
git push origin "${siteCommit}:refs/heads/gh-pages"
```

GitHub Pagesの公開元は `gh-pages` のルートです。

## 掲載素材

- `assets/key-visual.png` は提供画像 `20261106.png` の同一コピーです。
- `assets/logo.svg` はこのチャットで作成した仮タイトルロゴです。
- 戦闘映像は2026-10-25に採用された撮影用の演出サンプルです。
- 街並みはこのチャットで使用した世界観コンセプトのラフです。
