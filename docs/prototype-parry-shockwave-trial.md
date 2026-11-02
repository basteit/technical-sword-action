# パリィ衝撃波の画面屈折試作（#33）

## 体験目的と範囲

実際のパリィ成功点から短い屈折の輪を広げ、「弾いた瞬間」を強調する。判定・受付・威力・敵AI・Hitstop・反撃・カメラの位置や画角は変更しない。

ユーザーの2026-11-02の依頼に基づく比較試作であり、最終採用強度の人間による受入は未実施。既存の受領原稿、Scene、Prefab、Clip、完成動画は変更していない。

既存の未コミット差分を保持し、作業場所は同じフォルダの `issue/33-parry-shockwave`。基点 `5816d9c109a6d0493165154c7e57252ac35a3e38`、未統合PR #81に依存する。[Draft PR #98](https://github.com/basteit/technical-sword-action/pull/98) は#33全体を閉じず、今回の演出追加だけを扱う。

## 試用値と戻し方

- 基準強度: `0.65`。持続は実時間 `0.24秒`。
- 命中点の中心を残し、周囲の円環だけを押し引きする。新しいFlash、反転、色収差、カメラ移動、ブラーは追加しない。
- URPの画面色をPointサンプリングし、ドットのにじみを増やさない。最終画面に屈折をかけるため、輪が通る部分のキャラクターや地面の輪郭も短く歪む。
- プロトタイプは `Assets/Settings/PrototypeCombat/FeedbackSettings.asset` の `parryShockwave=0` で追加効果だけを止められる。`vfx=0` でも停止する。
- 撮影側は `VideoCombatPresentation.parryShockwaveStrength=0` で止める。純ドット比較（HybridGlow=false）では追加効果を出さない。
- 無効な値・カメラなしでは開始しない。同じカメラでは最新の1波へ置換し、複数パリィで変位を加算しない。

## 接続と終了

- プロトタイプ: `CombatFeedback2D` の通常/Just成功通知。受領作画を使う成功表示も、既存の接触座標を使用する。
- 撮影: `VideoCombatPresentation.PresentLightParry` の実際の表示用接触イベント。
- `ParryShockwavePulse` は非スケールの演出時間で進み、Hitstop中も波が進む。Pauseでは停止する。Pause中でも強度0への変更は即座に解除する。
- Reset、Disable、Scene変更、プレイヤー被弾ではプロトタイプの所有する波を解除する。終了後は描画パスを投入しない。
- カメラごとのMaterialを使用し、保存Material・グローバルShader値を変更しない。破棄済みカメラのMaterialは次回描画時に回収する。

## 比較動画

- [接触の1/10速度・追加あり拡大](video/previews/parry-impact-slow-on-20261102.mp4): 720×486、30fps、150枚、5秒、無音、116004bytes。接触付近のみを1/10速度とし、歪みが見える元49枚目を途中で1.5秒保持する。
- [接触の1/10速度・上下比較](video/previews/parry-impact-slow-comparison-20261102.mp4): 720×972、30fps、150枚、5秒、無音、200536bytes。上OFF/下ON、同じ元フレームと停止区間。[GIF版](video/previews/parry-impact-slow-20261102.gif)も保存。
- [スマホ用軽量MP4](video/previews/parry-shockwave-mobile-20261102.mp4): 480×648、30fps、189枚、6.3秒、無音、636194bytes。H.264 Constrained Baseline / Level 3.1 / yuv420p / faststart。上下比較の内容と時間は保持する。
- [通常速度・追加あり](video/previews/parry-shockwave-on-20261102.mp4): 1920×1080、60fps、210枚、3.5秒、無音。
- [上下比較](video/previews/parry-shockwave-comparison-20261102.mp4): 960×1296、60fps、378枚、6.3秒、無音。上=OFF、下=ON。最初3.5秒は1倍、後半2.8秒は接触付近を1/4速度にしている。
- [拡大スローGIF](video/previews/parry-shockwave-close-20261102.gif): 800×540、20fps、56枚。
- [同フレーム静止比較](video/previews/parry-shockwave-comparison-20261102.png)。

