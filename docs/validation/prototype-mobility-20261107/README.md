# 受領移動素材・近距離パリィの検証（2026-11-07）

Unity 6000.3.9f1 / ULoop CLI 2.1.3。作業ブランチ `issue/84-mobility-and-proximity-parry`、依存基点 `0a8c6b5`、パリィ実装 `9e7eb9e` と今回の移動接続を確認した。ユーザーの既存未コミット変更を保持した環境であり、クリーンcheckoutの保証ではない。

- Compile: Error0 / Warning0。
- EditMode全体: 343/343。
- 最終の追加PlayMode: ProximityParry25 + Mobility12 = 37/37。
- PlayMode全体: 928中915合格、13失敗、Skip0。失敗を含む結果として保持する。
- 新しい近距離probeを一時OFFにした比較: 24中11合格・13失敗。全体と失敗名/Assertion文が13件とも一致。比較後は実装をONへ戻し37件を再確認した。
- 原稿管理20/20、台帳validate合格。添付=Current=HistoryのSHA-256一致。
- 原稿ガイドだけを出力から除去。4Clip/ArtSetと非対象のScene/設定/Motor/動画Libraryを含む10ファイルで再更新後のSHA-256不変。
- 最終Console Error0 / Warning0。手動時計/入力/表示を復帰し、敵なしでPlayModeを終了。

## Game View

1920×1080、通常Orthographic Size5.625。拡大/切り抜きしていない。撮影時だけ左上の詳細診断Overlayを非表示にした。画面右側の操作/比較パネルはそのまま。頂点6でヒットストップ時のSprite/位置の不変も確認した。

- [踏切2コマ目](Rendering_20261107_070923_634.png)
- [頂点6コマ目](Rendering_20261107_071008_648.png)
- [落下7コマ目](Rendering_20261107_071019_525.png)
- [左Dash3コマ目](Rendering_20261107_071031_631.png)

## 未合格・未実施

既存動画の揺れ2、停止時間3、防御キャンセル4段版7、旧5F-Just期待1の計13失敗は未修正。[接続記録](../../prototype-mobility-and-proximity-parry.md)に切り分けを記載した。人の15分操作、物理パッド、Development Build/ZIP、髪/着地の制作と最終受入は未実施。

全PlayModeのCLIは180秒でタイムアウトしたがUnity側は継続した。最終XMLのstart/endは2026-11-06 21:54:16Z / 21:58:42Z、所要265.485051秒。Consoleの保存案内から読み取り、作業用コピーを `.uloop/outputs/TestResults/full-playmode-mobility-20261107.xml` へ保持した。比較XMLは `.uloop/outputs/TestResults/20261107_070328.xml`。生成XMLはGit管理しない。
