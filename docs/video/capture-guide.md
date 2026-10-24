# 撮影と再現

自由操作で敵・足場・アクションを試し、SNS用の静止画／動画を撮る場合は[撮影スタジオの使い方](studio-guide.md)へ。以下は決まったシナリオを全フレーム収録する手順。

2026-10-24最新：[固定被弾ポーズで浮いて止まるノックバック](enemy-knockback-arc-20261024.md)。1体目は後方0.40/上昇0.12unit、2体目は後方0.80/上昇0.18unit、どちらも18戦闘Fで着地して停止→消滅する。パリィ後も撃破時と同じdownコマを固定。通常ortho3.375/タメ2.8125/命中最小2.64375、強い揺れ、4F/Just5F・キック10F停止と0.900秒タメを保持。下記の初期ラフの表より最新記録を優先する。[距離と停止時間](mid-camera-hitstop-20261024.md) / [前版の距離比較](counter-revision-and-camera-comparison-20261024.md)。`CaptureAtDistance(normalSize,closeSize)` で一時比較でき、収録中の背景/床補完と比較距離は終了時に戻す。

作業場所は `C:\Users\hirot\GitHub\technical-sword-action`。Unity 6000.3.9f1、uloop CLI 2.1.3、Aseprite 1.3.18.6で確認した。音声は録画・書き出しに含めない。

## 素材とScene

2026-10-16：攻撃1段目は受領した9コマの仮提出へ部分差し替え、反撃は頭上ワープ/かかと落とし/落雷へ更新。[素材・同期点・既存Sceneへの適用手順](attack1-submission-and-heel-drop.md)。1段目の後ろ髪/復帰は今回の通しに要求せず、全体清書の完了とは区別する。既存119コマのラフは履歴/未提出Actionのフォールバックとして保持する。

主人公ラフは `Assets/Art/Player/VideoRough/Player_actions_rough.aseprite`、119コマ・18タグ・8レイヤー。瞳のガイド/清書レイヤーと動作別の復帰タグを含み、全タグの開始/終了コマを保存後の再読込で検証した。タイミングの正は同フォルダの `timing-manifest.json`（整数60Hzフレーム）。瞳の専用PNGは `EyeMasks`、対応と画素数は `eye-mask-manifest.json`。敵は `Assets/Art/Video/CaptureDummy.aseprite`、白いグリッド背景と、接地位置より下を埋める灰青色一色の床は `CaptureTrainingGrid.aseprite`、VFXは `CombatEffects.aseprite`。元の待機/歩きのハッシュを維持する。

Sceneは `Assets/Scenes/CombatVideo.unity`。Tools → Technical Sword Action → Video → Build Art and Capture Scene で素材/Clip/Animator/Sceneを再生成する。未保存の別Sceneがあると停止する。生成した撮影Sceneも手動変更がある場合は保存してから再生成する。SampleSceneとBasicActionValidationの既存差分を上書きしない。

敵AのHPは2段版2、4段版7。通常攻撃の威力は1/1/2/3、敵BはHP3、蹴りは3。敵Bは敵Aの撃破と攻撃復帰後に0.5秒の実AI予兆を出す。進行はAction要求を送り、成功判定をゲーム内のHit/Parry/Counter処理へ任せる。

## 検証

```powershell
uloop compile
uloop run-tests --test-mode EditMode --filter-type regex --filter-value TechnicalSwordAction
uloop run-tests --test-mode PlayMode --filter-type regex --filter-value TechnicalSwordAction
```

