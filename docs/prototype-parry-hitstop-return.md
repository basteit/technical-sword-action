# 停止解除から戻るパリィカメラと反撃通し（#33）

最新は[反撃入力時の直接カメラ接続と予備動作全体の収録](prototype-parry-counter-camera-link.md)。本書の停止解除時復帰は反撃入力なしの場合に維持し、予約反撃時の往復は最新方式へ置き換える。

## 体験目的と受入条件

パリィのインパクトは停止中だけ強く見せ、動きが再開したらカメラも戻し始め、攻撃入力による背後回り反撃へ自然につなぐ。

1. 停止中は接点と最大寄りを保ち、ヒットストップが解除された最初の表示更新から0.18秒の復帰を進める。弾き後8Fや追加保持0.20秒を待たない。
2. 左右・30/60/120fps、Pause、設定0%、中断と反撃へのカメラ所有引継ぎで、通常カメラの基準と戦闘タイミングを壊さない。直前の遅延復帰は比較設定として保持する。
3. 実F1敵AIの攻撃→パリィ成功→停止解除/カメラ復帰→Attack→背後移動→蹴り/吹っ飛び→通常画角までを、ゲーム内カメラで収録する。入力不要の自動反撃へは変更しない。

成功時は停止解除からすぐに画面が動き、入力した反撃へ接続する。失敗/空振りで寄らないこと、受付・威力・敵よろめき・判定は変更しない。調整軸はカメラの復帰開始時点のみとし、直前の弧・ズーム・光・火花・停止30Fは維持する。

## 実装・設定

`CounterCinematicProfile.parryCameraReturnOnHitstopEnd=true` を本編用/比較用の両アセットへ設定した。停止中は接点/最大Size4.25を維持し、停止解除を最初に観測した表示更新から既存の0.18秒・終端減速カーブで復帰する。弾き表示8Fや終了後保持0.20秒とは独立して進める。反撃が始まれば既存の共有Feedbackへ所有を移し、通常画角の基準を作り直さない。

`parryCameraReturnOnHitstopEnd=false` で直前の「弾き終了→0.20秒保持→復帰」へ戻せる。弧/線形の選択や火花設定とは独立し、両カメラ方式で旧保持を確認した。判定、受付、ダメージ、よろめき時間、停止30F、光・火花、反撃設定、受領原稿と完成撮影Sceneは変更していない。

## 動画と計測

- [反撃までの等倍5秒](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-counter.mp4)
- [反撃通しの1/2速度](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-counter-detail.mp4)
- [復帰開始の比較・上が直前/下が今回](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-comparison.mp4)
- [パリィのみ等倍](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-on.mp4)／[1/2速度](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-detail.mp4)／[GIF](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6a7f12c71d07bbfaf337066aac1c3ec2e466f585/docs/video/previews/parry-adaptive-20261103-hitstop-return-detail.gif)

F1実AIを使い、パリィのみ180枚/3秒、反撃通し300枚/5秒を60fpsで収録。成功17→インパクト25→停止解除/復帰開始55→弾き終了/敵やられ63は共通。パリィのみでは65枚目に通常画角へ戻る。直前は復帰開始75/通常復帰86だったため、開始が20F早まった。成功以降のSprite・HP・Hitstop・VFX・root・表示振動など19列の差分は0で、カメラだけが変わる。

反撃通しでは64枚目のAttackで反撃開始、背後移動74→タメ75→解放129→命中130→吹っ飛び終了164→反撃終了170→通常画角175。敵HP30→0、反撃ダメージ90、1Hit、主人公HP100。撃破演出を含む命中停止は実測16F、吹っ飛び1.6unitであり、今回その設定は変更していない。反撃開始時にカメラ所有を引き継ぎ、終了時の時間倍率・VFX・表示ずれ・カメラ残留0を確認。

編集でのズーム/切り抜き/後付けVFXはなく、ゲームカメラの全画角をリサイズした。HUDではなく実カメラの描画。MP4はH.264 Constrained Baseline/Level3.1/yuv420p/faststart/30fps/無音。全6ファイルを最後までデコードし、反撃通し/比較のコンタクトシートを目視した。公開MP4のHTTP200・video/mp4、Range206、ダウンロードSHA256一致を確認した。実物スマホでの再生は未確認。

## 検証と再現

実装 `80b9d4f`、動画/収録補助 `6a7f12c71d07bbfaf337066aac1c3ec2e466f585`。Compile Error0/Warning0、全EditMode304/304、パリィ・反撃PlayMode159/159。全PlayMode827件は814合格・13失敗で、失敗名とメッセージは直前版の13件と一致、新規失敗0。既存の動画蹴り高さ2件・タメ時刻3件・4段防御キャンセル7件・旧Just境界期待1件は未解決として保持する。

全体213.994秒でCLI180秒上限を超えたため、重複実行せずUnity最終XMLを回収して比較。全体内の関連159件も合格。最終Editorは保存済み `PrototypeCombatValidation`、dirty=false、再生/Pauseなし、MissingScript0、専用/共通Validator0、Console Error0/Warning0。[検証記録](validation/prototype-parry-hitstop-return-2026-11-03.json)。

保存済み `PrototypeCombatValidation` でPlayし、別ULoopリクエストで `AdaptiveParryCaptureUtility.Prepare()` → `Capture()` または `CaptureCounterSequence()`。既定は `.uloop/outputs/video/adaptive-parry-20261103-hitstop-return-v1/` のadaptive/counter。既存出力は上書きしない。再収録時は新しいrevisionを使う。エンコードは `Tools/Video/encode-adaptive-parry-review.ps1 -IncludeCounter`。Editorの背景実行がOFFのためフレームが止まった場合は、ULoopの `focus-window` で前面へ戻してから入力ガードを解除する。Project設定の背景実行は変更しない。

## 人間による確認

復帰と反撃の再ズームのつながり、連続成功の眩しさ/酔いやすさ、複数敵・壁際・空中、実物パッド/キーボード各15分、最低環境Build性能は人間による確認を残す。今回は新ZIPを作成せず、Issue #33や最終の操作感を完了扱いにしない。
