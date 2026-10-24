# 撮影スタジオ

SNS投稿・進捗共有と、敵／足場／アクションを試すための常設Scene。決まった流れを再生する従来の動画Sceneとは別に、通常のキー／ゲームパッドで自由に操作できる。

## 最初の使い方

1. Unityの **Tools → Technical Sword Action → Video → Open Studio** を選ぶ。
2. 「撮影スタジオ」パネルの「セット」で、アクション／コンボ／パリィ／足場／複数敵から選び、**このセットを配置してリセット**を押す。
3. **Playで試す → Game画面へ**。移動・攻撃・弾き・ジャンプ・ダッシュはプロジェクトの通常入力。パネルに現在のキーボード割当を表示する。ゲームパッドも対応。
4. 撮り直しは **F2**。死亡・コンボ・蹴りのスロー・床抜け・Pauseの途中からも復帰できる。
5. 「撮影」から距離・追従を調整。**F8**で静止画、**F9**で録画開始／終了。保存完了を待ってから共有する。

パネルはGame画面の横へドックすると使いやすい。ウィンドウは最低340px幅。撮影中も操作パネルや診断文字はカメラの出力に含まれない。

| キー | スタジオの操作 |
|---|---|
| F1 | Game画面の診断表示を切替（既定は非表示） |
| F2 | 全体リセット。HP・位置・AI・入力・時間・カメラ・VFX・足場を戻す |
| F3 | 最初の敵を1回だけ攻撃させる |
| F8 | 1920×1080のPNGを保存 |
| F9 | 無音MP4録画を開始／終了 |

標準の入力はA/Dで移動、Spaceでジャンプ、S＋Spaceで床抜け、Jまたは左クリックで攻撃、Kまたは右クリックで弾き、ShiftでDash、EscでPause。入力Assetを変更した場合はパネルの表示を優先する。

## セットと保存

| 初期セット | 用途 |
|---|---|
| アクション | 敵なしの平面。姿勢・移動・着地・キャンセル可能な復帰を確認 |
| コンボ | 前方にHP20の待機ダミー。4段コンボや攻撃キャンセルを確認 |
| パリィ | 背後にHP3の敵。予備動作0.65秒、回復後1.2秒待って反復。反撃で撃破可能 |
| 足場 | ブロック・すり抜け床・往復する移動床 |
| 複数敵 | 左右から既存の近接AIで追跡する2体 |

- **現在のセットを別名保存**は、管理下の敵・ギミック、登録した主人公のリセット位置、向き、カメラ設定を新しい `.asset` に保存する。元のセットを上書きしない。
- Play中の設定はPlay終了で戻るため、残したい配置は終了前に別名保存し、次回そのAssetを指定して配置する。
- **現在位置をリセット位置に登録**は、現在の立ち位置・敵・ギミックの位置をF2の戻り先にする。移動床の移動中に押すと、その位置が新しい往復の起点になる。
- **セットの左右を反転**で、主人公の戻り位置を軸に敵・足場・移動方向と主人公の向きを反転できる。CustomContentは移動しない。
- Play前にパネルやScene Viewで変更したHP／位置／足場は、その状態からPlayを開始する。開始時に元Assetへ戻す処理は入れていない。
- Sceneそのものの編集を残す場合は、通常どおりSceneを保存する。別構成を常設したい場合はSceneを別名保存しても使える。
- Open Studioは既存Sceneを開くだけ。生成メニューは既存のスタジオSceneを上書きしない。

セットAssetは `Assets/Settings/VideoStudio/`。Inspectorの配列で敵やギミックをまとめて定義することもできる。「セットAssetの設定」を編集したら、**このセットを配置してリセット**で反映する。

## 敵を試す

「敵」タブで追加位置を入力してダミーを追加し、対象を選ぶ。HP、リセット位置、攻撃方法、予備動作、間隔、ダメージ、追跡速度を個別に変更できる。

