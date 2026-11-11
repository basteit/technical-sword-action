# 浅い弧・遅れて追いつくズームと派手な接点演出（#33）

最新の復帰時点は[停止解除から戻るカメラと反撃通し](prototype-parry-hitstop-return.md)。本書の終了後保持0.20秒は比較履歴として保持する。弧・光・火花の設定は引き続き有効。

## 目的と受入条件

直線的な寄りを、浅い弧と位置/ズームの時間差で変化させ、参考動画の短い白い芯・横長の閃光・長短の火花を本作のオレンジ演出へ置き換える。元の作画と直前設定を保持する。

1. 位置は浅い上弧を描いて接点へ先着し、ズームは2F遅れて加速し、インパクトで最大4.25へ到達する。終了後の保持0.20秒と復帰0.18秒は維持し、復帰の終端を柔らかくする。
2. 小さな輪状の火花を、長短のある飛散火花と白橙の横長閃光へ拡張する。背景40%・停止30F・受付・敵ピーク同期・主人公前面表示を維持し、Flash/VFX0%で除去できる。
3. 左右・30/60/120fps、Pause、対象消失・被弾・死亡・Reset・Disable・Scene変更、反撃への接続で、判定/カメラ基準/表示オフセットを壊さない。独立した比較フラグで直前の線形カメラ/火花へ戻せる。

成功時はカメラの先行とズームの追いつき、短い明るい接点、速い飛散で勢いを作る。失敗/空振りでは寄りや成功VFXを出さず、成功条件を変えない。今回の調整軸は「カメラの軌道/時間配分」と「接点VFX」の2つ。

## 参考動画の扱い

ユーザー提供の約5.23秒・30fpsの録画を観察した。目立った要素は短い明るい芯、横長の閃光、放射状に伸びる長短の粒、素早い寄りと余韻。3Dの画面回転やキャラクター/色/画像をそのまま移植せず、2Dの視認性とオレンジ基調へ翻案する。録画・抽出画像・音声は解析用の無視ディレクトリだけに置き、リポジトリや提出動画へ含めない。

## 実装した試用値

- カメラ位置はSmoothStepで接点へ寄り、移動途中だけ上に最大0.20unitの浅い弧を加える。現行の準備8Fに対し6Fで接点へ先着する。
- ズームは最初の2Fを待ち、残りを二次加速して8F目にSize4.25へ到達する。位置・ズーム・復帰は独立した `AnimationCurve`。保持0.20秒は変更せず、0.18秒の復帰は終端を減速させる。回転や大きなオーバーシュートは使わない。
- 追加火花を14→32本、広がり設定1.2→3.2unitへ拡張し、角度・長さにばらつきを持たせる。白い芯とオレンジの縁を持つ横長10.5unitの閃光に、短い斜めの光2本を重ね、0.10秒で減衰させる。
- 停止30F（0.50秒）、背景減光40%、最大寄り4.25、0.10秒の画面揺れ、最大2仮想pxの表示振動、主人公前面表示は直前版のまま。成功条件・判定・HP・敵ピーク同期・反撃は変更しない。

設定は本編用 `CounterCinematic.asset` と `CounterLaunch_ReferenceTrial.asset` の両方へ反映した。受領原稿、PNG、Clip、完成動画用Scene/Libraryへ変更を加えていない。光・火花は接点へ固定した再利用メッシュで描き、カメラ位置/サイズの書き込みは既存の `CombatFeedback2D` に集約する。

## 動画と実描画の確認

