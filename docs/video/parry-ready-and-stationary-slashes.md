# 提出パリィ待機と、その場で振る2段斬撃（2026-10-17）

2026-10-18追記：成功（軽）も仮受領し、[新しい素材と最新動画](parry-light-submission-and-effects.md)へ接続した。以下は待機を受領した時点の記録。

最新の指示に従い、`Player_parry_wait.aseprite` を撮影/Studioのパリィ待機へ接続した。今回の2段動画は主人公の接近歩行を使わず、提出1・2段目も前進0で振る。敵の初期位置は背景1マス追加後のA x=1.85/B x=-1.60を維持する。2段目撃破、攻撃後の直接待機、同じ背後の敵へのかかと落としをつなぐ。

## 受領素材

- 原本の保存先: `Assets/Art/Player/VideoSubmitted/20261017/Player_parry_wait.aseprite`。Downloads原本とバイト一致、SHA256 `01adf3b9dfcc81dff09749a7362b77971ddef73afcd5077468526f5327411d1d`。
- 64×64px、8枚×100ms、32PPU、Pivot(32,38)。表示中のBody/Effectと、非表示のHair/Sampleを保持する。黒だけのガイドを出力時に隠す。
- `Player_parry_wait_preview.aseprite` は同じ日付フォルダの別名コピー。ガイド非表示と `ParryReady` タグだけを加え、原画の100msは保持。Aseprite本体で保存・再読込済み。
- ゲーム用Clipは `[2,2,6,6,6,6,6,6]` 戦闘Fへ割り当てる。最初の2姿勢を各2Fでつなぎ、4Fに構えの3枚目へ到達する。待機の全8枚を必ず再生してから成功させる仕様にはしない。成功/失敗のゲーム判定で即座に分岐する。
- 入力受付12F、軽/重属性、反撃の内容は維持する。通常の撮影では入力を接触9F前へ早め、構えを数F見せてから弾く。防御キャンセル紹介の入力タイミングは従来通り。

## 斬撃の届く範囲

提出1・2段目の命中コマの `Effect` レイヤーから、不透明な画素の範囲をAsepriteで取得した。

| 段 | Effectの画素範囲（右/下は含まない） | 前方先端 | 上下範囲（主人公原点基準） |
| --- | --- | --- | --- |
| 縦斬り | x50〜106 / y37〜128 | 2.3125unit | -1.1875〜1.65625unit |
| 返し斬り | x7〜117 / y85〜124 | 2.65625unit | -1.0625〜0.15625unit |

握りから斬撃先端までを前方矩形の判定にする。弧を包む矩形であり、1ドット単位の形状判定ではない。射程を円の半径で増やして背後や上下まで一様に広げる方式を避け、向きに応じて前方矩形だけを反転する。1段・同一個体への1Hitと既存Animation Eventを維持し、敵のヒットバックは残す。通常の本編AttackDefinitionと、矩形未設定の既存攻撃は従来の円判定を使う。

新しい射程と前進0は撮影/Studioの提出1・2段目の設定。3・4段目の本編基盤は保持し、任意4段の比較撮影には従来の接近とラフ段の前進を残す。今回出力するのは、その場で振る2段版。

以前の攻撃キャンセル紹介用 `CombatVideoDefenseCancel.unity` は、着地ダッシュからの再攻撃が従来の踏み込みに依存するため、そのSceneだけ元の1・2段目の移動量も維持する。今回の通常2段動画とは設定を分ける。

## 再現

パリィ出力は `Tools/Art/export-parry-ready-submission.lua` をAsepriteのバッチで実行する。入力・出力を指定し、出力フォルダと `EyeMasks/` を先に用意する。出力manifestを既存 `VideoSubmitted` へAction名で統合する。攻撃用 `export-player-submission.lua` も同じEffect範囲を再取得する。

保存済み動画Sceneから `HeelDropVideoSetup.Apply()` を実行し、通常の `CombatVideo.unity` をPlayModeへ。`VideoCaptureUtility.ValidateResetRuns()` → `VideoCaptureUtility.CapturePreview(2,true)` → `Tools/Video/encode-captures.ps1 -Revision parry-ready-stationary -Variants 2stage-hybrid`。

## 成果物

- [MP4](previews/combat-2stage-hybrid-parry-ready-stationary.mp4)
- [撮影イベント](previews/combat-2stage-hybrid-parry-ready-stationary.json)
- [検証記録](previews/parry-ready-stationary-validation.json)
- [通し画像](previews/parry-ready-stationary-contact.png)

## 確認結果

実装/素材/保存Sceneは `138125c`。最新MP4は **248F / 4.133333秒、1920×1080・60fps・音声なし**。1段目命中0.583333秒、2段目命中と敵A撃破1.050000秒。パリィ待機1.916667秒→成功2.050000秒、反撃開始2.250000秒。8戦闘Fの待機中に新しい構えが見える。スロー0.666667秒、解除→命中4F、命中→着地8Fを維持する。接近と攻撃復帰のイベントは0件。

Unity compile/収録後Console Error 0 / Warning 0、**PlayMode352/352・EditMode186/186 Passed**。追加16ケースで斬撃先端の内側/外側・両方向・2段分、背後への誤命中防止、根元位置の固定、30/60/120fps×両方向でのその場の完走、パリィ待機の4F構え/サイズ/接地/受付時間を確認。初回の全体検証で攻撃キャンセル紹介の4段目までつながらない問題が見つかったため、同Sceneの旧踏み込みを保持して全件を再確認した。

最終撮影Sceneでも2/4段×左右×各10回、計**40回**のResetを実行。二重命中・別対象反撃・操作不能・時間/カメラ/AI/VFX等の残留なし。2段4.133333秒、4段比較5.533333秒で完走した。既存の攻撃キャンセル紹介の2/4段・左右・40回も全テスト内で通過。

全248Fをデコードし、実動画12場面を確認。チャットには同一MP4のバイトを直接埋め込み、最初のフレームのポスターと再生操作を付ける。外部リソースなし・1MB未満・埋め込み一致を検査した。再生UIの自動操作は実施していない。

Asepriteでの新しい待機の再読込、全8枚の瞳マスク、元ファイルの非変更、既存攻撃Exporterで2つの射程manifestと17枚すべてのPNGを再現できることを確認。保護した元素材/既存PNG/過去動画/ユーザー差分は**1061ファイルのSHA256が一致**した。起動時にPrototypeへ追加された未使用の既定フィールド12行は退避後に内容を確認し、元ファイルのバイト一致を維持した。終了時は `CombatVideo.unity` のEditMode・未保存変更なし。

実装56ファイルのRepository index監査に成功。前回の公開済み2コミットの規約名の未達は[前回記録](grid-spacing-and-direct-idle.md)へ明記済みで、今回の実装はscopeとIssue参照を付けた規約名でコミットする。

待機は今回受領した素材。成功（軽）と頭上かかと落とし等には従来ラフが残る。成功素材の受領後の接続・見やすさ調整・最終検証/書き出し・全体受入は未完了。#69の全体完成とは区別する。
