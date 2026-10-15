# 攻撃1段目の仮提出・頭上かかと落とし（2026-10-16）

最新のユーザー指示を優先する。1段目は受領した作画を仮採用し、後ろ髪・復帰の追加を今回の動画の前提にしない。最後の反撃は、従来の全身回転蹴りから **弾いた敵の頭上へワープ → 同じ短いスロー → 解除時にかかとを振り下ろす → 落雷風の命中** へ変更する。

## 受領した1段目

- 原本のコピー：`Assets/Art/Player/VideoSubmitted/20261016/Player_attack_1.aseprite`。Downloadsの原本とバイト一致。
- 接続確認用：`Assets/Art/Player/VideoSubmitted/Player_attack_1_preview.aseprite`。Actionタグ `VerticalSlash` を追加し、黒い枠/床ガイドだけを非表示にした。元のBody/Sword/Effectの画素、Hairの非表示、9コマ×70msは保持する。
- 128×128px、32PPU。64px基準セルが左下にあるため、Pivotは下端から **(32,38)px**。絵を縮小・拡大・切り抜きせず、足裏を従来の床へ合わせる。大きな斬撃も切らない。
- 専用Exporter：`Tools/Art/export-player-submission.lua`。通常の完成素材Exporterとは別に、仮提出の状態を記録する。`VideoSubmitted/timing-manifest.json` に載ったActionだけを置き換え、将来のVideoCleanが優先される。
- Unityでは70msの累積時刻を60Hzへ丸め、38F / 0.633秒。各コマを一律4Fへ切り縮めない。Asepriteの原再生時間は0.630秒。命中は5コマ目の開始17F、次段は9コマ目の開始34Fから接続する。
- 提出絵に斬撃が描かれているため、この1段目には従来の汎用Slash軌跡を重ねない。実ダメージ、軽いHitstop、揺れ、前進/敵ヒットバックは維持する。

| コマ | 元の時間 | Unity開始F | 表示F | 役割 |
|---|---:|---:|---:|---|
| 1 | 70ms | 0 | 4 | 構え |
| 2 | 70ms | 4 | 4 | 構えの保持 |
| 3 | 70ms | 8 | 5 | 刀を引く |
| 4 | 70ms | 13 | 4 | 振りかぶり/軌跡の出始め |
| 5 | 70ms | 17 | 4 | 大きな振り下ろし、実命中 |
| 6 | 70ms | 21 | 4 | 振り抜き/斬撃の減衰 |
| 7 | 70ms | 25 | 4 | 低い終端、斬撃が薄れる |
| 8 | 70ms | 29 | 5 | 軌跡の消失 |
| 9 | 70ms | 34 | 4 | 終端、次の横斬りへの接続点 |

原本にはActionタグがなかったが、タグ不足を理由に受領を止めず確認用コピーへ追加した。復帰の作画は後回し。通常の単発操作では既存の自由復帰ラフを使えるが、今回の通しでは1段目から2段目へつなぐ。

## 新しい反撃

編集用：`Assets/Art/Player/VideoHeelDrop/Player_heel_drop_rough.aseprite`（26コマ・2タグ・8レイヤー）。参照/ガイド、ラフ本体、軌道、清書用、瞳マスクを分離する。元の119コマのラフと待機/歩きは保持する。

既存Clip/復帰/部分清書との接続を保つため、内部Action名は `AirRoundhouse` / `Recovery_AirRoundhouse` を継続する。**この撮影設定での見た目はかかと落とし**。タグ名を理由に旧回転蹴りを清書しない。

| 戦闘F | 動作/演出 |
|---|---|
| 0–2 | 成功した敵の頭上へ移動。体を隠し、元位置/移動経路の黒い残像と影を残す。障害物をすり抜けない |
| 2–18 | 頭上で位置を保持。全身の予備動作を見せない |
| 18–24 | 高く上げた片脚を見せ、0.15倍・実時間約0.667秒のスロー。強い暗転、主人公の持ち上げ、瞳/残光、アップを継続 |
| 24–28 | スロー解除。4F / 約0.067秒で脚を上→前→下へ振り抜き、体も短く下降 |
| 28 | 頭への接触、1回の実ダメージ、落雷/地面の衝撃波、下向きの強い揺れ、8FのHitstop |
| 28–36 | 8戦闘Fで速く着地。敵は低く外へ吹き飛び、消滅する |
| 36以降 | 行動/無敵を解放。片手着地から立ち姿へ戻る既存33Fの自由復帰。すべての行動でキャンセル可能 |

