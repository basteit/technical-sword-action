# オレンジの追加火花・発光と停止中の表示振動（#33）

## 目的と受入条件

パリィの接点を新しいオレンジの火花・光で強調し、停止中の小さな振動で受け止めた圧力を見せる。元素材を色替えせず、既存の白い光を弱めて追加演出を主役にする。

1. 成功の表示ピークで新しい火花と発光を出す。旧白ハローは0%、白補助光は8%。受領原稿・既存PNGと完成動画設定を維持する。
2. 主人公と弾いた敵は停止中のみ逆方向へ最大2ドット相当揺れ、root・Collider・判定・カメラ・停止36F・待ち8Fを変更しない。
3. 30/60/120fpsと左右、Pause、演出0%、停止解除・中断・リセットで、タイミング・判定・表示位置を壊さず復帰する。

失敗時の予兆・受付・やられは従来のまま。成功時だけ追加演出を出して、何が成功したかを識別できるようにする。数値は操作感の最終承認ではなく仮置き。

## 初期値と構造

- 追加火花：オレンジ基調・14粒・0.32秒、広がり1.2unit。焼けた橙の縁と短い黄橙の先端を使う。受領画像とは独立した再利用メッシュで、元の黄色い作画火花も保持する。
- 追加光：接点中心・0.18秒の滑らかな減衰。従来の大きな白いハローは0%、全身白補助光は40%→8%。暗転30%は維持。
- 振動：仮想32PPUの最大2px、2/60秒ごとに左右を交互にしながら減衰。両者の表示だけを逆位相で動かす。共有Feedback以外にカメラの所有者を増やさない。
- 火花と光はカメラ別の表示。VFX0%で両方消去、Flash0%で光を除去。画面揺れ0%で振動を解除する。停止や成功時刻は設定強度で変えない。
- Pauseで色・粒・振動を停止。対象消失・被弾・死亡・Disable・Scene変更・Resetで、新VFXと所有者つき表示オフセットを消去する。復帰後の描画更新で旧オフセットを復活させない。

`CounterCinematicProfile` の新しいフィールドで変更できる。元の白発光へ戻す比較は、遭遇をリセットし `parryOrangeAccent=false / parryActorJitterPixels=0 / parryActorLight=0.4 / parryLegacyBloomStrength=1`。同期前への復帰は従来どおり `synchronizedParryImpact=false` と遭遇リセット。実行中の無リセット切替は推奨しない。

## 検証

実装 `3bd3044`、収録と動画 `788880b32ab150f6a456b37cda7da3694206a281`。Compile Error0/Warning0、全EditMode288/288、パリィ・反撃PlayMode130/130。全PlayMode797件は784合格・13失敗で、全失敗名が直前版および10月26日の既存記録と一致し、新規失敗0件。既存の動画蹴り高さ2件・タメ時刻3件・4段防御キャンセル7件・旧Just境界期待1件は未解決として保持し、全テスト合格とは扱わない。

全体は197.933秒かかりCLIの180秒上限を超えたが、Unityの最終XMLを回収して比較した。二重実行なし。最後は元の保存済み `PrototypeCombatValidation`、dirty=false・再生/Pauseなし、MissingScript0、専用/共通Validator0、Console Error0/Warning0。

- [実ゲーム全画角・等倍3秒](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@788880b32ab150f6a456b37cda7da3694206a281/docs/video/previews/parry-adaptive-20261102-orange-on.mp4)
- [前回との比較（上が前回、下が今回）](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@788880b32ab150f6a456b37cda7da3694206a281/docs/video/previews/parry-adaptive-20261102-orange-comparison.mp4)
- [1/2速度・観察用](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@788880b32ab150f6a456b37cda7da3694206a281/docs/video/previews/parry-adaptive-20261102-orange-detail.mp4)／[GIF](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@788880b32ab150f6a456b37cda7da3694206a281/docs/video/previews/parry-adaptive-20261102-orange-detail.gif)

同じF1通常ザコの実AI・成功1回・被ダメージ0・敵HP30/主人公HP100を180枚・60fpsで記録。成功17→インパクト25→やられ69枚目は前回と同じ。追加火花20サンプル、接点光11サンプル、振動36サンプル・左右切替17回。最大振動は両者2仮想px、停止中の両root移動量0。旧白ハローの表示0、終了時の新VFX/振動残留0を記録した。

通常カメラのOrthographic Size5.625・16:9全画角を使用し、追加ズーム・切り抜きなし。MP4はH.264 Baseline/Level3.1/yuv420p/faststart/30fps/無音。等倍/観察用1280×720、比較640×720（各画面は全体640×360）。全4ファイルをデコードし、直接URLのHTTP200・MIME・Range206・本体SHA256一致を確認した。実物スマホ上の再生受入とは区別する。

再現は保存済み `PrototypeCombatValidation` でPlayし、ULoopの別リクエストで `AdaptiveParryCaptureUtility.Prepare()` → `Capture(false)`。既定出力は `.uloop/outputs/video/adaptive-parry-20261102-orange-v1/adaptive/`。既存収録は上書きしない。`Tools/Video/encode-adaptive-parry-review.ps1` は本版と直前sync-v2版の全連番を比較する。過去版の再現は各動画コミット時点の補助と設定を基準にする。[詳細な検証記録](validation/prototype-parry-orange-impact-2026-11-02.json)。

人間による各15分の操作感・複数敵の視認性・最低環境Build性能は別途確認する。今回は新ZIPを作成せず、Issue #33全体を完了扱いにしない。
