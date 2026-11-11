# パリィから反撃への連続カメラ（#33）

## 目的と受入条件

反撃を入力したパリィから通常画角を経由せずに反撃の構図へ移り、敵の予備動作から決着まで一続きに読める画面にする。

1. 反撃入力が予約済みで開始可能なら、停止解除で通常画角へ戻さず、現在の画角から反撃の注視点/Size3.75へ直接補間する。遅い入力でも既に戻った位置から連続して引き継ぎ、接点へスナップし直さない。反撃入力なしなら停止解除から通常復帰する。
2. 元の通常カメラ基準、左右・30/60/120fps・強度0/50/100%、Pause・中断時の復帰を保つ。受付・反撃開始/命中時刻・威力・敵の96F予兆は変えず、旧カメラは独立設定で比較可能にする。
3. 敵予備動作の開始から全96Fを省略せずに収録し、同じAI/入力で旧/新カメラを比較する。実カメラの全画角で、編集側の寄りや切り抜きを使わない。

成功時は接点から反撃へ視線が流れ、失敗/空振りでは寄らない。今回の調整軸はカメラ接続と収録開始位置の2点。操作感と眩しさの最終採否、実物入力15分、最低環境Build性能は人間による確認を残す。

## 実装と戻し方

`parryCounterCameraBridge=true` で、開始条件を満たす反撃予約中は通常復帰を止める。弾き表示の終了から反撃が実際に始まる次tickまで予約を保持し、1フレームだけ復帰する隙間も作らない。反撃開始時の画角を引き継ぎ、0.20秒のSmoothStepで既存の反撃注視点/Size3.75へ移る。遅い入力はその時点の画角で復帰を止め、接点へ戻し直さない。強度50%でも補正を二重に掛けない。書き込みは共有Feedbackのみ。

`parryCounterCameraBridge=false` で直前のカメラ接続へ戻せる。反撃入力なしの停止解除時復帰、弧/線形、追加火花、停止時間とは独立。Pause、強度0%、対象消失、死亡、Disable、Reset、Scene変更で通常画角と所有を復帰する。新しい構図へ移る時間だけを設定アセットで調整する。

## 動画・計測

- [予備動作からの等倍6秒](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@2b17019da2ac118d25b4a2ca4c8855229eb02b1e/docs/video/previews/parry-counter-camera-link-20261103-on.mp4)
- [同一入力の比較・上が旧接続/下が直接接続](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@2b17019da2ac118d25b4a2ca4c8855229eb02b1e/docs/video/previews/parry-counter-camera-link-20261103-comparison.mp4)
- [全体1/2速度・12秒](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@2b17019da2ac118d25b4a2ca4c8855229eb02b1e/docs/video/previews/parry-counter-camera-link-20261103-detail.mp4)

双方360枚/60fps、F1実AI。予備開始1枚目から96サンプルを省略せず収録。成功97→インパクト105→停止解除135→弾き終了143→反撃144→背後移動154→タメ155→命中210→吹っ飛び終了244→反撃終了250→通常画角255枚目が一致。Attackは106枚目に1回だけ予約する。

停止解除からタメ前までの最大Sizeは旧5.6186→新4.25。新カメラは144枚目4.2402→146枚目4.1719→150枚目3.9381→155枚目3.75と連続して寄り、途中で広がらない。遅い入力の接続も左右/強度差のテストで確認した。

HP・状態・敵Sprite・座標など28列、成功以降の主人公Sprite、命中後の吹っ飛び距離の比較差分0。吹っ飛び距離は非発動中に前回値が残る診断値なので命中後だけ比較した。主人公HP100、敵HP30→0、反撃90ダメージ/1Hit、吹っ飛び1.6unitを維持。両者の停止中root移動0、終了時のカメラ・時間倍率・VFX・表示ずれ残留0。

動画は実ゲームカメラの全画角をリサイズした描画で、HUD収録や編集側のズーム/切り抜き/VFX合成ではない。H.264 Constrained Baseline/Level3.1/yuv420p/faststart/30fps/無音。3ファイルを完全デコードし、全体と比較のコンタクトシートを目視した。配信HTTP200/video/mp4、Range206、ダウンロードSHA256一致を確認。実物スマホでの再生は未確認。

## 検証・再現

実装 `f73b79d`、終了処理の補強 `ae7e172`、動画/補助 `2b17019da2ac118d25b4a2ca4c8855229eb02b1e`。Compile Error0/Warning0、全EditMode306/306、関連PlayMode174/174。追加した接続時間10秒の終了ケースも1/1合格で、行動終了前に補間が終わらなくてもカメラを解放する。標準0.20秒の納品動画の動きは変わらない。初回の追加テストで1tickの引継ぎの隙間を検出し、予約保持で修正してから再確認した。

最終の全PlayMode843件は830合格・13失敗、うち関連175件はすべて合格。失敗名とメッセージは直前版と一致し、新規失敗0。既存の動画蹴り高さ2件・タメ時刻3件・4段防御キャンセル7件・旧Just境界期待1件は未解決。全体280.280秒でCLIの180秒上限を超えたため、重複実行せずUnity最終XMLを回収した。最終Editorは保存済みPrototypeCombatValidation、dirty=false、再生/Pauseなし、MissingScript0、専用/共通Validator0、Console Error0/Warning0。[検証記録](validation/prototype-parry-counter-camera-link-2026-11-03.json)。

保存済み `PrototypeCombatValidation` でPlayし、別ULoopリクエストでPrepare→ `AdaptiveParryCaptureUtility.CapturePreviousCounterSequence()`、再度Prepare→ `CaptureCounterSequence()`。既定出力は `.uloop/outputs/video/adaptive-parry-20261103-camera-link-v1/` のcounter-before/counter。既存出力へ上書きしない。エンコードは `Tools/Video/encode-parry-counter-camera-link.ps1`。通常ザコのAI/96F予兆・合図・入力受付・威力・反撃の時刻・受領原稿・完成撮影Sceneは変更していない。

人間による確認は、直接接続の操作感、遅い入力と複数敵/壁際/空中の見やすさ、連続成功の眩しさ/酔いやすさ、実物入力各15分、最低環境Build性能。新ZIPは作成せず、#33全体を完了扱いにしない。