- **待機**：通常は攻撃しない。F3や「この敵が1回攻撃」で単発を試せる。
- **1回だけ攻撃**：リセット後、指定間隔を待って1回攻撃し、回復を終えると待機する。
- **定間隔で反復**：最初は指定間隔待機。攻撃と回復を終えてから指定間隔待ち、次を出す。
- **追跡AI**：既存の近接AIが主人公へ接近し、通常の攻撃・回復・Cooldownを使う。

「パリィを実判定で試す」「パリィ → 反撃」は、選んだ敵の攻撃に通常のParry／Attack入力を送る補助。ダメージや成功結果を直接作らない。敵は主人公から1.8unit以内、壁のない位置へ配置する。弾いた実際の個体に向き直り、その対象に反撃する。射程外や敵の動作中はパネル下部に理由を表示する。

敵を設定し直す・除く・セットを切り替える際は、進行中の反撃と時間効果を解除してから変更する。二重の攻撃予約を防ぎ、単発が意図せずループしないようにしている。死亡した敵は自動復活しない。F2で一括復活する。

既定Prefabは `Assets/Prefabs/VideoStudio/StudioMeleeDummy.prefab`。セットの各敵の `prefab` を別の近接Prefabに変える場合は、`StudioEnemyActor`、`EnemyMeleeChaser2D`、`Damageable2D`、Collider2Dを持たせる。Prefabの接続を維持するので、見た目を更新して再利用できる。遠隔敵・ボスの汎用撮影制御は今回追加していない。

## ギミック・手置きのコンテンツ

「ギミック」タブからブロック／すり抜け床／移動床を追加し、位置・幅・高さを変更する。移動床には移動量と往復の秒数を設定できる。上の主人公を運び、スロー・ヒットストップ・Pauseには戦闘時計と一緒に追従する。床抜けの衝突無視もF2で解除する。

Hierarchyの役割：

| ルート | 内容 |
|---|---|
| 01_Stage | 白グリッド、y=0以下の単色床、左右の境界 |
| 02_Player | 動画と共通の主人公・戦闘判定・ラフ・復帰 |
| 03_StudioCamera | 撮影カメラ |
| 04_Effects | 共通VFX・暗転・瞳・カメラ演出 |
| 05_SetEnemies | セットが管理する敵。セット切替で置換 |
| 06_SetGimmicks | セットが管理するギミック。セット切替で置換 |
| 07_CustomContent | 手置きのPrefab・試作ギミック。セット切替でも保持 |
| 08_StudioControls | セット・リセット・操作補助 |

手置きのものは **07_CustomContent** の下に置き、戻したいルートへ **StudioResetAnchor** を追加する。Play開始時のローカル位置・回転・拡縮・有効状態、Rigidbody2Dの速度／重力、Colliderの有効状態、Damageable2DのHP、近接AIをF2で復元する。状態を持つ独自ギミックは、同じGameObjectのコンポーネントに `IStudioResettable` を実装して内部タイマーなども戻す。

```csharp
public void ResetStudioState()
{
    elapsed = 0;
    activated = false;
}
```

独自コンテンツを `Destroy` した場合の再生成や、未知のAI・生成したProjectileの掃除までは自動化しない。再利用するPrefab側でリセットを実装する。手置きコンテンツはSceneへ保存し、セットAssetへの保存対象には含めない。

## 撮影と書き出し

「撮影」タブで、主人公への追従／固定中心と、アップ・近め・広め・ステージ確認の距離を選択する。1080pで整数倍率になる距離を用意している。VFXは採用済みのドット＋短い発光が既定で、比較用の純ドットに切替可能。蹴りの暗転・瞳・短いスローと揺れは動画と共通。

2026-10-24の調整では、パリィ後の敵は提出chanceコマで静止し、撃破後は主人公を向いたdownコマで地面を滑りながら消える。非致死の通常ヒットバックと実在する壁の判定は保つ。パリィ/キックの揺れを強め、キック命中で短く一段アップして戻る。[演出の数値と確認記録](enemy-reaction-impact-20261024.md)。

録画は**1920×1080／60fps／無音MP4**。自動停止は6／10／15／30秒、またはF9で手動終了を選べる。既定の保存先は **`output/video-studio/`**（Git管理外）。連番の日時名でPNG・MP4・対応するJSONを保存する。フォルダ変更と、最後に出力したファイルの表示もパネルから可能。

