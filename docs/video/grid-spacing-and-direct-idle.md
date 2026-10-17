# 背景1マス分の間隔と、攻撃から待機への直接復帰（2026-10-17）

通常の2体撃破動画で、敵2体を前回からそれぞれ背景の小さい1マス分、中央から外へ離した。背景は16px間隔・32PPUなので1マスは0.5unit。敵Aはx=1.35→1.85、敵Bはx=-1.10→-1.60。保存Scene、リセット、再生成の配置を揃え、左右反転にも適用する。

離れた分は実際の移動でつなぐ。主人公は最初の18Fの待機後、標準歩行で敵Aとの間隔1.35unitまで進んで斬る。敵Bは敵A撃破後に通常の追跡AIで接近し、射程1.35unitから予備動作を開始する。判定範囲やダメージを広げず、通常の入力要求・移動・Animation Event・敵AI・パリィ判定を使う。移動Clipも提供済み `WalkReference` を優先し、待機の `IdleReference` と共に元素材をそのまま使う。

最新指示に従い、撮影/Studioの `VideoPlayerAnimationDriver.SkipAttackRecovery` を有効にした。斬撃1〜4段とかかと落とし後の自由復帰アニメは挟まず、行動終了後に提供済み待機へ直接切り替える。かかと落とし本体の着地と行動/無敵終了は維持する。攻撃の判定・硬直・コンボ接続を短縮する設定ではない。パリィ・移動・通常着地・ダッシュの復帰は従来通り。

復帰用の原画・Clip・全入力キャンセルの仕組みは保持した。将来比較する場合は同プロパティをfalseに戻せる。今回は復帰の追加作画を動画の前提にしない。

1→2段目の予約時29F接続と、2段目での敵A撃破を維持する。本編4段・任意4段撮影も保持する。攻撃キャンセル紹介用Sceneは従来配置を保ち、同じ攻撃復帰省略設定だけを反映する。Studioのユーザー保存セットや敵配置は上書きしない。

## 成果物と再現

- [更新MP4](previews/combat-2stage-hybrid-grid-spacing-direct-idle.mp4)
- [撮影イベント](previews/combat-2stage-hybrid-grid-spacing-direct-idle.json)
- [検証記録](previews/grid-spacing-direct-idle-validation.json)
- [通し画像](previews/grid-spacing-direct-idle-contact.png)

`HeelDropVideoSetup.Apply()` で3つの保存済み動画/Studio Sceneへ素材と設定を反映できる。通常の `CombatVideo.unity` のPlayModeで `VideoCaptureUtility.ValidateResetRuns()` → `VideoCaptureUtility.CapturePreview(2,true)` を実行し、`Tools/Video/encode-captures.ps1 -Revision grid-spacing-direct-idle -Variants 2stage-hybrid` で出力する。初期座標・移動接続の有効状態・復帰省略の有効状態を撮影JSONに記録する。

## 確認結果

実装・Scene・Move Clipは `d3b6742`（`issue/33-52-38-combat-polish`）。動画は**275F / 4.583333秒、1920×1080・60fps・音声なし**。最初の歩行は0.300〜0.433秒、1段目命中0.716667秒、2段目命中と敵A撃破1.183333秒。敵Bの接近開始1.700秒→予備動作2.000秒→背後パリィ2.483333秒。パリィと反撃は同じEnemy_B。スロー0.666667秒、解除→命中4戦闘F、命中→着地8戦闘Fを維持し、攻撃復帰の開始イベントは0件だった。

Unity compile/収録後Console Error 0 / Warning 0、**PlayMode336/336・EditMode186/186 Passed**。追加10件は斬撃4段それぞれの直接待機、2/4段・左右・30/60/120fpsを含む移動接続と着地後待機、歩行途中のReset、Pause/Resumeを確認する。復帰を再度有効にした場合の旧キャンセル/復元テストも維持した。

初回の追加テストで、120fpsの1描画は半戦闘Fしか進まない点と、Resumeした描画の入力抑制を確認した。待機表示は行動終了から1戦闘F以内で検証し、再開直後に抑制された攻撃要求を撮影側が消費せず次の描画で再試行するよう修正した。実際のPause/Resumeは描画を1回進めて確認している。

最終撮影Sceneで2/4段×左右×各10回、計**40回**のResetを再実行し、二重ダメージ・誤対象・操作不能・HP/位置/AI/無敵/時間/カメラ/VFXの残留なし。2段4.583333秒、4段5.733333秒でいずれも6秒以内。今回は指定通り2段の1本だけを書き出した。

MP4全275Fをデコードし、実動画の12場面とスローの全身/足/対象の画面内収まりを確認。チャットには同じ275F・1080p60・無音の軽量版（CRF18、元MP4はCRF16）を埋め込む。軽量版も全フレームをデコードし、埋め込みのバイト一致・外部リソース不要・1MB未満を確認した。再生UIの自動操作は実施していない。

元素材・既存PNG・過去動画・ユーザーの未コミット変更は、基準**1059ファイルのSHA256がすべて一致**。元の `PrototypeCombatValidation.unity` を未保存変更なし・EditModeへ戻した。実装11ファイルのRepository監査も成功した。

人のパリィ・かかと落とし等の素材と、最終の見やすさ/品質受入は残る。今回の配置・復帰調整と、#69の全体完成を区別する。
