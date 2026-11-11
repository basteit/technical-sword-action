# パリィとカウンターのヒットストップ延長・試用版

2026-10-25。ユーザーの「両方もっと長く」の指示に合わせ、前版より停止を強めた。採用確定ではなく、見え方を比較できる試用値として撮影Scene/攻防切替Scene/自由Studioへ反映した。

| 接触 | 前版の停止 | 今回の停止 | 主動画での接触コマの表示 |
| --- | --- | --- | --- |
| 通常パリィ | 4F | **7F / 約0.117秒** | 3コマ目を5→8動画F |
| Justパリィ | 5F | **8F / 約0.133秒** | 通常との差1Fを維持 |
| カウンター | 10F | **16F / 約0.267秒** | 命中の9コマ目を11→17動画F |

表示時間は停止分に、そのコマ本来の1動画Fを含む。主人公・敵・独立電撃は停止中に同じ姿勢/位置を保つ。パリィ3コマ目の先頭へ揺れの最大点・敵のdown開始を揃えた[直前の同期](parry-impact-cel3-20261025.md)は維持し、タメや振り抜きのコマ時間は延ばさない。

## 動画と比較

- [今回のMP4](previews/combat-2stage-hybrid-longer-hitstop-20261025.mp4)：**273F / 4.55秒、1920×1080・60fps・H.264・無音**。
- [直前のMP4](previews/combat-2stage-hybrid-parry-impact-cel3-20261025.mp4)：267F / 4.45秒、通常4F/カウンター10F停止。
- [連続画像](previews/longer-hitstop-20261025-contact.png)／[イベント表](previews/combat-2stage-hybrid-longer-hitstop-20261025.json)／[検証JSON](previews/longer-hitstop-20261025-validation.json)。チャット内で再生できるプレビューを用意した。

全273Fをデコード。攻撃/パリィ/反撃の対象・イベント種類・戦闘tickはすべて前版と一致する。パリィの判定と3コマ目の接触までは実時間も同じ。延びた停止の分、反撃開始〜キック接触は+0.05秒、着地は+0.15秒。スローのタメは1.0秒、直前8コマ目7動画F、解除→命中1tick、全8パリィ/全15キックと電撃の同期を保つ。

停止を長くした初回収録では、従来の「敵撃破から実時間0.85秒以上」という終了条件で消滅途中を切ってしまった。終了条件へ**敵2体が消え、VFXも終了していること**を加え、消滅完了まで再収録した。元の最低待ち時間/待機姿勢の条件は保持。停止は合計9F増えたが、終端の実時間待ちと消滅待ちの重なりにより、動画全体は6F増えて4.55秒となる。

## 反映範囲

`VideoCombatPresentation` の動画用既定値を通常7/Just8/キック16Fへ集約し、Builderと既存Sceneへの再適用設定も一致させた。既存3Sceneの差分は、各Sceneのパリィ通常/Just/キックの3値だけ。原稿/PNG/Clip/Controller・共有Art・Current/History/catalog、本編反撃Profile、通常攻撃の2F停止、配置/カメラ/揺れ/暗転/VFXの強度は変更しない。

完成判定用のIssueは今回の試用で閉じない。#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。

## 確認

- Compile Error 0 / Warning 0。
- 関連PlayMode **27/27合格**：30/60/120fps×左右のパリィ同期・電撃/人物/敵の接触保持、通常7/Just8/カウンター16F停止と再開、Pause、同じ敵への反撃、Studio、実時間と左右各10回＝20回の通し/Reset。
- 消滅途中の録画終了を直した後、影響する完走/時間の3件と左右の10回Resetの2件を再実行し、**5/5合格**。全件/旧80回の組み合わせは再実行していない。
- 全F記録でパリィ3コマ目8動画F、カウンター9コマ目17動画F。残停止はそれぞれ[7…0]、[16…0]、停止中の戦闘tick・主人公/敵/電撃は固定。最後は両敵非表示・VFX0・停止0・時間倍率1・カメラ/揺れ復元・提出Idleを確認。
- 撮影/終了後Consoleは全種0。元の `PrototypeCombatValidation` へEditMode・未保存変更なしで復帰。
- 開始時2392ファイル中、意図した10点以外の2382点をバイト保持。既存の未コミット変更と全原稿/PNG/Clip/Controller・共有Art・台帳・本編Profileを保持。3Sceneは停止設定の各3値だけ変更。今回15ファイルのステージ済みRepository監査Passed。

## 再現・比較

保存済み `CombatVideo.unity` でPlayに入り `VideoCaptureUtility.CapturePreview(2,true)`、続いて `Tools/Video/encode-captures.ps1 -Revision longer-hitstop-20261025 -Variants 2stage-hybrid`。

Unity上で比較する場合は、動画SceneのPlayerの `PlayerDamageReceiver2D.normalParryHitStop / justParryHitStop` を7/60・8/60秒、`VideoCombatPresentation.kickHitstopFrames` を16へ設定する。前版の比較値は4/60・5/60秒・10F。Inspectorで個別調整でき、旧MP4も保持する。
