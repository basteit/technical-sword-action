# 強い接点光とパリィ専用カメラ（#33）

## 目的と受入条件

短くした停止の中でも、予備動作から接点へ視線を集め、強い一度の光と短い画面揺れで弾いた重みを伝える。主人公は敵より前に表示し、重なっても姿勢を読めるようにする。

1. 停止30F（0.50秒）・背景減光40%・新しいオレンジ光の強化を適用し、受付・8Fピーク同期・判定・原稿を維持する。
2. 成功後の弾き予備動作から接点へ寄り、インパクトでは最大まで寄って0.10秒だけ強く揺れる。終了後0.20秒保持して0.18秒で通常カメラへ戻る。
3. 主人公本体/髪を敵より前へ出す。Pause、演出0%、被弾・対象消失・Reset・Disable・Scene変更、反撃への接続でカメラや表示順を壊さない。

予備動作は成功後に追加した8Fの弾き準備を指す仮解釈とし、空振り/失敗パリィではカメラを動かさない。成功の演出を強くしても成功条件や失敗理由は変えない。最新ユーザー依頼による複数軸は、時間/光・カメラ・表示順を区切って検証する。

## 仮置き値

- パリィ停止36→30F、背景30→40%。新しい接点光の色は保持し、アルファ0.45→0.8、RGBゲイン3倍、広がり設定1.6unit、0.18秒の単峰減衰。旧白ハロー0%・白補助光8%は維持する。
- カメラは通常Orthographic Size5.625、予備4.75、インパクト4.25。予備の移行0.10秒、弾き終了後の保持0.20秒、戻り0.18秒。接点に固定した注視点を使い、保持中に主人公が動いても注視点を引っ張らない。
- インパクトの画面揺れは0.10秒・最大0.32unitの減衰した反動。キャラクターの最大2仮想pxの逆位相振動は維持し、停止時間だけに適用する。
- カメラの書き込みは既存の共有Feedbackへ統一する。反撃へ進む場合はカメラ基準位置を保って所有を渡し、反撃終了後に正しい通常画角へ戻す。
- 主人公の実表示Sorting Orderを最低20、敵の現行表示（8以下）より前へ置く。受領画像のレイヤーやPNGは変更しない。VFX・UIの専用前面レイヤーは維持する。

## 検証と動画

実装 `7f7405e`、動画/収録補助 `6be7e0416f9fcec5bf7f735bb3c229ec6b18d8c7`。Compile Error0/Warning0、全EditMode293/293、パリィ・反撃149/149、主人公素材表示7/7。全PlayMode817件は804合格・13失敗で、失敗名は直前版と一致、新規失敗0件。既存の動画蹴り高さ2件・タメ時刻3件・4段防御キャンセル7件・旧Just境界期待1件は未解決のまま保持し、全テスト合格とは扱わない。

全体は204.881秒でCLIの180秒上限を超えたが、Unityの最終XMLを回収して比較した。重複実行なし。最後は保存済み `PrototypeCombatValidation`、dirty=false、再生/Pauseなし、主人公Sorting Order20、MissingScript0、専用/共通Validator0、Console Error0/Warning0。

- [等倍3秒・ゲーム内カメラそのまま](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6be7e0416f9fcec5bf7f735bb3c229ec6b18d8c7/docs/video/previews/parry-adaptive-20261102-camera-on.mp4)
- [直前/今回の比較（上が直前、下が今回）](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6be7e0416f9fcec5bf7f735bb3c229ec6b18d8c7/docs/video/previews/parry-adaptive-20261102-camera-comparison.mp4)
- [1/2速度・観察用](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6be7e0416f9fcec5bf7f735bb3c229ec6b18d8c7/docs/video/previews/parry-adaptive-20261102-camera-detail.mp4)／[GIF](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@6be7e0416f9fcec5bf7f735bb3c229ec6b18d8c7/docs/video/previews/parry-adaptive-20261102-camera-detail.gif)

実AIのF1ザコ、180枚/60fps/3秒。成功17→インパクト25→やられ63枚目。カメラは準備17→最大寄り25→終了後保持63→戻り開始75→通常画角への復帰86枚目。最大寄り4.25、停止30F、背景40%、画面揺れの実測最大0.3263unit。振動30サンプル/左右交替14回、両rootの停止中移動0、主人公/敵のSorting Order20/8、終了時のVFX・オフセット・カメラ残留0を確認。

ゲーム内カメラの全画角を記録し、編集側のズーム/切り抜き/歪み合成は行っていない。HUDの収録ではなくカメラの実描画。等倍/観察用1280×720、比較640×720（各全画角640×360）、H.264 Baseline・Level3.1・yuv420p・faststart・30fps・無音。全4ファイルをデコードし、等倍MP4のHTTP200/video/mp4・Range206・ダウンロードSHA256一致を確認。実物スマホ上の再生受入は未実施。

再現は保存済み `PrototypeCombatValidation` でPlayし、ULoopの別リクエストで `AdaptiveParryCaptureUtility.Prepare()` → `Capture(false)`。既定収録先は `.uloop/outputs/video/adaptive-parry-20261102-camera-v1/adaptive/`、既存出力は上書きしない。`Tools/Video/encode-adaptive-parry-review.ps1` はcamera-v1と直前orange-v1を比較する。過去版の再現は各動画コミット時点の補助/設定を使う。[検証記録](validation/prototype-parry-camera-impact-2026-11-02.json)。

## 調整と戻し方

`CounterCinematicProfile` の `parryCamera*`、`parryImpactShake*`、`parryGlow*`、`parryTrialHitstopFrames`、`parryBackgroundDim` を調整する。`parryCameraEnabled=false` は接点への寄り/保持/戻りだけを切る。画面揺れ0%ではカメラの寄り・画面揺れ・キャラクター振動をすべて止めるが、判定・停止時間を変えない。

直前の厳密な比較基準は `efc0c23` とorange-v1の元収録。現行設定から停止36F・背景0.3・光アルファ0.45/ゲイン1/半径1.2・カメラ寄りOFFへ戻しても比較できる。主人公前面表示は独立した `PrototypePlayerArtSet.foregroundSortingOrder` であり、カメラのON/OFFで戻さない。実行中の設定変更前に遭遇をリセットする。既存未コミット変更を破棄するcheckoutは行わない。

操作感の最終採否、複数敵や反撃連携での視認性、キーボード/実物パッド各15分、最低環境Build性能は人間による確認が必要。今回は新ZIPを作成せず、Issue #33全体を完了扱いにしない。