Test Runnerは単独実行する。2026-10-15のスタジオ追加版はEditMode 187 / PlayMode 185件 Passed。PlayModeに2段/4段・両方向各10回（計40回）、30/60/120fps相当、Pause、対象消失、壁、無敵、強制中断、死亡、Disable、Scene再読込、解除→命中→着地、瞳/Material、段別の実前進/ヒットバック、VFX追従を含む。4段各段・成功/失敗パリィの無入力復帰、歩き/各行動による復帰キャンセル、入力を消費しないこと、揺れ/FlashのPauseと復帰も確認した。影を残す2Fの接近/表示復帰、途中の障害物、空中吹き飛び/壁/回転のリセット、着地直後のPauseによる撮影終了の防止も含む。同期の6件では30/60/120fps×両方向でスロー開始/解除/命中停止のSprite一致と下向きの衝撃を確認する。未保存の既存Sceneを自動で保存・破棄しない。

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
| 接近の見せ方 | 開始〜18Fは体を隠し、黒い残像/足元の影だけを表示。最初の2戦闘Fで実際の対象の前へ移動し、18Fからスローの蹴り姿勢を表示。壁で停止、中断/Reset/Disableで表示を復帰 |
| 振り抜き | 倒立寄りに膝を畳んでタメ、24Fで通常速度へ戻して全身を縦回転させ、28Fの命中まで4F/0.066667秒で上→前→下へ脚を振り下ろす |
| 着地/復帰 | 28〜36Fの落下（停止を除き0.133333秒）。36FでAction/無敵を終了、続く自由な復帰は7ポーズ/33F/0.55秒。手を離す→膝/上体を起こす→髪/衣服が落ち着く |
| 復帰キャンセル | 地上・無入力時に再生。歩き/攻撃/パリィ/Dash/Jump/Special/Heal/Interact等でキャンセル、追加ロックなし。通常の資源/クールダウンは維持 |
| Hitstop | 通常斬撃2F、通常/Just Parry 4/5F、蹴り10F（2026-10-24） |
| カメラ | 通常ortho3.375、タメ2.8125（1080pで5倍/6倍）、基準Y=1.05、座標1/32unitスナップ。比較2本の中間を採用。電撃の外縁すべてを収める必要はない |
| 揺れ | 斬撃0.04/0.055/0.075/0.11unit・0.09/0.10/0.11/0.14秒、パリィ0.22unit/9F、蹴り0.38unit/0.30秒（読みやすさ設定）、着地0.065unit/0.11秒。各方向の反動と減衰を1つのカメラ所有者で合成 |
| 命中アップ | キックで2Fかけてortho2.64375へ寄せ、6F保持→12Fで通常3.375へ復帰。実時間で進み、Pauseでは停止。Reset/Disableで即解除 |
| 命中Flash | 蹴りのみ設定最大12%、60fpsの実描画は8%→4%→0。2描画Fで減衰し3Fで消える。揺れ/Flashは実時間、Pause中は停止、リセット/中断で残留なし |
| スロー中の暗さ | 黒96.5%、入り/戻り各3/60秒。主人公は通常色の像を不透明度60%で重ね、瞳のみ白青色の光と8描画Fの残光。Pause中は停止、リセット/Disableで即解除 |
| 前進/敵ヒットバック | 前進0.12/0.20/0.28/0.40unit、ヒットバック0.10/0.18/0.30/0.58unit。蹴りは横2.25unit/高さ1.05unit/20戦闘Fの吹き飛びと回転。壁で止め、消滅VFXは実個体へ追従。撮影の敵の一律ノックバックは0で二重移動を防止 |
| 敵B/蹴り接近 | 初期x=-0.85、最大接近1.2unit。実際に弾いた敵を固定し、開始後の対象変更なし |
| VFX | 採用確定：ドット本体＋短い加算発光（hybrid=true、2026-10-12のユーザー選択）。純ドット（false）は比較・調整用 |
| 強度 | VideoVfxRendererのintensityで調整。初期0.8 |
| 床 | 接地位置world y=0（背景の上端から156行）より下はすべて不透明な灰青色一色（RGB 164/179/199）。模様・継ぎ目・縁取りなし。白いグリッドは床より上だけ。別Asepriteレイヤーで編集可能 |
| VFXサイズ/背景対策 | 斬撃0.65/0.80/1.00/1.25、パリィ1.25、蹴り1.55倍。白い背景でも読める暗い1pxの縁・深い金色/青緑色。通常命中の短い停止は維持 |
| 素材版 | 主人公は頭を含め全身を新規作画したrough。撮影中の待機/歩きも同じ作画のIdleRough/WalkRough。提供素材は非表示参照に保持し、必須清書が揃った際に待機/歩きへ再接続。敵/背景/VFXは制作版 |

4段版は全行程が約4.92秒に収まるため、清書が揃えば採用を第一候補とする。復帰を含む必須60コマが先に揃う場合は2段版を完成させ、4段追加25コマの受領後に4段版を再判定する。現段階の動画を最終完成と扱わない。

最終書き出しは採用した混合版で `CapturePreview(2,true)` または `CapturePreview(4,true)` を使う。短い発光を接触時へ集中させ、清書統合後に足・刀・敵の反応が隠れないことを再確認する。

2026-10-13 ラフ改善版: 蹴りは横軸に近い軸で頭/肩/腰を回し、倒立寄りのタメ→脚を伸ばして縦に振り下ろす→膝を畳んで着地へ変更。斬撃は段間の重心/足位置をつなぎ、命中時の前膝を曲げて踏ん張る。横斬りの直後に刀が逆側へ飛ぶ振り抜きを修正し、パリィは腰と肩の押し込みを強めた。Action/命中/スロー/着地の整数フレームは維持。

撮影でスロー開始時に予備姿勢が1tick残り、命中停止がひとつ前の絵を保持するずれを発見した。CounterのTimer Event後に同じ整数FのSpriteを選び、18Fのスロー開始は6コマ目、24Fの解除は12コマ目、28Fの命中停止は16コマ目で表示する。Animatorの補間時間を当たり判定へ逆流させず、清書も同じタイミング表で接続する。蹴りの衝撃方向は下向き主体。検証の日時/件数は`previews/training-validation.json`を参照。斬撃の4段すべての命中姿勢と、描いた蹴り足/VFX/対象Colliderの一致を左右反転で検証する4件を追加した。

最終素材で40回再実行と候補3本の再収録/全フレームデコードを実施。2段3.9秒/4段4.916667秒、1080p60/音声なし、Console Error 0 / Warning 0。検証の詳細は`previews/training-validation.json`。

2026-10-13再改善: 蹴りは19コマ。スロー中を2姿勢から6姿勢へ増やし、解除後は4姿勢で脚を振り下ろす。命中後は低い片手着地を保ってから立ち上がる。24/26Fに短い体の残像を置き、衝撃波の中心を空けて足を隠さない。前版との同時比較は`previews/combat-readability-before-after.mp4`（左Before、右After）。

2026-10-13軽い弾き修正: 成功tickで接触姿勢を即表示、刀の(51,18)pxにVFX、敵武器の上への分離を同期。カメラ0.135unit/7F/1.25周期、白背景へ2描画Fの暗いアクセントを追加。Normal/Just停止2/3Fと3候補の尺は維持。[軽重の区別](light-parry-reference-notes.md)・[パリィの比較](previews/parry-light-before-after.mp4)・[作画ガイド](first-slash-authoring-guide.md)を参照。検証はPlayMode141/141・EditMode181/181・40回反復、詳細日時はtraining-validation.json。
