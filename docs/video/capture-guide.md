# 撮影と再現

作業場所は `C:\Users\hirot\GitHub\technical-sword-action`。Unity 6000.3.9f1、uloop CLI 2.1.3、Aseprite 1.3.18.6で確認した。音声は録画・書き出しに含めない。

## 素材とScene

主人公ラフは `Assets/Art/Player/VideoRough/Player_actions_rough.aseprite`、98コマ・16タグ・8レイヤー。瞳のガイド/清書レイヤーと動作別の復帰タグを含み、全タグの開始/終了コマを保存後の再読込で検証した。タイミングの正は同フォルダの `timing-manifest.json`（整数60Hzフレーム）。瞳の専用PNGは `EyeMasks`、対応と画素数は `eye-mask-manifest.json`。敵は `Assets/Art/Video/CaptureSentinel.aseprite`、背景は `CaptureRuins.aseprite`、VFXは `CombatEffects.aseprite`。元の待機/歩きのハッシュを維持する。

Sceneは `Assets/Scenes/CombatVideo.unity`。Tools → Technical Sword Action → Video → Build Art and Capture Scene で素材/Clip/Animator/Sceneを再生成する。未保存の別Sceneがあると停止する。生成した撮影Sceneも手動変更がある場合は保存してから再生成する。SampleSceneとBasicActionValidationの既存差分を上書きしない。

敵AのHPは2段版2、4段版7。通常攻撃の威力は1/1/2/3、敵BはHP3、蹴りは3。敵Bは敵Aの撃破と攻撃復帰後に0.5秒の実AI予兆を出す。進行はAction要求を送り、成功判定をゲーム内のHit/Parry/Counter処理へ任せる。

## 検証

```powershell
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type regex --filter-value TechnicalSwordAction
uloop run-tests --test-mode PlayMode --filter-type regex --filter-value TechnicalSwordAction
```

Test Runnerは単独実行する。2026-10-12の最新修正版はEditMode 181 / PlayMode 108件 Passed。PlayModeに2段/4段・両方向各10回（計40回）、30/60/120fps相当、Pause、対象消失、壁、無敵、強制中断、死亡、Disable、Scene再読込、解除→命中→着地、瞳/Material、段別の実前進/ヒットバック、VFX追従を含む。4段各段・成功/失敗パリィの無入力復帰、歩き/各行動による復帰キャンセル、入力を消費しないこと、揺れ/FlashのPauseと復帰も確認した。未保存の既存Sceneを自動で保存・破棄しない。

## 録画

撮影Sceneを開いてPlayに入り、起動/Domain Reloadが完了した後に実行する。

```powershell
uloop control-play-mode --action Play
uloop execute-dynamic-code --code 'return VideoCaptureUtility.ValidateResetRuns();'
uloop execute-dynamic-code --code 'return VideoCaptureUtility.CapturePreview(2,true);'
uloop execute-dynamic-code --code 'return VideoCaptureUtility.CapturePreview(4,true);'
uloop execute-dynamic-code --code 'return VideoCaptureUtility.CapturePreview(4,false);'
uloop get-logs --log-type Error
uloop get-logs --log-type Warning
uloop control-play-mode --action Stop
```

通常プレビューは `CombatVideoDirector.StartTake(2/4, hybrid)`。録画は共通時計を1/60秒ずつ進め、各描画フレームをUnityのCamera Render Requestから1920×1080で取得する。スロー・Hitstopのフレームも保存するため、編集機の処理速度で尺や入力が変わらない。無音の動画再生の実時間はフレーム数÷60で測る。エンコード時にも全フレームの存在と3〜6秒を確認する。

連番とEvent記録は `.uloop/outputs/video/{2stage-hybrid,4stage-hybrid,4stage-pixel}/`。フレームを省略せずにH.264 MP4へエンコードする。

```powershell
./Tools/Video/encode-captures.ps1 -FfmpegPath 'FFmpeg実行ファイルの絶対パス'
```

この作業環境では `.uloop/outputs/python-deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe` を使用した。FFmpegはシステムのPATHでも利用可能。依存を新たに用意する場合は `imageio-ffmpeg` を作業出力内へ導入する。インストール済みのUnity/Aseprite/uloop設定は書き換えない。

## 撮影設定と結果

| 項目 | 設定/結果 |
|---|---|
| 形式 | 1920×1080 / H.264 / yuv420p / 60fps / 音声Streamなし |
| 2段版 | 234フレーム、3.9秒 |
| 4段版 | 295フレーム、4.916667秒 |
| タメ | 18〜24戦闘F、0.15倍、実時間0.666667秒 |
| 振り抜き | 24Fで通常速度へ戻し、28Fの命中まで4F/0.066667秒 |
| 着地/復帰 | 28〜36Fの落下（停止を除き0.133333秒）。36FでAction/無敵を終了、続く自由な復帰は7ポーズ/33F/0.55秒。手を離す→膝/上体を起こす→髪/衣服が落ち着く |
| 復帰キャンセル | 地上・無入力時に再生。歩き/攻撃/パリィ/Dash/Jump/Special/Heal/Interact等でキャンセル、追加ロックなし。通常の資源/クールダウンは維持 |
| Hitstop | 通常2F、通常/Just Parry 2/3F、蹴り8F |
| カメラ | 通常ortho 2.8125、タメ2.109375、座標1/32unitスナップ |
| 揺れ | 斬撃0.04/0.055/0.075/0.11unit・0.09/0.10/0.11/0.14秒、パリィ0.095unit/0.12秒、蹴り0.20unit/0.22秒、着地0.065unit/0.11秒。各方向の反動と減衰を1つのカメラ所有者で合成 |
| 命中Flash | 蹴りのみ設定最大12%、60fpsの実描画は8%→4%→0。2描画Fで減衰し3Fで消える。揺れ/Flashは実時間、Pause中は停止、リセット/中断で残留なし |
| スロー中の暗さ | 黒82%、入り/戻り各3/60秒。主人公は通常色の像を不透明度60%で重ね、瞳のみ白青色の光と8描画Fの残光。Pause中は停止、リセット/Disableで即解除 |
| 前進/敵ヒットバック | 前進0.12/0.20/0.28/0.40unit、ヒットバック0.08/0.14/0.24/0.40unit、蹴り0.65unit。撮影の敵の一律ノックバックは0で二重移動を防止 |
| 敵B/蹴り接近 | 初期x=-0.85、最大接近1.2unit。実際に弾いた敵を固定し、開始後の対象変更なし |
| VFX | 採用確定：ドット本体＋短い加算発光（hybrid=true、2026-10-12のユーザー選択）。純ドット（false）は比較・調整用 |
| 強度 | VideoVfxRendererのintensityで調整。初期0.8 |
| 素材版 | 主人公Actionはrough、待機/歩きは提供素材、敵/背景/VFXは制作版 |

4段版は全行程が約4.92秒に収まるため、清書が揃えば採用を第一候補とする。復帰を含む必須53コマが先に揃う場合は2段版を完成させ、4段追加25コマの受領後に4段版を再判定する。現段階の動画を最終完成と扱わない。

最終書き出しは採用した混合版で `CapturePreview(2,true)` または `CapturePreview(4,true)` を使う。短い発光を接触時へ集中させ、清書統合後に足・刀・敵の反応が隠れないことを再確認する。
