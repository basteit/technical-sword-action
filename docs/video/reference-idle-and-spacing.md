# 既存待機の採用と敵の間隔（2026-10-17）

ユーザーの追加指示に合わせ、通常の2体撃破動画の敵を中央からそれぞれ0.25unit（原画8px）離した。敵Aはx=1.10→1.35、敵Bはx=-0.85→-1.10。配置とリセット位置を同時に更新し、撮影Sceneの再生成・素材再適用でも同じ位置になる。左右反転時は主人公x=0を中心に反転する。

主人公の待機Clip `PlayerVideo_Idle` は、提供済み `Assets/Art/Player/Player_idle.aseprite` の `IdleReference_01`〜`06` を優先する。6コマ×100ms、計0.6秒ループ、64×64px/32PPU/Pivot(32,38)。Asepriteで原本を再出力し、既存の6枚と全画素の一致を確認した。元ファイルと画像を加工せず、そのまま再利用している。

今回も1→2段目は予約時29F接続、敵Aは2段目で撃破する。背後パリィから同じ敵Bへの頭上ワープ・スロー・かかと落としまで収録する。本編4段攻撃と比較動画を保持する。

## 成果物と再現

- [更新MP4](previews/combat-2stage-hybrid-reference-idle-spacing.mp4)
- [撮影イベント](previews/combat-2stage-hybrid-reference-idle-spacing.json)
- [検証記録](previews/reference-idle-spacing-validation.json)
- [動画の連続画像](previews/reference-idle-spacing-contact.png)

保存済み撮影Sceneから `HeelDropVideoSetup.Apply()` で待機Clipと配置を反映。`CombatVideo.unity` のPlayModeで `VideoCaptureUtility.ValidateResetRuns()` → `VideoCaptureUtility.CapturePreview(2,true)` を実行し、`Tools/Video/encode-captures.ps1 -Revision reference-idle-spacing -Variants 2stage-hybrid` で書き出す。チャットには動画の再生プレビューを提示する。

## 確認結果

実装・Scene・Clipは `9581af3`（`issue/33-52-38-combat-polish`）。新しい動画は**249F / 4.15秒、1920×1080・60fps・無音**。全249フレームをデコードし、開始時/終了時の提供待機、広げた配置、斬撃接触、背後パリィと同じ個体への反撃を通し画像で確認した。前回の5F早い接続版とイベントの対象・tick・実時間はすべて一致した。

Unity compile/収録後Console Error 0 / Warning 0、PlayMode **326/326**、EditMode **186/186** Passed。追加した左右2ケースで敵の初期位置と待機6コマのループ・サイズ・Pivot・PPUを確認。最終撮影Sceneの2/4段×左右×各10回、計**40回**のリセットも通過した。新配置で旧テストの固定壁x=.55がワープ1F後に背後へ回るため、壁を現在位置と対象の中間へ置くよう修正し、開始前/途中の遮蔽とHP・無敵・時間復帰を再確認した。

原素材・既存PNG・過去動画・ユーザーの未コミット変更は基準696ファイルのSHA256が一致。収録後は元の `PrototypeCombatValidation.unity` をEditMode・未保存変更なしへ戻した。チャットのプレビューには同じMP4を埋め込み、外部配信への依存なしで提示する。

待機は受領済み素材を採用したが、パリィ・かかと落とし等には従来ラフが残る。#69の全体完成・清書受入とは区別する。