- Cameraの描画を取り込むため、デスクトップや他アプリ、操作パネル、OnGUIの情報表示は映らない。
- Game画面を開いて録画を開始する。ステージ使用中だけバックグラウンドでも進行を許可し、終了時に元へ戻す。
- スローとヒットストップ込みの実時間を使う。録画前にGame画面が止まっていた時間は含めない。
- 描画が60fps未満の場合は画像を重複して尺を維持する。**60種類の絵を毎秒保証する仕組みではない。** JSONの `repeatedFrames` と完成尺を確認し、動きがカクつく場合はGame画面へ集中して録り直す。確定シナリオを欠落なしで出力する場合は、従来の決定的な連番撮影を使う。
- GPU／エンコードが追いつかない場合、録画を停止して失敗理由を表示する。取得0枚を成功扱いにしない。`frames` はエンコーダへ渡した枚数、`scheduledFrames` は取得予定枚数。
- Editor自体のPauseやPlay終了でも録画を終えてMP4を仕上げる。「戦闘を一時停止」は凍った画面を録り続ける。書き出し中は新しい録画を開始しない。

FFmpegが必要。PATH上の実行ファイル、または既存のローカル動画作業用ランタイムを自動検出する。別環境では「FFmpeg本体を指定」で `ffmpeg.exe` を選ぶ。インストール先はEditorの個人設定に保存し、他の人の環境に固定しない。静止画にはFFmpeg不要。

実装にはUnityの[非同期GPU読戻し](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/rendering/asyncgpureadback)を使用。動画の圧縮はEditorのメイン処理から分離している。

## 元の待機・歩きの確認動画

[標準速度の6秒確認動画](previews/studio-idle-walk-standard.mp4)は、`Assets/Art/Player/Player_idle.aseprite` と `Player_walk.aseprite` をAseprite本体から毎回読み出し、スタジオのCameraで撮影したもの。敵・ギミック・HUDを隠し、通常のSampleScene／BasicActionValidationと同じカメラサイズ5・固定カメラで左右の歩きを確認できる。[採用前の通常画角版](previews/studio-idle-walk-game-scale.mp4)と[以前のアップ版](previews/studio-idle-walk-original.mp4)も比較用に保持する。

| 実時間 | 動き |
|---|---|
| 0.0–1.2秒 | 右向きの待機 |
| 1.2–2.8秒 | 右へ歩く |
| 2.8–3.4秒 | 待機 |
| 3.4–5.0秒 | 左へ歩く |
| 5.0–6.0秒 | 左向きの待機 |

2026-10-16に、下記の比較の**1.5倍版を標準の歩行速度として採用**。`PlayerMotor2D.StandardMoveSpeed` は **4.4019unit/s**、現行床の摩擦込みで実測約 **4.2057unit/s**。コンポーネントの初期値と、SampleScene／BasicActionValidation／CombatVideo／CombatVideoDefenseCancel／CombatVideoStudioのプレイヤー設定を一致させる。今後の引数なしの撮影もこの標準値を使う。

待機は6コマ×100ms、歩きは8コマ×80msの元のタイミングを使い、速度採用に伴うモーションの再生速度変更は行わない。60fpsへ時刻でサンプリングするため、歩きの各コマは4または5フレーム表示となる。通常ゲームの画角に合わせ、1080pで1ドット3.375pxとなるPoint描画を使う。黒い接地ガイド専用の `Layer 1` だけを書き出し時に非表示にし、元のAsepriteは変更しない。未知の絵がガイドレイヤーに追加された場合は、非表示処理を拒否する。

再収録する場合はスタジオをPlayにし、プロジェクト直下のPowerShellで順に実行する。Asepriteの既定パスはユーザー指定のSteam版。異なる環境では `VideoStudioReferenceCapture.cs` のパスを変更する。

```powershell
uloop execute-dynamic-code --code 'return VideoStudioReferenceCapture.Capture();'
./Tools/Video/encode-idle-walk.ps1
```

