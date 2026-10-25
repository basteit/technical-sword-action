# 反撃の予備動作を長く見せる調整（2026-10-18）

「最後のカウンターにもう少し緩急を付け、ゆっくり予備動作を見せたい」という指示への調整。受領したキックの4〜7コマを読む時間を増やし、足を伸ばしてから命中する速さは保つ。絵・色・輪郭は変更しない。

## 時間配分

提出移行12F、背後への移動10〜12F、キック4コマ目でスローへ入る18Fは維持する。以下は反撃開始からの60Hz戦闘tickであり、実時間の動画フレームとは異なる。

| 区間 | 前版 | 今回 | 意図 |
| --- | --- | --- | --- |
| キック4コマ目 | 2tick・約0.222秒 | 3tick・約0.333秒 | 足を畳む最初の姿勢を長く見せる |
| 5コマ目 | 1tick・約0.111秒 | 2tick・約0.222秒 | 体をひねる過程を読む |
| 6コマ目 | 1tick・約0.111秒 | 2tick・約0.222秒 | 最後まで動きのあるタメを保つ |
| 7コマ目 | 2tick・約0.222秒 | 1tick・約0.111秒 | 振り抜きへ向けて加速する |
| スロー全体 | 18〜24F・実測0.667秒 | 18〜26F・実測0.900秒 | 0.15倍を保ち、60fpsで14フレーム延長 |
| 解除→命中 | 24→25F・1tick | 26→27F・1tick | 伸ばした脚を即座に当てる |
| 命中→最終接地 | 25→38F・13tick | 27→40F・13tick | 振り抜き・着地を遅くしない |

キック単体のゲーム用配分は `[2,2,2,3,2,2,1,1,1,2,2,2,2,2,2]`、合計28F。ローカルのCharge=6 / Release=14 / Hit=15 / Landing=28。移行12Fを足して18 / 26 / 27 / 40となる。スロー要求の実時間上限は0.7→0.95秒へ延長し、26FのReleaseイベントで解放する。暗転・アップ・主人公の暗部補助も同じイベントに追従する。タメの理論時間は約0.889秒で、60fpsの撮影フレーム上では0.900秒となる。個別コマの表は理論値で、表示は戦闘時計と描画フレームの境界へ丸められる。

3つの保存Scene（CombatVideo、CombatVideoDefenseCancel、CombatVideoStudio）、共通生成設定、ArtLibrary、Counter Clip、Aseprite書き出しスクリプトを揃える。新しいSceneにも同じ設定を使う。本編の地上反撃・4段攻撃・プロトタイプ専用Clipは変更しない。

## 素材の保持

編集元は `ArtSource/Player/Current/Player_counter_kick.aseprite`、SHA256 `705817e748266c3c2f1071aa4c977aae4f1e84e5d87bac35f2ff306bf43339db`。原本の15コマ・1480ms、レイヤー、表示状態、Sample、保存履歴を保持した。原稿の追加更新ではないため新しい原稿版は登録しない。

Asepriteで別名レビューを再保存・再読込して元の表示時間を確認。出力PNG・EyeMasks計31枚が既存画像と全バイト一致することを確認し、ゲーム用時間表だけを取り込んだ。元の提出・Current・History・以前の動画は保持する。

## 再現

1. 保存済みの `Assets/Scenes/CombatVideo.unity` を開く。
2. 時間表から再生成するときは `Tools > Technical Sword Action > Video > Apply Submitted Attack and Heel Drop` を実行する。
3. PlayModeで `VideoCaptureUtility.CapturePreview(2,true)` を実行する。
4. `Tools/Video/encode-captures.ps1 -Revision counter-anticipation -Variants 2stage-hybrid` で無音MP4へ書き出す。

## 検証・成果物

実装コミット：`c6bdf3e`。基点は `4c012be`、作業フォルダとブランチは従来どおり。元Sceneを `PrototypeCombatValidation` のEditModeへ戻し、未保存変更がないことを確認した。

- [通しMP4](previews/combat-2stage-hybrid-counter-anticipation.mp4)：**259フレーム / 4.316667秒、1920×1080・60fps・無音**。全259フレームをデコードし、H.264・音声stream 0を確認。前版245Fから14F延長した。
- [イベント表](previews/combat-2stage-hybrid-counter-anticipation.json)：CounterStart 2.250秒 → ChargeStart 2.533秒 → Release 3.433秒 → Kick 3.450秒 → Landing 3.800秒。Parry/Kickは同じEnemy_B。タメ開始までのイベント・位置・対象・実時間は前版と一致し、解除→命中1tick・命中→接地13tickも保持した。
- [姿勢の確認画像](previews/counter-anticipation-poses.png) / [検証JSON](previews/counter-anticipation-validation.json)。実Game Viewでもタメ5コマ目（elapsed=21 / slow=.15 / dim=.965）と命中9コマ目（elapsed=27 / slow=1 / hitstop / 敵への命中1回）を確認した。
- Unity compile Error 0 / Warning 0、**EditMode196/196・PlayMode387/387 Passed**。撮影統合150件を含み、予備動作4姿勢の表示時間・最後の加速・スロー要求の継続/解除を30/60/120fps×両向きの6件で追加検証した。Pause・対象消失・中断・無敵/重力/時間/カメラ復帰の回帰も合格。
- 通常/攻防切替それぞれ2/4段×両向き×10回＝**計80回リセット**。二重ダメージ/別対象/操作不能/時間・カメラ・暗転・AI・VFX・姿勢等の残留なし。通常2段4.316667秒・4段5.716667秒、攻防切替2段4.733333秒・4段5.766667秒、すべて3〜6秒内。
- 収録後Console Error 0 / Warning 0。元の原稿・PNG・過去動画・着手時の未コミット変更を含む692ファイルのSHA256を保持。既存Controllerの状態IDと配置も保持し、カウンターClipの時間だけを更新した。

## 残作業

今回も提出Sampleを使う動作確認版。キックの人物清書・目、足の青白い熱・電流・軌跡、清書に合わせた最終接触/VFX・動画全体の受入は残る。#69/#72/#74/#76は未完了を維持する。今回の時間調整を清書の完了として扱わない。
