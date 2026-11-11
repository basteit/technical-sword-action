# 提出された敵5モーションの接続（2026-10-19）

撮影SceneとStudioの共通敵を、提出された `EnemySample` の5原稿へ差し替えた。`down` は撃破、`chance` はパリィで弾かれた反応として接続する。主人公の作画・時間配分と、本編の別敵Prefabは変更しない。

## 素材と接続

原本は [ArtSource/Enemy](../../ArtSource/Enemy/README.md) の `Submissions/20261019/` へ変更せず保存した。5点すべてDownloadsの提出ファイルとバイト一致。Unity用の36枚は `Assets/Art/Enemy/Submitted/20261019/`。原稿内の黒いガイドレイヤーだけを出力時に非表示とし、Body/Effectを保持する。

| 原稿 | ゲーム内の接続 |
| --- | --- |
| idle | 待機、10枚・1秒ループ |
| walk | 歩行、10枚・1秒ループ |
| attack | 1〜7枚目を予兆30tick、8〜14枚目を攻撃16tick |
| chance | パリィやられ。通常被弾にも一時流用 |
| down | 撃破。命中停止後に表示し、既存の吹っ飛び・消滅へ接続 |

攻撃はエフェクトが伸びる8枚目を発生へ合わせる。原稿の表示時間は保存し、ゲーム用の予兆/攻撃Clipだけを既存の戦闘時刻に合わせて短縮する。32PPU/Point、Pivot=(32px,38px)。通常64px・攻撃128pxのキャンバスを維持する。

再出力は `Tools/Art/export-enemy-submissions.lua`、再適用はUnityメニュー `Tools > Technical Sword Action > Video > Apply Submitted Enemy`。通常の動画素材再構築からも提出版を再適用する。更新原稿は日付/版を増やして保存し、受領済み原本を上書きしない。

## エフェクトの描画順

最新のユーザー指示に合わせ、敵を原画の攻撃軌跡より手前へ上げる処理を解除した。敵は通常の描画順8、主人公の作画済み軌跡は10。電撃の衝撃輪/枝状放電も30/31へ上げ、敵より前に表示する。命中停止中の位置/姿勢保持、8F停止、停止解除後の吹っ飛びは維持する。前版の「エフェクトを人物の後ろへ置く」は今回の指示で更新した。

## 最小限の確認と録画

- Unity compile: Error 0 / Warning 0。
- 関連PlayMode **6/6合格**。30/60/120fps×左右で、敵に対する軌跡/衝撃輪/放電の描画順、命中時の接触保持、Pause/Resetを確認。
- 新しい敵の待機→歩行→攻撃→弾かれ→撃破を通しで録画し、全242Fをデコード確認。パリィと反撃対象はいずれもEnemy_B。
- 収録後Consoleは全種0件。`PrototypeCombatValidation.unity` の保存済みEditMode、未保存変更なしへ戻した。
- 使用量を考慮する最新指示に従い、全EditMode/PlayModeと80回Resetは今回は再実行していない。前版の全件合格を今回の敵差し替え後の結果とは扱わない。

[MP4](previews/combat-2stage-hybrid-enemy-submitted-20261019.mp4)：**4.033333秒 / 242F / 1920×1080 / 60fps / 無音**。保存CRF16、チャット内プレビューは同解像度/fps/全コマのCRF22。

[イベント表](previews/combat-2stage-hybrid-enemy-submitted-20261019.json) / [通し画像](previews/enemy-submitted-20261019-contact.png) / [検証JSON](previews/enemy-submitted-20261019-validation.json)。再収録は `CombatVideo.unity` のPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、その後 `Tools/Video/encode-captures.ps1 -Revision enemy-submitted-20261019 -Variants 2stage-hybrid`。

これは提出素材の接続確認であり、全体の最終受入とは分ける。#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
