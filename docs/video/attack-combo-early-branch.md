# 1段目の終盤を早めに切り上げる接続（2026-10-17）

ユーザーの「1段目の後半を少しキャンセル気味にして2段目へ早めに派生」に合わせ、次段を予約した場合だけ接続を34Fから29Fへ、5F / 0.083333秒早めた。斬り下ろしの命中と振り抜きは残し、残光が薄くなる終盤を切り上げる。単発時の復帰開始は34Fのままで、提出原画の9コマ×70ms / Unity38Fと命中17Fを保つ。

`PlayerAttack2D.OnComboBranch` は予約済みの次段だけを開始する。未予約・異なる段・停止中・定義駆動の攻撃では進めない。Clip生成はbranchとrecoveryを分離し、既存の防御/後半キャンセル開始と本編4段を保持した。Asepriteの再書き出しでも29F/34Fを再現し、再読込と全9コマの画素一致を確認した。元のAsepriteと既存画像は変更しない。

## 動画

[2段撃破・早い接続版MP4](previews/combat-2stage-hybrid-submitted-attacks-1-2-early-branch.mp4) は1920×1080・60fps・無音、**249F / 4.15秒**。スローとヒットストップを含む実時間。前の4.233333秒版は比較用に保持する。

| 確認点 | 前回 | 今回 |
|---|---:|---:|
| 1段目の予約済み接続 | 34F | 29F |
| 単発の復帰開始 | 34F | 34F |
| 1段目命中 | 0.583333秒 | 0.583333秒 |
| 2段目命中・敵A撃破 | 1.133333秒 | 1.050000秒 |
| パリィ | 2.133333秒 | 2.050000秒 |
| 動画尺 | 254F / 4.233333秒 | 249F / 4.15秒 |

2段目以降の相対タイミングは変えず5F前倒し。パリィと反撃は同じ敵B、スローは0.666667秒、解除からかかと落とし命中は4F。3/4段目は今回の動画へ出さない。

チャットには同じMP4を埋め込んだ再生プレビューを提示する。外部動画URLやローカル配信サーバーに依存せず、ネイティブの再生/シーク/全画面操作で確認できる。

## 検証と再現

実装・素材タイミングは `4886a14`（`issue/33-52-38-combat-polish`）。

- Unity compile Error 0 / Warning 0、収録後Console Error 0 / Warning 0。
- 予約あり/なしを30/60/120fpsで追加検証。PlayMode **324/324**、EditMode **186/186** Passed。
- 最終撮影Sceneで2/4段×左右×各10回、**40回**リセット。二重ダメージ・誤対象・操作不能・時間/カメラ/AI/VFX等の残留なし。
- MP4の全249Fをデコード。1080p60・音声ストリームなし、2段目と敵A死亡の同tick、1段目命中は不変、以後5Fだけ早まったことをイベントJSONで照合。
- 既存の未コミット変更、元のScene/原素材/過去動画を保持。収録後は `PrototypeCombatValidation.unity` をEditMode・未保存変更なしで復元。

保存済み撮影Sceneで `HeelDropVideoSetup.Apply()`、PlayModeで `VideoCaptureUtility.ValidateResetRuns()` → `VideoCaptureUtility.CapturePreview(2,true)` を実行し、`Tools/Video/encode-captures.ps1 -Revision submitted-attacks-1-2-early-branch -Variants 2stage-hybrid` で再現する。

[イベントJSON](previews/combat-2stage-hybrid-submitted-attacks-1-2-early-branch.json) / [検証JSON](previews/early-slash-validation.json) / [通し画像](previews/submitted-attacks-early-branch-contact.png)。パリィ/かかと落とし等には従来ラフが残り、#69の全体完成・清書受入とは区別する。
