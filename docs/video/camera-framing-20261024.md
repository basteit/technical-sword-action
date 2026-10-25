# 撮影カメラを少し引く（2026-10-24）

カウンターを少し離し、通常撮影もそれに合わせるユーザー指示を反映した。人物と敵の動き・接触を優先し、256pxの電撃の外縁すべてが画面内に入るような自動ズームは行わない。

| 場面 | 前版のortho size | 更新値 | 1080pでの原画1px |
| --- | ---: | ---: | ---: |
| 通常 | 2.109375 | 2.4107144 | 8px → 7px |
| カウンターのアップ | 1.6875 | 2.109375 | 10px → 8px |

カウンターの人物は前版の80%、通常時は87.5%の大きさで表示する。アップ時の倍率差を少し抑え、人物の全身・髪と敵との距離に余裕を持たせた。背景・敵配置・フォーカス位置・揺れ・スロー・復帰速度は保持する。

主動画、攻防切替の比較Scene、自由撮影Studioの初期値・5プリセット・Scene生成側を揃えた。Studioの距離選択に「標準（7倍）」を追加し、距離を変更した際も通常/アップの比率を7:8とする。本編のカメラや反撃Profileは変更しない。

## 確認

Unity compile Error/Warning0。既存PlayModeの関連 **12/12**（30/60/120fps×左右で、主2段の背後接近とカウンターの人物フレーム、Studioの実パリィ→同じ敵への反撃と時間/ズーム/VFXの復元）を確認。カメラ調整に絞り、全件テストと従来80回Resetは再実行していない。

主動画は **308F / 5.133333秒・1920×1080・60fps・無音**。全コマをデコードし、前版と全イベント・対象・tick・実時間が一致することを確認。タメ0.900秒、命中8F停止、蹴り後の動作、前回受領した後ろ髪と独立電撃を保持。通常時・タメ・接触・着地の連続画像を目視した。収録後Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰した。

[MP4](previews/combat-2stage-hybrid-camera-framing-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-camera-framing-20261024.json) / [連続画像](previews/camera-framing-20261024-contact.png) / [検証JSON](previews/camera-framing-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision camera-framing-20261024 -Variants 2stage-hybrid`。

今回のカメラ調整と、動画全体の最終受入は区別する。#69/#76はOpen、時間制御/カメラ基盤の#73はClosedを維持する。
