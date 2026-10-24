# キック細部修正とカメラ距離の比較（2026-10-24）

提出された人物と独立電撃の細部修正版を適用した。同じ更新素材・同じ動き・同じ時間配分で、従来の撮影距離とゲーム画面に近い距離の2本を収録する。

## 受領と接続

| 原稿 | 版 | SHA256 |
| --- | ---: | --- |
| Player_counter_kick.aseprite | 5 | 170a5732e08d1cd3fe7626451c0ed85e7ebbdc07ca6803230466fcebdac4d4c5 |
| Player_counter_kick_effect.aseprite | 2 | e1b72bf36cca4c8decfb18725e2db3b367f336be5fee481ca1af74d57fc6dfd8 |

Current/Historyへ原本を登録し、前版を保持。人物128px・電撃256px、各15コマと原本時間を維持する。全Body参照が横89px・縦43pxの差で一致し、独立電撃Pivot(121,123)/32PPUを再利用した。人物のHair/Body/Eyeと別原稿のEffect/Effect2を出力し、旧Effectや確認用Body/Eyeを二重表示しない。

Unity出力では人物9/10/11/13/14/15枚目、電撃3/5/6/7/8枚目が更新された。他のPNG・目マスク・Clipの時間・接点は保持。動画用と本編の専用Snapshotで確認し、検証後に両原稿のvideo/prototype使用版を更新した。

## カメラ比較

| テイク | 通常ortho size | カウンターortho size | 1080pで原画1px |
| --- | ---: | ---: | --- |
| 従来の撮影距離 | 2.8125 | 2.4107144 | 通常6px / アップ7px |
| ゲーム画面より少し近い比較版 | 4.21875 | 3.375 | 通常4px / アップ5px |

参照した現在の本編Sceneは通常5.625（原画3px）。比較版はそれより人物が約1.33倍大きく、従来の撮影版より引いた距離。カウンターも7pxから5pxへ引き、通常からのアップは残す。既定の撮影Scene/Studioや本編カメラは、この比較撮影だけで変更しない。電撃の外縁すべてを収める自動調整は行わない。

引くと見えてしまう有限の背景/床の端を、収録中の一時描画で補完した。元画像の64pxグリッドを同じ間隔で繰り返し、床は同じ画像の単色を接地位置から下へ延長する。背景全体の拡大によってグリッド間隔を変えず、PNG/Sceneを変更しない。収録後に追加描画と生成Spriteを破棄する。

`VideoCaptureUtility.CaptureAtDistance(normalSize,closeSize)` を追加。距離を収録中だけ適用し、終了時には元の距離・ズームへ戻す。イベントJSONにも通常/カウンターの距離を記録する。

## 確認と成果物

Unity compile Error/Warning0、関連EditMode **3/3**、PlayMode **10/10**。位置・足先の接触・30/60/120fps×左右・Pause/8F停止/Reset・本編の同期を限定確認。全件テストと従来80回Resetは再実行していない。

両テイクとも **308F / 5.133333秒・1920×1080・60fps・無音H.264**。合計616Fをデコードし、全イベント/対象/tick/実時間が前版と一致。スロー0.900秒、命中8F停止、蹴り後の動作を維持する。画面下端の全幅が従来と同じ床色で埋まること、グリッド/床の外枠が映らないこと、接触/電撃の見え方を確認。比較収録後の距離・ズーム復元と一時背景の破棄も確認した。最終Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

- [従来距離のMP4](previews/combat-2stage-hybrid-counter-revision-close-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-counter-revision-close-20261024.json)
- [ゲームに近い比較MP4](previews/combat-2stage-hybrid-counter-revision-game-near-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-counter-revision-game-near-20261024.json)
- [同時刻の比較画像](previews/counter-revision-camera-comparison-20261024.png) / [検証JSON](previews/counter-revision-20261024-validation.json)

再現はCombatVideoのPlayModeで、先に `CapturePreview(2,true)` → `encode-captures.ps1 -Revision counter-revision-close-20261024 -Variants 2stage-hybrid`、次に `CaptureAtDistance(4.21875f,3.375f)` → `encode-captures.ps1 -Revision counter-revision-game-near-20261024 -Variants 2stage-hybrid`。生PNGの収録先は共通なので、次の収録前にそれぞれ書き出す。保存CRF16、会話内の2本は同解像度/fps/全コマのCRF22。

今回の修正版反映と比較動画は完了。採用カメラ・動画全体の最終受入は別に残し、#69/#72/#74/#76はOpenを維持する。
