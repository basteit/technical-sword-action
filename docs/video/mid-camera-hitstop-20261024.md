# 中間の撮影距離と少し重いヒットストップ（2026-10-24）

2本の比較に対する指示を受け、見た目の大きさが中間になる距離を採用した。1080p/32PPUで通常は原画5倍（ortho3.375）、カウンターは6倍（2.8125）。比較元の通常6/4倍、アップ7/5倍の中間に当たり、ドットを整数倍率で表示できる。

主撮影・攻防切替・Studio/5プリセットと生成初期値を揃えた。Studioの標準は5倍。本編のカメラや原稿は変更しない。

## 停止時間

| 命中 | 前版 | 更新 | 実時間 |
| --- | ---: | ---: | ---: |
| 通常パリィ成功 | 2F | 4F | 約0.067秒 |
| Justパリィ成功 | 3F | 5F | 約0.083秒 |
| カウンターのキック | 8F | 10F | 約0.167秒 |

撮影/Studioの停止だけを延長し、パリィの反応種別は軽のまま。通常斬撃の2F停止、スロー0.900秒、振り抜きと蹴り後の戦闘tick配分、VFXの色/大きさ、揺れは保持。本編のFeedback/Profileの停止設定は変更しない。キック停止はVideoCombatPresentationのKick Hitstop Framesで調整できる。

共通戦闘時計を停止するので、人物・敵の姿勢/位置、物理、キックの独立電撃は一緒に止まる。カメラの揺れ等の既存実時間演出はそのまま。Pauseでは停止残り時間も凍結する。

## 確認と収録

Compile Error/Warning0。関連PlayMode **14/14**：左右の通常パリィ4wall slot・キック10wall slotを実測し、途中Pauseで停止時間が消費されないこと、接触姿勢/電撃/対象位置が保たれること、次slotで再開しResetで残留しないことを新規2ケースで確認。他に30/60/120fps×左右の接触/VFXとStudioの反撃/復元を確認した。全件テストや従来80回Resetは今回再実行していない。

新MP4は **310F / 5.166667秒・1920×1080・60fps・無音**。全310Fデコード。前版と全イベントの種類/対象/戦闘tickは一致。パリィ成功時刻は2.966667秒のまま、反撃開始から命中までは停止延長により2動画F後ろへ移動（命中4.300000秒）。着地はキック停止の延長も含み4動画F後ろ（4.866667秒）。終了は撃破から実時間0.85秒の既存待ち条件なので、全尺は前版から2動画F長くなり、着地後の待機が少し短くなる。

カメラの距離とパリィ/電撃/着地の連続画像、画面全幅の床、収録後の一時背景破棄を確認。最終Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-mid-camera-hitstop-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-mid-camera-hitstop-20261024.json) / [連続画像](previews/mid-camera-hitstop-20261024-contact.png) / [検証JSON](previews/mid-camera-hitstop-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision mid-camera-hitstop-20261024 -Variants 2stage-hybrid`。全体の最終受入は別に残し、#69/#71/#72/#76はOpen、#73はClosedを維持する。
