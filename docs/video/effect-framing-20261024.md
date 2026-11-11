# 電撃をもう少し見せる撮影距離（2026-10-24）

「もうちょいエフェクト見せたい」に合わせ、カウンターを前版の原画8倍から7倍へ引いた。人物は前版の87.5%の大きさ、視野は約14%広がる。通常時も7倍から6倍にして、アップの関係を保つ。電撃の外縁すべてを収める自動調整は行わず、人物と敵が読める範囲で大きい電流の枝と軌跡を見せる。

- 通常ortho：2.4107144 → **2.8125**。カウンター：2.109375 → **2.4107144**。
- 主動画・攻防切替・Studioと5プリセット・生成初期値を統一。Studioの「標準」は6倍、「寄り」は7倍。本編のカメラは保持する。
- 素材、配置、フォーカス位置、スロー0.900秒、命中8F停止、揺れ、動画の時間配分は保持する。

Unity compile Error/Warning0。既存の関連PlayMode **12/12**（30/60/120fps×左右、主2段の人物フレームとStudio反撃/復元）を限定実行。全件・従来80回Resetは再実行していない。

**308F / 5.133333秒・1920×1080・60fps・無音**で再収録。全308Fをデコードし、前版と全イベント/対象/tick/実時間が一致。通常・タメ・大きい電撃・命中の連続画像を確認した。収録後Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-effect-framing-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-effect-framing-20261024.json) / [連続画像](previews/effect-framing-20261024-contact.png) / [検証JSON](previews/effect-framing-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision effect-framing-20261024 -Variants 2stage-hybrid`。

動画全体の最終受入は別に残す。#69/#76はOpen、#73はClosedを維持する。
