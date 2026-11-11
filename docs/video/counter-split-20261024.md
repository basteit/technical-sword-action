# 後ろ髪と独立電撃エフェクトの統合（2026-10-24）

提出された人物128×128pxとエフェクト256×256pxを、それぞれ15コマのまま接続した。人物には新しいHair・Body・Eyeを使用し、人物原稿内に残っている旧Effectは出力から除く。独立原稿はEffect/Effect2だけを使い、位置確認用のBody/Eyeを二重表示しない。受領原本の画素・レイヤー・表示状態・表示時間は変更していない。

## 素材と位置

| 原稿ID | Current | SHA256 | 履歴 |
| --- | --- | --- | --- |
| counter_kick | ArtSource/Player/Current/Player_counter_kick.aseprite | 10061865424f45625e064e07cf0bca00ab188fce995263e12b6ba8ae807894dd | 4版、旧版も保持 |
| counter_kick_effect | ArtSource/Player/Current/Player_counter_kick_effect.aseprite | ba044fd315c892a65dfeae2e138d825407f04f4bb7b99b7ec8d994314be1ec1e | 初版 |

全15コマのBody参照が、人物の左上から横89px・縦43pxの位置差で完全一致した。人物Pivotは左下基準(32,38)、独立VFXは(121,123)、いずれも32PPU。256px原稿の中心をPivotにせず、この差を使って同じ姿勢・拡大率・左右反転へ重ねる。人物キャンバスは128pxのままなので、ゲーム側の身体サイズ・判定を大きくしない。

Unity画像は人物 `Assets/Art/Player/VideoSubmitted/CounterKick_01..15.png`、独立VFX `Assets/Art/Video/Submitted/20261024/CounterKickEffect_01..15.png`。既存の人物PNGのGUIDを維持し、本編の専用Snapshotにも同じ独立素材を追加した。確認用の人物を含む256px画像を、そのままキャラクターとして重ねてはいない。

## 時間と描画

人物原本は1〜10枚目100ms、11〜15枚目60ms。独立VFX原本は全100ms。ゲームでは1対1で同期するよう両方を `[2,1,1,1,1,1,1,1,1,4,4,4,4,4,3]` 戦闘tickへ接続する。原稿の保存時間と、スローを含むゲーム内の表示時間は別に記録した。

独立VFXも戦闘時計の同じコマを選ぶため、タメのスロー・Pause・命中8F停止で人物とずれない。描画順1009で暗転1000より前、浮かせる人物1010/瞳1013より後ろ。色は提出原色・不透明度1、追加の加算Glowなし。新VFXを使用するときは、従来の円形電撃衝撃波と自動生成の放電を出さない。パリィの火花・他の効果は従来のまま。

Inspectorの `VideoVfxRenderer / Use Authored Counter Effect` で旧命中VFXとの比較ができる。新VFXの濃さは `Authored Counter Opacity`。従来の `Intensity` は旧VFX用で、この提出素材には適用しない。`Hybrid Glow` を切っても今回の作画済み電撃は表示される。

敵の攻撃を見せる主2段テイクは前版と同じ **308F / 5.133333秒**。タメ0.900秒、20F解除→21F命中→45F接地、命中後0.533333秒、無敵/対象固定/吹っ飛びと全15コマを維持。前版と全イベント・対象・tick・実時間が一致する。

## 更新手順

1. 両原稿を素材ライブラリの同じIDへ `player_source_library.py register` で登録する。履歴を残し、Unity使用記録は動作確認後に更新する。
2. `python Tools/Art/export_counter_split.py` を実行する。AsepriteのLua出力で15コマの参照一致を検査し、他モーションを残して人物・瞳・VFXのPNGと時間表だけを更新する。途中出力は `.uloop/outputs/counter-split-export/` に保持する。参照位置やコマ数を変えた原稿は、そのまま通さず接続を見直す。
3. UnityのEditModeで `Tools > Technical Sword Action > Video > Apply Split Counter Artwork`。共通Libraryと本編専用Snapshot、必要なCounter Clipだけを反映する。Scene・既存Controller・本編の反撃設定全体を再生成しない。
4. コンパイル、左右の同期/停止/リセット、実画面を確認する。検証記録を作ってから両IDのvideo/prototype使用版を `record-use` へ記録する。

## 限定検証と成果物

- Unity compile Error/Warning 0。EditMode **3/3**：独立Snapshot/対応コマ時間・Pivot・Point設定、Clipと時計、保存Sceneの接続。
- PlayMode **16/16**：30/60/120fps×左右の位置・命中停止・Pause・Reset、4段比較の足先接点、旧VFXの再利用/純ドット切替、本編の左右同期・暗転前後・停止・Resetと旧命中VFX互換。全件テストや従来の80回Resetは今回再実行していない。
- 主2段を1回収録。H.264、1920×1080、60fps、音声stream0、全308Fデコード。タメ・命中・後半の連続画像で後ろ髪と広い電撃を確認。収録後Console全種0、元の `PrototypeCombatValidation.unity` へEditMode/未保存変更なしで復帰。
- 再出力ツールも実行し、既存の時間表・他の動作を保持して出力できた。

[MP4](previews/combat-2stage-hybrid-counter-split-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-counter-split-20261024.json) / [連続画像](previews/counter-split-20261024-contact.png) / [検証JSON](previews/counter-split-20261024-validation.json)。保存CRF16、チャット内は同解像度/fps/全コマのCRF24。

再録画は `CombatVideo.unity` のPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision counter-split-20261024 -Variants 2stage-hybrid`。

後ろ髪と提出電撃の統合は完了。動画全体の最終受入、他のVFXとの密度/接触の仕上げ確認は別に残す。#69/#72/#74/#76をこの素材反映だけで完了扱いにしない。
