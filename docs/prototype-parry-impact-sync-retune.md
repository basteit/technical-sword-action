# パリィの暗さ・間隔・ゲーム画角の再調整（#33）

## 目的と受入条件

敵の攻撃と弾きが離れて見える待ちを詰め、前回より明暗差を少し強くしつつ、停止を少し短くして手応えと流れを両立する。対象は本編プロトタイプの軽パリィであり、完成動画や原稿を変更しない。

1. 背景減光30%・通常/Just停止36Fを適用し、Flash 0/50/100%でも判定と時刻を変えない。
2. 現行汎用ザコは成功から8Fで口閉じコマ・接触姿勢・火花・停止・歪みを同期し、弾き後8Fでやられ表示へ移る。受付、Just境界、威力、ストック数は維持する。
3. プレビューは本編カメラの16:9全体を記録し、拡大切り抜きを行わない。中断・Pause・リセットと反撃予約に新しい残留がない。

成功時は敵の振り抜きへ素早く弾きが重なり、失敗時は従来の予兆と入力受付で理由を判断できるようにする。今回の複数の明示依頼は、明暗/停止と表示同期を分けて検証する。通常攻撃・反撃カメラ・カウンターの停止時間は変更しない。

## 仮置き値

| 項目 | 直前版 | 今回 |
| --- | --- | --- |
| 背景減光の上限 | 20% | 30% |
| パリィ停止 | 40F / 約0.667秒 | 36F / 0.60秒 |
| 現行ザコの成功→表示ピーク | 15F / 0.25秒 | 8F / 約0.133秒 |
| 弾き後の表示 | 8F | 維持 |
| 歪み | 0.4秒 / 32px | 維持 |
| プレビュー | 800×540切り抜き | 1920×1080の全画角から縮小のみ |

`CounterCinematicProfile.parryApproachMaxFrames` を8へ設定。元の敵ピークまでの残り時間を計算した後、その表示区間だけを上限8Fへ圧縮する。成功前のAI・攻撃予兆・口閉じピークの元位置は移動しない。ピーク後は通常の表示速度へ戻し、主人公の弾き8Fが終わってからやられ表示へ移る。

元の口閉じがすでに近い敵は8Fまで引き延ばさない。未登録の仮敵は従来どおり即接触表示へフォールバックする。現行ザコの反撃受付180tickは成功から数え、準備8F＋弾き8Fが進むため、やられ表示開始時は約164tick残る。停止36Fは戦闘時計を進めない。

## 戻し方

- 直前の同期版：`parryTrialHitstopFrames=40`、`parryBackgroundDim=0.2`、`parryApproachMaxFrames=15` にして遭遇をリセットする。
- 同期導入前：`synchronizedParryImpact=false` にして遭遇をリセットする。即時表示、通常9F/Just10F、背景99%・4Fへ戻る。
- 設定は `CounterLaunch_ReferenceTrial.asset` と `CounterCinematic.asset`。無リセットの実行中切替は推奨しない。原稿・完成動画・旧プレビューは保持する。

## 検証・動画

実装 `3e17d7b`、収録・動画 `89c2b910a509176ac223de4b00c95104b0f6b0b7`。CompileはError0/Warning0、全EditMode278/278、パリィ・反撃PlayMode108/108。全PlayMode775件は762合格・13失敗で、全失敗名が10月26日の既存記録と一致し、新規失敗は0件。内訳は動画の蹴り高さ2件・タメ時刻3件、4段防御キャンセル動画7件、旧Just境界期待1件。未解決のまま保持し、全テスト合格とは扱わない。

全体実行は191.912秒かかり、CLIの180秒応答上限を超えたが、Unityが保存した最終XMLを回収して照合した。二重実行はしていない。最終Sceneは元の保存済み `PrototypeCombatValidation`、dirty=false・再生/Pauseなし・MissingScript0・専用/共通Validator0・Console Error0/Warning0。人間による操作感の受入は未完了。

- [等倍3秒・実ゲーム全画角](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@89c2b910a509176ac223de4b00c95104b0f6b0b7/docs/video/previews/parry-adaptive-20261102-v2-on.mp4)
- [直前／今回の比較（上が直前、下が今回）](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@89c2b910a509176ac223de4b00c95104b0f6b0b7/docs/video/previews/parry-adaptive-20261102-v2-comparison.mp4)
- [1/2速度の観察用](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@89c2b910a509176ac223de4b00c95104b0f6b0b7/docs/video/previews/parry-adaptive-20261102-v2-detail.mp4)／[GIF](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@89c2b910a509176ac223de4b00c95104b0f6b0b7/docs/video/previews/parry-adaptive-20261102-v2-detail.gif)

全MP4はH.264 Baseline・Level3.1・yuv420p・faststart・30fps・無音。等倍と観察用は1280×720、比較は各640×360の全画角を上下へ配置した640×720。4ファイル全体をデコードし、配信MP4はHTTP200/video/mp4、Range206、ダウンロードSHA256一致を確認した。実物スマホ上の受入は未実施。

同じF1通常ザコの実AIを180枚・60fpsで記録。成功17枚目→口閉じ/インパクト25枚目→やられ69枚目、HPは主人公100/敵30、成功1回・ダメージ0・反撃なし。カメラは通常Orthographic Size5.625、16:9、基準位置(0,3,-10)を使用し、録画用のズームは加えない。前回は同じ全画角の元画像を動画化の際に切り抜いていたため、今回は動画化でも全体を保持した。HUD自体の収録ではなく、ゲームカメラの実描画である。

再収録は保存済み `PrototypeCombatValidation` をPlayし、別リクエストで `AdaptiveParryCaptureUtility.Prepare()` → `Capture(false)`。既定出力は `.uloop/outputs/video/adaptive-parry-20261102-sync-v2/adaptive/`。既存収録を上書きしない。エンコードは `Tools/Video/encode-adaptive-parry-review.ps1`、比較元はv1のadaptive連番。新規収録は未使用のRevision/OutputTagを指定する。検証記録は[今回のJSON](validation/prototype-parry-impact-sync-retune-2026-11-02.json)。

人間による確認：ゲーム画角で暗転30%と停止0.60秒が自然か、8Fの仮振りかぶりで口閉じへ弾きが合うか、連続成功と複数敵で他の予兆を隠さないか。キーボード・パッド各15分とビルド性能の確認は別途必要。
