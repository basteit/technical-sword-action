# 撮影と再現

作業場所は `C:\Users\hirot\GitHub\technical-sword-action`。Unity 6000.3.9f1、uloop CLI 2.1.3、Aseprite 1.3.18.6で確認した。音声は録画・書き出しに含めない。

## 素材とScene

主人公ラフは `Assets/Art/Player/VideoRough/Player_actions_rough.aseprite`、67コマ・8タグ・6レイヤー。タイミングの正は同フォルダの `timing-manifest.json`（整数60Hzフレーム）。敵は `Assets/Art/Video/CaptureSentinel.aseprite`、背景は `CaptureRuins.aseprite`、VFXは `CombatEffects.aseprite`。今回のラフは源の待機/歩きのハッシュを維持する。

Sceneは `Assets/Scenes/CombatVideo.unity`。Tools → Technical Sword Action → Video → Build Art and Capture Scene で素材/Clip/Animator/Sceneを再生成する。未保存の別Sceneがあると停止する。生成した撮影Sceneも手動変更がある場合は保存してから再生成する。SampleSceneとBasicActionValidationの既存差分を上書きしない。

敵AのHPは2段版2、4段版7。通常攻撃の威力は1/1/2/3、敵BはHP3、蹴りは3。敵Bは敵Aの撃破と攻撃復帰後に0.5秒の実AI予兆を出す。進行はAction要求を送り、成功判定をゲーム内のHit/Parry/Counter処理へ任せる。

## 検証

```powershell
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type regex --filter-value TechnicalSwordAction
uloop run-tests --test-mode PlayMode --filter-type regex --filter-value TechnicalSwordAction
```

Test Runnerは単独実行する。2026-10-12の結果はEditMode 181 / PlayMode 71件 Passed。PlayModeに2段/4段・両方向各10回（計40回）、30/60/120fps相当、Pause、対象消失、壁、無敵、被弾による強制中断、死亡、Disable、反撃中Scene再読込を含む。未保存の既存Sceneを自動で保存・破棄しない。

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
| 2段版 | 230フレーム、3.833333秒 |
| 4段版 | 291フレーム、4.85秒 |
| タメ | 18〜28戦闘F、0.25倍、実時間0.666667秒 |
| Hitstop | 通常2F、通常/Just Parry 2/3F、蹴り8F |
| カメラ | 通常ortho 2.8125、タメ2.109375、座標1/32unitスナップ |
| VFX | ドット本体＋短い加算発光（hybrid=true）、純ドット比較（false） |
| 強度 | VideoVfxRendererのintensityで調整。初期0.8 |
| 素材版 | 主人公Actionはrough、待機/歩きは提供素材、敵/背景/VFXは制作版 |

4段版は全行程が約4.85秒に収まるため、清書が揃えば採用を第一候補とする。必須36コマのみ清書が先に揃う場合は2段版を完成させ、任意17コマの受領後に4段版を再判定する。現段階の動画を最終完成と扱わない。
