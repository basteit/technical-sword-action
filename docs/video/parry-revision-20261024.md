# 軽パリィの更新原稿と作画エフェクトへの切り替え

2026-10-24。提出された `Player_parry_light.aseprite` を2版目として受領。人物と黄色い弾きの作画を採用し、撮影と自由Studioで従来のCodex製パリィ十字・火花・補助Glowを重ねない。ヒットストップ、方向付きのカメラ揺れ、短い暗色の画面Flashは維持する。

## 原稿と差し替え

- 現在版：[Current](../../ArtSource/Player/Current/Player_parry_light.aseprite)。旧64px/7コマの原稿はHistoryと日付付きAssetsに保持。
- 新原稿SHA256：`115ab7f81eb0016932a93ca36d8eb0c9f667beec19dfb392c4943d9916628996`。Downloads原本・Current・History・Assets内の受領コピーがバイト一致。
- 128×128px、8コマ×80ms、32PPU、下原点Pivot(32,38)。上と右に広がったキャンバスを縮小せず、接地と人物の画素サイズを維持。
- Body/Effectを合成し、非表示Sword/Hair/Sampleはそのまま。黒いガイドだけを書き出し時に隠す。各PNGはその状態の原稿と全画素一致。原稿のレイヤー・時間は変更しない。
- レビュー用 [タグ付きAseprite](../../Assets/Art/Player/VideoSubmitted/20261024/Player_parry_light_preview.aseprite) は保存・再読込済み。`ParrySuccessLight` の8コマタグ、元の80ms、ガイド非表示を確認。
- ゲーム用の表示は `[2,1,1,1,1,1,1,2]` の計10戦闘F。最後の8コマ目が反撃分岐より前に出るよう、開始を8Fに置く。判定・受付・成功ロック・反撃入力の時刻は変更しない。
- 60fps主動画では、1コマ目6動画F（既存4F停止を含む）、2〜7コマ目各1動画F、8コマ目2動画F。新8コマをすべて使用する。

`includesParryEffect` をAction/共有Libraryへ記録し、`EmitParry` が実際の軽重に対応する素材を参照する。作画エフェクト付きなら従来の独立VFXを発生させない。旧素材や未作画リアクション用のフォールバックは履歴・再利用用に保持する。

共有PNGとLibrary、撮影用の成功Light/成功互換Clip、本編専用ParrySuccess Clipを個別更新した。Controllerや他アクションを一括再生成しない。新しいメニュー `Video > Refresh Submitted Light Parry` と `Prototype > Refresh Light Parry Clip` で再適用できる。本編Clipは既存の長さ・イベント・接地補正を維持し、今回の本編側確認はClipの参照・8枚・128px/32PPU/PivotのEditMode検証まで。実戦操作の受入や全24モーションの完成を意味しない。

## 動画と維持した内容

[MP4](previews/combat-2stage-hybrid-parry-revision-20261024.mp4)／[通し画像](previews/parry-revision-20261024-contact.png)／[イベント表](previews/combat-2stage-hybrid-parry-revision-20261024.json)／[検証JSON](previews/parry-revision-20261024-validation.json)。チャット内で再生できるプレビューも更新。

**267F / 4.45秒、1920×1080・60fps・H.264・無音。** 全267Fをデコード。全イベント・対象・tick・実時間・撮影メタデータが[直前の版](early-rear-charge-hold-20261024.md)と一致する。

- 2体目の冒頭接近・0.50秒の攻撃開始、全12コマ/96戦闘Fの攻撃、口元の大きい発光後2.15秒のパリィ。
- 1.05秒の2段撃破、実際に弾いたEnemy_Bへの背後反撃。
- タメ1.0秒、蹴る直前の8コマ目7動画F、解除の次tickの命中、全15キックコマと独立電撃の同期、後半10〜15コマ各4F。
- 通常ortho3.375/タメ2.8125/命中最小2.64375、パリィ4F/Just5F・蹴り10F停止、カメラ揺れ、無敵と対象固定。
- A後方0.80/上昇0.18、B後方1.60/上昇0.27unitの固定down姿勢の吹っ飛び・停止・消滅。本編用4段攻撃と既存配置。

## 確認

- Unity Compile Error 0 / Warning 0。
- 関連PlayMode **22/22**：30/60/120fps×左右の新原稿とPause/Reset、旧VFX非発生とフォールバック、全8枚と同対象反撃、4/10F停止、Studio、完走/実時間、左右各10回の吹っ飛び/復元。
- 本編素材の関連EditMode **2/2**：24個のOverride参照とパリィ待機/成功の分離、新8枚/128px/32PPU/Pivot。
- 初回PlayModeは19/22。追加Fixtureのフィールド設定方法を直し、反撃分岐で最後の8枚目が省かれる時間配分を上記へ修正。同じ22件を再実行して全合格。全件や旧80回の組み合わせは再実行していない。
- 267Fの進行記録で全8パリィ画像、旧パリィSpriteの非表示、15キック画像と電撃同期、終端の時間/カメラ/揺れ復元を確認。パリィ成功時には独立VFXのActiveCountが0。
- 撮影/終了後Consoleは全種0件。元の `PrototypeCombatValidation` へEditMode・未保存変更なしで復帰。
- 素材更新後にEditorが未保存扱いにした同Sceneを保存。既存コードの既定値10行（敵のひるみ上書き無効、既存VFX設定）がシリアライズされただけで、配置や操作設定は変えていない。
- 台帳validate成功・更新待ち0。開始時2368ファイル中、意図した49点以外の2319点をバイト保持。旧原稿履歴・他アクション・撮影Scene・本編反撃Profileと開始時の未コミット差分を保持。今回68ファイルのステージ済みRepository監査Passed。

今回の差し替えと録画は完了。動画全体の最終受入は別に残し、#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。重パリィ専用作画や今回省略した復帰などを新しい完成待ちに加えない。

## 再現

原稿登録は `Tools/Art/player_source_library.py register --id parry_light --source <受領ファイル>`。`Tools/Art/export-parry-light-submission.lua` で出力し、`VideoSubmitted` のAction/瞳Manifestを同名で統合する。日時付き原稿/レビューは保持し、上記の個別更新メニューを使う。

保存済み `CombatVideo.unity` でPlayに入り、`VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision parry-revision-20261024 -Variants 2stage-hybrid`。通常操作の撮影は[Studio手順](studio-guide.md)を参照。