360枚すべてを戦闘時計で1/60秒ずつ進めて撮影し、描画速度に依存する録画落ちを避ける。連番PNGは `.uloop/outputs/video/studio-idle-walk-standard/`、MP4とタイミングJSONは `docs/video/previews/studio-idle-walk-standard.*` に出力。撮影後はカメラ・歩行速度・入力・Animator・敵／ギミックの表示を復元してリセットする。通常のF9録画では引き続き現在のゲーム素材を使用する。

### 中間の距離で標準速度を確認する

[中間画角の待機・歩き](previews/studio-idle-walk-standard-medium.mp4)は、以前のアップ版と通常ゲーム画角版の間として、1080pで1ドット6px（カメラサイズ2.8125）を使う。主人公は画面高の約3割に収まり、標準速度4.4019/床上実測約4.2057unit/sで左右へ歩く。歩行範囲の中央へ固定カメラを置き、元の待機6×100ms/歩き8×80msを維持する。6秒・1080p60・無音。

```powershell
uloop execute-dynamic-code --code 'return VideoStudioReferenceCapture.CaptureMedium();'
./Tools/Video/encode-idle-walk.ps1 -Take standard-medium
```

通常の標準版や過去の比較動画は保持し、カメラ変更はこの撮影中だけ適用して終了時に復元する。

[検証記録](previews/idle-walk-medium-validation.json)：360枚の元Sprite/タイミングと画面内の収まり、速度実測、カメラ/入力/表示の復元、MP4全フレームのデコードを確認。Unity compile/収録後ConsoleはError 0 / Warning 0。

### 採用時の速度比較（上が現在の標準）

[採用時の同時再生版](previews/studio-idle-walk-speed-comparison.mp4)は、採用前の通常画角版（移動設定3unit/s）を1.0倍とし、**上が移動1.5倍、下が移動2.0倍**。上を標準として採用した。両方とも同じ開始位置・カメラ中心・サイズ5・6秒の入力を使い、歩き8コマ×80msの再生速度を維持する。上下版は各Camera画像の余白を縦に切り取って並べ、キャラクターの表示倍率を変えない。

[1.5倍の全画面版](previews/studio-idle-walk-speed-1p5x.mp4)／[2倍の全画面版](previews/studio-idle-walk-speed-2x.mp4)も保存する。いずれも1920×1080・60fps・無音。地面に接した基準歩行を事前に測り、既存の摩擦による減速を補正して、**実際の移動量**の比率を合わせる。設定値と実測値は各JSONに記録する。

当時の比較を再撮影するにはスタジオをPlayにして、以下を順に実行する。引数付きAPIは採用前の基準を保持しており、新しい標準へさらに1.5倍／2倍を掛ける処理ではない。標準版・通常画角版・アップ版を上書きしない。

```powershell
uloop execute-dynamic-code --code 'return VideoStudioReferenceCapture.Capture(1.5f);'
uloop execute-dynamic-code --code 'return VideoStudioReferenceCapture.Capture(2f);'
./Tools/Video/encode-idle-walk-comparison.ps1
```

## 従来の動画制作との関係

- `Assets/Scenes/CombatVideo.unity`：通常コンボ → 背後パリィ → 空中蹴り。
- `Assets/Scenes/CombatVideoDefenseCancel.unity`：攻撃キャンセル → 背後パリィ → 蹴り → Dash → 再攻撃。
- `Assets/Scenes/CombatVideoStudio.unity`：今回追加した自由操作と配置用のスタジオ。

固定テイクは「撮影」タブのリンクから開ける。[従来の撮影手順](capture-guide.md)と[追加シーン](additional-showcase-proposal.md)を引き続き利用する。スタジオ整備によって、元の動画の清書統合・完成判定を済ませたことにはしない。

主人公は既存のAsepriteラフと共通のVideoArtLibraryを使う。Jump/Fallは代用ポーズで、全24モーションや未制作アクションの作画を完了させるものではない。清書は引き続き[受け渡し手順](player-cleanup-handoff.md)で差し替える。
