# 後方2倍・上昇1.5倍のノックバック（2026-10-24）

最新指示に合わせ、両敵の撃破時ノックバックを前版から後方2倍・上昇1.5倍にした。

| 対象 | 後方距離・前→新 | 上昇量・前→新 |
| --- | ---: | ---: |
| 1体目・2段目撃破 | 0.40→0.80unit | 0.12→0.18unit |
| 2体目・カウンター | 0.80→1.60unit | 0.18→0.27unit |

移動時間18戦闘F＝0.30秒は保持するため、飛ぶ勢いも増す。downコマ/向きの固定、着地後の停止→消滅、パリィ後の同一コマ固定、通常の非致死ヒットバック、壁判定を保持。4段比較も撃破距離を2倍（4段目1.60unit）にし、Directorの反撃は1体目の2倍を保つ。自由操作Studioにも適用。本編の敵/カメラ/4段攻撃、受領原稿は変更しない。

Compile Error/Warning0、関連PlayMode **12/12**（左右のPause/固定ポーズ、4段比較、Studio30/60/120fps、左右各10回の通し/Reset）。全件や従来80回の組み合わせは再実行していない。実進行の座標でA距離0.80/上昇0.18、B距離1.60/上昇0.27を実測し、前版から2倍/1.5倍であること、停止後/フェード中に動かないことを確認した。

MP4は **310F / 5.166667秒・1920×1080・60fps・無音**。全310Fデコード。前版と全レイアウト/イベント種類/対象/戦闘tick/実時刻が一致。通常/タメ/命中カメラ、揺れ、4F/Just5F・キック10F停止、0.900秒タメを保持。連続画像で両敵の軌道・停止・構図を確認。最終Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-enemy-knockback-boost-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-enemy-knockback-boost-20261024.json) / [連続画像](previews/enemy-knockback-boost-20261024-contact.png) / [検証JSON](previews/enemy-knockback-boost-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision enemy-knockback-boost-20261024 -Variants 2stage-hybrid`。今回の調整確認と動画全体の最終受入は区別し、#69/#72/#76はOpen、#75はClosedを維持する。[軌道/固定コマの仕組み](enemy-knockback-arc-20261024.md)。