頭上位置は開始時の実対象を基準に固定する。敵ののけぞった頭は胴体の中心から離れるため、足の着点を反撃方向へ7px寄せる。主人公中心は敵の中心から横3px手前/高さ1.35unit、接触時は高さ1.15unit。足の接触キーは64pxキャンバス内(42,48)、Pivotは上端基準(32,26)。別対象へ追従/乗り換えない。天井/壁で到達できなければ安全中断し、表示、対象拘束、重力、無敵、時間演出を解除する。

`Assets/Art/Video/HeelDropEffects.aseprite` は落雷の核/枝、空中の小さな接触、床の横長衝撃波、破片を分けた8コマのドット素材。採用済みの短い発光を補助に使い、純ドットにも切り替えられる。命中停止中は敵を接触直前の姿勢に保ち、頭が先に下がって接触が離れて見える問題を防ぐ。

## 再現

既存3Scene（CombatVideo / CombatVideoDefenseCancel / CombatVideoStudio）の配置を保持して設定を更新した。保存済み動画Sceneで Tools → Technical Sword Action → Video → Apply Submitted Attack and Heel Drop を実行すると、素材/Clipと3Sceneの設定を再接続できる。未保存Sceneがある場合は上書きしない。

録画は従来通り `VideoCaptureUtility.CapturePreview(2,true)` / `(4,true)`、純ドット比較は `(4,false)`。追加の防御キャンセルSceneは `CaptureDefenseCancelPreview` を使う。`events.json` に `counterAction=OverheadHeelDrop` と `provisionalActions=[VerticalSlash]` を記録し、仮提出の動画を完成版と区別する。標準歩行4.4019・元の待機/歩きテンポは維持する。

## 完成状態

1段目は仮提出の実機確認。後ろ髪・復帰が省かれていることを理由に今回の動画を止めないが、全主人公アクションの清書完了や親Issue #69の完成とは扱わない。残る清書、かかと落としの最終作画、清書後の同期/構図/VFXの最終受入は継続する。

## 今回の出力と検証

| 動画 | 実時間（スロー/停止込み） | フレーム |
|---|---:|---:|
| [2段・提出1段目＋かかと落とし](previews/combat-2stage-hybrid-heel-drop-submitted-attack1.mp4) | 4.100秒 | 246 |
| [4段・提出1段目＋かかと落とし](previews/combat-4stage-hybrid-heel-drop-submitted-attack1.mp4) | 5.117秒 | 307 |
| [4段・純ドット比較](previews/combat-4stage-pixel-heel-drop-submitted-attack1.mp4) | 5.117秒 | 307 |
| [攻防切替・2段締め](previews/combat-defense-cancel-2stage-hybrid-heel-drop-submitted-attack1.mp4) | 4.067秒 | 244 |
| [攻防切替・4段締め](previews/combat-defense-cancel-4stage-hybrid-heel-drop-submitted-attack1.mp4) | 5.033秒 | 302 |

すべて1920×1080・60fps・無音。全フレームをデコードし、音声Stream 0、尺、個体ID、スロー実時間0.666667秒、解除から命中4F、命中から着地8戦闘Fを確認した。過去の戦闘候補5本と採用した標準歩行の動画は上書きせず、バイト一致を確認した。

Unity compile/収録後ConsoleはError 0 / Warning 0。最終接点調整後にEditMode187/187・PlayMode196/196 Passed。新規11ケースで30/60/120fps×左右の頭上到達/位置保持/頭への命中、天井中断、敵の接触姿勢の保持、提出絵のサイズ/累積タイミングを確認。既存の対象消失/壁/Disable/死亡/無敵/復帰キャンセル/時間制御/スタジオの回帰も通過した。

最終素材で通常Sceneと攻防切替Sceneのそれぞれ2段/4段×通常/反転×各10回、**各40回・計80回**を再実行。二重ダメージ、別対象への反撃、操作不能、時間/カメラ/AI/VFX/重力/表示の残留なし。提出原本のSHA256は `D994E5FDBA1D091018C8BA1B2E4029068AA2A1F6351485AADE2124BE80BF2650`。9枚の画素比較では削除した黒いガイド2119画素以外は一致し、元idle/walkと119コマのラフも保持した。

[検証記録](previews/heel-drop-submission-validation.json) / [通しの連続画像](previews/heel-drop-submitted-attack1-contact.png) / [新しい蹴りのラフ](rough-previews/HeelDrop-slow.gif) / [提出1段目の原テンポ](rough-previews/SubmittedAttack1.gif)。最終的にUnityは標準速度4.4019のStudioをEditModeで開き、未保存Sceneなしの状態へ戻した。

連番収録後は `Tools/Video/encode-captures.ps1 -Revision heel-drop-submitted-attack1` と `Tools/Video/encode-showcase.ps1 -Revision heel-drop-submitted-attack1` でこの版へ書き出す。Revisionを省略すると従来の既定ファイル名を使う。