- [等倍3秒・ゲーム内カメラ](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@f1e6338cb7508fda608ccbbc357083f0f5d01401/docs/video/previews/parry-adaptive-20261102-expressive-on.mp4)
- [直前/今回の比較（上が直前、下が今回）](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@f1e6338cb7508fda608ccbbc357083f0f5d01401/docs/video/previews/parry-adaptive-20261102-expressive-comparison.mp4)
- [1/2速度・観察用](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@f1e6338cb7508fda608ccbbc357083f0f5d01401/docs/video/previews/parry-adaptive-20261102-expressive-detail.mp4)／[GIF](https://cdn.jsdelivr.net/gh/basteit/technical-sword-action@f1e6338cb7508fda608ccbbc357083f0f5d01401/docs/video/previews/parry-adaptive-20261102-expressive-detail.gif)

F1の実AIでパリィ1回を180枚/60fpsで収録。成功17→インパクト25→やられ63枚目、停止30Fは直前版と一致。位置は23枚目に接点へ到着し、ズームは25枚目に最大となる。位置の上弧は左右両方向の単体テストと実カメラのトレースで確認した。閃光6サンプル、火花20サンプル、最大32本、振動30サンプル/左右交替14回。主人公HP100/敵HP30、両rootの停止中移動0、表示順20/8を維持。保持63→復帰開始75→通常画角86枚目で、終了時のVFX・表示ずれ・カメラ残留0。

編集側の拡大/切り抜きや光の後付けは行わず、ゲームカメラの全画角をリサイズしている。HUDではなくカメラの実描画を記録した。等倍/観察用1280×720、比較640×720（各全画角640×360）、H.264 Constrained Baseline/Level3.1/yuv420p/faststart/30fps/無音。元の接触画像と納品動画のコンタクトシートを目視し、MP4/GIF全4ファイルを最後までデコードした。配信HTTP200・MP4のMIME・Range206・ダウンロードSHA256一致も確認。実物スマホ上での再生確認は未実施。

## 検証・再現

実装 `fb44305`、動画/収録補助 `f1e6338cb7508fda608ccbbc357083f0f5d01401`。Compile Error0/Warning0、全EditMode304/304、パリィ・反撃PlayMode156/156。

全PlayMode824件は811合格・13失敗。失敗名と失敗メッセージは直前版の13件と一致し、新規失敗0。既存の動画蹴り高さ2件・タメ時刻3件・4段防御キャンセル7件・旧Just境界期待1件を未解決として保持する。全体244.535秒でCLIの180秒上限を超えたため、重複実行せずUnityの最終XMLを回収した。全体内のパリィ・反撃156件もすべて合格。

最終Editorは保存済み `PrototypeCombatValidation`、dirty=false、再生/Pauseなし、主人公Sorting Order20、MissingScript0、専用/共通Validator0、Console Error0/Warning0。[検証記録](validation/prototype-parry-expressive-trial-2026-11-02.json)。

保存済み `PrototypeCombatValidation` をPlayし、別ULoopリクエストで `AdaptiveParryCaptureUtility.Prepare()` → `Capture(false)`。既定出力は `.uloop/outputs/video/adaptive-parry-20261102-expressive-v1/adaptive/`。同じ出力へ上書きせず、新しいrevisionを指定する。エンコードは `Tools/Video/encode-adaptive-parry-review.ps1`。比較元は直前の `20261102-camera-v1`。過去版は各動画コミットの補助/設定で再現する。

## 調整と戻し方

`CounterCinematicProfile.parryExpressiveCamera=false` で直前の線形カメラへ、`parryReferenceAccent=false` で直前の14本火花/閃光なしへ、それぞれ独立して戻せる。停止・判定・他の演出値は変わらない。新しいカーブや数値は保持するので再ONできる。4通りの組合せをPlayModeテストで確認した。実行中の設定変更前には遭遇をリセットする。

カメラ寄り自体は `parryCameraEnabled=false`、追加火花/光は `parryOrangeAccent=false`。プレイヤー向けの画面揺れ0%はカメラ移動/揺れ/表示振動を、Flash0%は発光/閃光を、VFX0%は追加火花/光/閃光を除く。Pause中の0%化でも以前の光を復活させない。無関係な未コミット差分を捨てるcheckoutは行わない。

## 人間による確認

数値は仮置き。派手さとキャラの読みやすさ、連続パリィ時の眩しさ/カメラ酔い、複数敵・壁際・空中・反撃連携、実物パッド/キーボード各15分、最低環境Build性能は別途確認する。今回は新ZIPを作成しておらず、Issue #33全体や最終的な操作感の受入を完了扱いにしない。
