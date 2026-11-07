# 既知PlayMode 13失敗の診断（2026-11-07）

対象は前回の全PlayMode 940件中の13失敗。13種類の不具合ではなく、4つの検証内容を左右/FPS等で展開した件数である。今回のジャンプ姿勢変更では動画設定・テスト期待値・パリィ境界を修正していない。

初期報告の「動画キックの揺れ2件」「停止9F/旧期待8Fの差3件」は不正確だった。失敗行を再照合し、以下の「敵の跳ね上がり」「タメ開始→解除」に訂正する。

| 件数 | テストの内容 | 期待と実測 | 確認した原因 |
| --- | --- | --- | --- |
| 2 | KickKnocksBackOnlyItsTargetAndPauseFreezesTheFixedPose、左右 | 命中から16描画F後に高さ0.21〜0.27を期待、実際0 | 現行の動画キックは16F Hitstop。16回進めた直後は停止が解けただけで吹っ飛びは0F。古い短い停止を前提としたサンプル時刻 |
| 3 | ReleasePrecedesImpactAndFollowThroughReachesGroundAtEveryRenderRate、30/60/120fps | タメ開始→解除8tickを期待、実際9tick | 両動画SceneはchargeFrame=12、chargeReleaseFrame=21。現行素材/設定に合わせた9Fに対し、テストだけ旧8Fのまま。Hitstopの誤差ではない |
| 7 | 防御キャンセル4段動画、左右×3fpsの6件＋40回リセット検証の1件 | 6秒以内にCompletedを期待、実際6秒時点ではfalse | 4段動画の最終撃破後の消滅待ちが6秒を超える。6.25〜6.266667秒まで進めると6条件とも正常終了。2段版は旧枠内 |
| 1 | SuccessfulParryKeepsHeldMovementStoppedOnlyForItsExistingLock、5F/Just | 5F接触をJustと期待、実際Normal | プロトタイプのParryTimingは受付0〜13F、Just0〜3F。テストの5F/Justだけ旧境界のまま |

## 実行による根拠

- 関連PlayMode 69件: 56合格/13失敗、Skip0。新しいジャンプ31/31・素材統合7/7は合格。前回全体XMLとの比較で失敗名・Assertion文面が全13件一致。今回の結果は`.uloop/outputs/TestResults/20261107_080825.xml`（生成物のためGit対象外）。
- CombatVideoの実行で、吹っ飛び開始時はHitstop=16・hitbackFrame=0・高さ設定0.27。16描画F後はHitstop=0・hitbackFrame=0・変位(0,0)。さらに9F進めるとhitbackFrame=9・変位(2.80,0.27)で、跳ね上がり自体は動作していた。
- 同じ実行のChargeStart→ChargeReleaseは9tick・実時間1.000000秒。設定12→21と一致した。
- CombatVideoDefenseCancelの4段版を12秒の診断枠で実行。30/60fpsは左右とも6.266667秒、120fpsは左右とも6.250000秒でCompleted=true。全6条件で前敵5Hit、後敵1Hit、各死亡1回、プレイヤーNeutral。
- 6秒時点では、4段目はすでに命中して前敵も撃破済み、プレイヤーIdle/Neutral、復帰なし、VFX0。前敵がまだactiveのため終了待ちだった。前敵が3段目で早死していることが原因ではない。Death通知がSlash4通知より先に記録されるのは、ダメージ適用中のDiedがHitConfirmedより先に発火するため。
- Justの判定はPlayerParry2DがElapsedFrames<=timing.justEndFrameを使い、PrototypeCombat/ParryTiming.assetのjustEndFrame=3を参照する。5FでNormalは現行仕様どおり。

## 影響と修正方針（診断時点）

- この13失敗から、現在の本編プロトタイプで吹っ飛び/反撃/パリィが13箇所壊れているとは判断しない。12件は撮影専用Scene/ユーティリティ、1件は本編用テストの旧期待値。ただし、全件合格として放置もしない。
- 2件: Hitstop終了を待ってから吹っ飛びの戦闘tickを数える。描画Fと戦闘tickを混同しない。
- 3件: 現行の9Fタメを採るなら期待値を更新し、設定値とイベント差の一致を検証する。8Fへ戻す場合は動画演出側の別判断が必要で、本編設定をテスト都合で変えない。
- 7件: 現行尺を採るなら受入時間枠とタイムアウトを更新する。従来の3〜6秒を厳守するなら、動画用の待ち/構成だけを約0.27秒以上詰める必要がある。単に上限を伸ばして「3〜6秒達成」とはしない。
- 1件: Justの代表ケースを0〜3F内へ直し、3F/4F境界と5FのNormalを別に検証する。現行のパリィ受付を古いテストへ合わせて変更しない。

## 後続のユーザー指示によるテスト整理

この診断後に「現状のテストを必要なものへ絞って整理」と指示を受けたため、6件の古い期待値/計測時点を上記方針で修正した。Justは3F/Just、4F・5F・9F/Normalとし、境界確認を増やした。動画の演出設定・Scene・ゲーム本体の受付や威力は変更していない。

旧3〜6秒の受入7件は`video-acceptance`へ分離し、`video`の通常回帰からは除外した。`all`では引き続き実行し、未達を隠さない。標準daily287/287と、補正後のvideo267/267が合格。最終`all`は949件中942合格・7失敗・Skip0で、残った失敗はこの旧動画受入7件だけ。新しい失敗はない。分類と実行条件は[テスト実行方針](../test-policy.md)を参照する。

その後、動画制作の完了に伴い、旧3〜6秒受入を含む撮影専用テストを廃止した。上記の件数・実行手順は廃止前の記録であり、ゲーム設定を変えて旧受入を満たしたことを意味しない。

参照コード: 廃止前の `CombatVideoIntegrationTests` と `DefenseCancelShowcaseTests` はコミット `824f2f6`。現行の[動画Director](../../Assets/Scripts/Video/CombatVideoDirector.cs)、[動画演出](../../Assets/Scripts/Video/VideoCombatPresentation.cs)、[敵の吹っ飛び](../../Assets/Scripts/Video/VideoEnemyAnimationDriver.cs)、[パリィ境界テスト](../../Assets/Tests/PlayMode/PlayerState/PrototypePlayerMovementFixTests.cs)は保持。