全てUnityの実描画から取得し、後付けの動画歪み加工ではない。操作版の実AIによる成功パリィ1回と、90ダメージの反撃1回を収録。実物パッドの操作感やDevelopment Buildの性能確認を代替しない。

1/10速度版は既存の元連番40〜60枚目を6fpsとして読み、30fpsへコマを反復した観察用編集である。フレーム補間や新しい動きを合成せず、途中の `DETAIL HOLD` 表示中だけ元49枚目を追加で1.5秒保持する。ゲーム内の速度、Hitstop、歪み、暗転は変更していない。暗転のチカつき改善は提案段階であり、この動画へ適用済みとは扱わない。再生成は `Tools/Video/encode-parry-impact-slow.ps1`。既存プレビューがある場合は停止し、過去の動画を上書きしない。[追加プレビューの検証記録](validation/parry-impact-slow-preview-2026-11-02.json)を参照。

### スマホへの共有

GitHubの `blob` ページは端末によって動画を表示できず、raw配信も `application/octet-stream` でダウンロード扱いになる場合がある。共有には公開済みコミットを固定した `https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@<commit>/docs/video/previews/<file>.mp4` の直接リンクを使う。今回の公開ファイルについて `Content-Type: video/mp4` と `Accept-Ranges: bytes` を確認した。追加の公開範囲変更は行わない。実際のスマホ端末での再生は未確認なので、再生できない場合に備えて拡大スローGIFも添える。

## 再収録と比較条件

保存済みの `PrototypeCombatValidation` をPlayModeで開き、ULoopの別リクエストでPrepare→Captureを実行する。既存の収録フォルダを上書きしない。再実行は第2引数のrevisionを新しくする。

```csharp
ParryShockwaveCaptureUtility.Prepare();
// 次のEditorフレーム以降の別リクエスト
ParryShockwaveCaptureUtility.Capture(0f, "new-revision");
// Prepareをもう一度実行して別リクエスト
ParryShockwaveCaptureUtility.Capture(.65f, "new-revision");
```

エンコードは `Tools/Video/encode-parry-shockwave-review.ps1 -Revision <revision>`。標準の完成動画や元原稿を上書きしない。

今回の元連番は `.uloop/outputs/video/parry-shockwave-20261102-v2/strength-000` と `strength-065`。各210行のトレースで敵状態・HP・Hit数・逆光・反撃フェーズ・カメラ・揺れ・Hitstop・SlowScaleの差分は0。両方とも敵HP30、主人公HP100で終了する。

待機ループは収録開始までのEditorフレームの違いで開始位相が異なる（元フレーム0〜41）。パリィ開始以降の元フレーム42〜209は主人公Spriteも一致している。異なる待機コマを追加効果の変化として扱わない。

## 実描画で見つけた修正と検証

初回の収録でUVの上下を余分に反転し、接触点と反対の高さに輪が出ることを発見。URPが扱う画面向きを二重に反転しないよう修正した。初回の不正な連番は作業出力に保持し、公開プレビューには使用していない。

`ParryShockwaveCaptureUtility.ValidateProjection()` は画面の中心から外れた位置で実描画を比較する。正しい中心付近の変化3553pixel、上下を反転した位置0pixel、解除後の残留0pixelを確認した。別カメラの変化も0pixelであることを追加テストで確認した。GPUがない実行環境では、この画像検証だけは実行できない。

最終EditModeは253/253、追加のPlayMode連携は11/11。撮影側の230件は225合格・5失敗で、失敗名は10月26日の記録にもある蹴りの高さ2件・タメ時刻3件と一致する。今回の演出追加で生じた照明テストとの干渉は、照明と屈折の契約を別々に検証して解消した。既存5件や全PlayModeが合格したとは扱わない。

コンパイル・テスト・動画デコードの最終結果は [検証記録](validation/prototype-parry-shockwave-2026-11-02.json) を参照。

## 人間による確認

- 通常速度で弾いた感触が増えるか、輪が強すぎないか。
- 連続成功・複数敵で予兆を見失わないか。15分の疲労感と、0%/50%/標準の読みやすさ。
- 左右・空中・壁際で中心が自然か。
- 最低環境のDevelopment/Release Buildで画面コピーによる負荷が許容範囲か。

比較動画の確認だけでこれらを合格扱いにしない。
