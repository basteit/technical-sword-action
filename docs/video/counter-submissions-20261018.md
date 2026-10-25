# 反撃の移行・キック仮素材の接続（2026-10-18）

後続の指示により、予備動作のタメを0.900秒へ延長した。[最新の時間表・4.316667秒の動画・検証](counter-anticipation-20261018.md)。以下は導入時の素材確認と前版の時間表であり、原稿と画像は現在も同じものを使用する。

ユーザー提出の `Player_counter_transfer.aseprite` と `Player_counter_kick.aseprite` を接続する。最新指示は「弾いた敵の背後へ回り込み、蹴り命中後は大きくノックバック」。キックは今回の表示中の Sample を使う。旧かかと落としの絵・接触位置をそのまま当てない。

## 受領・原本保持

| 素材 | 原本のコマ・時間 | 取り込み | ゲーム用 |
| --- | --- | --- | --- |
| 移行 | 64×64、8×100ms＝800ms | Body/Effect/Eye の表示を保持。Sample は原本どおり不透明度0 | 12F（1,1,2,2,2,1,1,2） |
| キック | 128×128、15コマ＝1480ms | Sample のシルエットを使用。非表示の Body/Sword/Hair は非表示のまま | 26F（2,2,2,2,1,1,2,1,1,2,2,2,2,2,2） |

原本は `Assets/Art/Player/VideoSubmitted/20261018/Player_counter_{transfer,kick}.aseprite`。Downloads の原本と保存先の SHA256 を照合した。

- transfer: `299dc68428aa7653ea56bfad548cbde0f737bbd5a36f10afe964b93261a41b2b`
- kick: `705817e748266c3c2f1071aa4c977aae4f1e84e5d87bac35f2ff306bf43339db`

`Tools/Art/export-counter-submissions.lua` を Aseprite バッチで実行する。プレビュー用の別名ファイルに Action タグを追加し、保存・再読込と元の表示時間を確認する。ガイドレイヤーの黒線を非表示にし、キック1〜7コマの離れた参照点 (91,44) のみを除去する。原本への保存、描かれた体の変更、キャンバスの縮小は行わない。

編集可能な確認用は同じ日付フォルダの `Player_counter_transfer_preview.aseprite` と `Player_counter_kick_preview.aseprite`。Unity PNG と EyeMasks は `VideoSubmitted/`。32PPU、Point、無圧縮、Pivot(32,38) を共通にする。キックのガイド床 y=122 は従来の64px素材の y=58 に64px足した位置なので、128pxでも Pivot の下原点38を変えない。

## 時間・対象・位置

以下は反撃開始からの60Hz戦闘tick。スローとヒットストップ中は絵と物理の戦闘tickも止まる。

| 時刻 | 絵・処理 |
| --- | --- |
| 0〜10F | 提出移行の沈み込み・暗い姿。主人公のroot位置を保つ |
| 10〜12F | 実際に弾いた敵の背後へ2tickで移動。黒い影とEyeレイヤーの残光を残す |
| 12F | 提出キックの1コマ目へ。敵に向き直る。敵はひるみ中の元の向きを保つ |
| 18F | キック4コマ目、足を伸ばす前に0.15倍スロー・アップ・暗転 |
| 24F | スロー解除、8コマ目の大きく伸ばした脚へ |
| 25F | 9コマ目のつま先 (73,86) が敵の頭へ命中。単一ダメージ、8F停止、衝撃波と横方向の強い揺れ |
| 25〜38F | 提出の振り抜き〜接地姿勢。敵は攻撃方向へ3.2unit・高さ0.65unit・22tickで吹っ飛ぶ |
| 38F | 最終接地姿勢で反撃・無敵・重力上書きを解放。動画設定では待機へ直接戻る |

スロー区間は6戦闘tick、実時間約0.667秒。足先のローカル座標は (41,4)/32。敵の攻撃方向を開始時に保存し、その背後1.5unitへ回り込んでから敵へ向く。移動開始時のパリィ方向とキック方向を混同しない。

敵がひるみ中に移動先を追って振り向く処理を止める。敵の被弾・死亡・ヒットバック、カウンターの対象固定、1押下1消費、他対象ストック、接触/壁判定、全中断とリセットを維持する。本編の地上反撃や4段攻撃はこの動画用設定で置換しない。

## 暗転・作画について

移行の Effect は体の上に黒を段階的に重ねる構成で、Eye はその上。構成はUnityの暗転と併用できる。原本のEyeレイヤー不透明度は128（約50%）なので、原画だけでは残光が弱め。Unityはそのレイヤーから目のマスクを作り、移動時の短い発光を補助する。清書でもEyeを別レイヤーのまま、明るい芯と薄い尾に分けると調整しやすい。

キックのSampleは濃紺一色で目も脚の発光も未作画。そのまま黒背景へ置くと埋もれるため、暗転中だけ被写体の暗部を持ち上げるUnityの表示補助を追加した。元の色・アルファ・輪郭を変更せず、拡大やぼかしで絵を代用しない。キックの目の発光は素材に目が入ってから接続する。

足の青白い熱、脚に沿う電流、回転方向を示す短い軌跡は清書受領後に同期を詰める。今回の命中は既存の大きな衝撃波を使い、動き確認用のシルエットに電気の完成作画を加えた扱いにはしない。旧かかと落とし用の落雷素材は保持する。主人公清書・電気の最終品質・#69の完成受入は残る。

## 再現・検証・成果物

検証対象の実装コミット：`02283a45ef63623942854561d5f2aff02d2a6100`。

- `docs/video/previews/combat-2stage-hybrid-submitted-rear-counter.mp4`：245フレーム、**4.083333秒**、1920×1080、60fps、無音。スローと停止を含む実時間。FFmpegで全245フレームをデコードし、音声stream 0を確認。
- 同名 `.json`：対象ID、戦闘tickと実時間。Parry 2.050秒 → CounterStart 2.250秒 → ChargeStart 2.533秒 → ChargeRelease 3.200秒 → Kick 3.217秒 → Landing 3.567秒。Parry/KickはいずれもEnemy_B。
- Compile Error 0 / Warning 0、EditMode **186/186**、PlayMode **375/375** Passed。撮影統合144件を含む。別チャットの攻撃イベント・すり抜け足場修正も同じ状態で回帰確認した。
- 通常Sceneと攻防切替Scene、それぞれ2/4段×通常/左右反転×10回＝各40回、**計80回**を別途保存。二重ダメージ/死亡、別対象、操作/無敵/重力/時間倍率、カメラ/暗転/明るさ/残光、敵AI/回転/色/Collider、VFX、位置/HPリセットの残留なし。
- 通常Sceneの実時間は2段4.083333秒 / 4段5.483333秒、タメ0.666667秒。主動画は最新指示に合わせた2段版。既存本編4段と比較設定は維持。
- Game Viewのタメ/命中を確認し、収録後Console Error 0 / Warning 0。

結果のまとめは `docs/video/previews/submitted-rear-counter-validation.json`。最初の検証で、Vector2用の設定処理が無く足先オフセットが旧値に残る問題を検出し、専用setterを追加した。また自由復帰の終了tickで撮影だけが先に終わる場合を修正し、実際にIdleが見えるまで終了を待つ。提出ラフへの更新で旧ポーズを前提とするテストも更新し、最終の全体検証を通した。

対象Sceneは `CombatVideo.unity`、`CombatVideoDefenseCancel.unity`、`CombatVideoStudio.unity`。保存済みの動画Sceneを開き、`Tools > Technical Sword Action > Video > Apply Submitted Attack and Heel Drop`（既存メニュー名）で共通設定を適用する。PlayModeから `VideoCaptureUtility.CapturePreview(2,true)`、続けて `Tools/Video/encode-captures.ps1 -Revision submitted-rear-counter -Variants 2stage-hybrid` で書き出す。

別チャットが保存した `PrototypeCombatValidation.unity` には新しい2項目の無効値（blinkStartFrame=0、behindTarget=false）だけが追加される。通常の地上反撃へ背後ワープを適用しない。その他の元からのScene/設定/素材/要件草案の未コミット変更を保持する。
