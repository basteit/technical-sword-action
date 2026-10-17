# 提出2段目の接続確認（2026-10-17）

ユーザー提供の `Player_attack_2.aseprite` を1段目へ接続し、今回は**2段目で敵Aを撃破する動画**を採用する。3・4段目は録画へ出さず、本編と比較用の4段攻撃は保持する。背後パリィから頭上ワープ・スロー・かかと落としへの流れは維持する。

## 受領と取り込み

- 原本コピー: `Assets/Art/Player/VideoSubmitted/20261017/Player_attack_2.aseprite`。Downloadsの原本とバイト一致。
- 接続確認用: `Assets/Art/Player/VideoSubmitted/Player_attack_2_preview.aseprite`。黒い作画ガイドだけを非表示にし、Actionタグ `HorizontalSlash` を追加。Hair/Sampleの非表示を保持する。
- 128×128px、32PPU、Pivotは左下原点(32,38)px。縮小・切り抜き・絵の描き直しは行わない。
- 元の8コマ×70ms、計560msを保持。Unityは累積時刻を60Hzへ丸め、34F / 0.566667秒にする。原本の見える画素は黒いガイド1857画素以外一致。1段目9コマの既存PNGと原本を保持した。
- `includesSlashTrail` で描き込み斬撃を明示し、1・2段目には汎用Slashを重ねない。ダメージ・ヒットバック・軽い停止・画面の揺れは維持する。

| コマ | 元の時間 | Unity開始F | 表示F | 役割 |
|---|---:|---:|---:|---|
| 1 | 70ms | 0 | 4 | 前段からの構え |
| 2 | 70ms | 4 | 4 | 刃の切り返し |
| 3 | 70ms | 8 | 5 | 斬り上げの始動・軌跡 |
| 4 | 70ms | 13 | 4 | 大きな斬り上げ・実命中 |
| 5 | 70ms | 17 | 4 | 振り抜き・軌跡の分離 |
| 6 | 70ms | 21 | 4 | 軌跡の減衰 |
| 7 | 70ms | 25 | 4 | 残光の消失 |
| 8 | 70ms | 29 | 5 | 終端保持 |

1段目の次段接続は従来の34F、2段目の命中は13F。撮影専用の敵AはHP2、各段1ダメージ。1回目で生存、2回目の命中tickに撃破する。今回の録画は `CapturePreview(2,true)` を使い、3・4段目の入力を送らない。4段攻撃と任意の4段撮影は削除しない。

## 再現手順

1. Asepriteの原本を保持したまま `Tools/Art/export-player-submission.lua` に `action=HorizontalSlash` を渡して別の作業出力へ書き出す。既存のVerticalSlashとmanifest/瞳マスク一覧を統合する。
2. 保存済み動画Sceneで Tools → Technical Sword Action → Video → Apply Submitted Attack and Heel Drop を実行し、Clipと3つの撮影Sceneのタイミング・前進を更新する。無関係な未保存Sceneは上書きしない。
3. `Assets/Scenes/CombatVideo.unity` でPlayModeに入り、`VideoCaptureUtility.ValidateResetRuns()` と `VideoCaptureUtility.CapturePreview(2,true)` を実行する。
4. `Tools/Video/encode-captures.ps1 -Revision submitted-attacks-1-2 -Variants 2stage-hybrid` で今回の1本だけをMP4へ書き出す。過去の2段・4段動画は上書きしない。

パリィ・かかと落とし等には従来ラフを含む。今回の提出素材の接続確認と、動画全体の最終受入を区別し、#69を完了にしない。

## 出力と検証

実装・素材のコミットは `01fc867`（`issue/33-52-38-combat-polish`）。今回の提出素材と撮影条件は、このブランチの既存実装へ統合した。

[今回の2段版MP4](previews/combat-2stage-hybrid-submitted-attacks-1-2.mp4) は1920×1080・60fps・無音、254F / **4.233333秒**。スローとヒットストップを含む実時間で測定した。全フレームをデコードし、Slash1→Slash2だけを実行、敵Aの2段目命中と同tickの死亡、パリィと反撃の対象個体一致を確認した。スローは実時間0.666667秒、解除から命中4Fを維持する。

Unity compile Error 0 / Warning 0、PlayMode318/318・EditMode186/186 Passed。新規3ケース相当で両提出素材のサイズ・Pivot・累積F、両向きの2段目撃破、3/4段目不使用、描き込みSlashの二重表示防止を確認。全PlayModeに通常/攻防切替Sceneの2/4段×左右の反復を含む。最終撮影Sceneでも2/4段×左右×各10回、計40回のリセットを通過し、二重ダメージ・誤対象・操作不能・時間/カメラ/AI/VFX等の残留なし。今回書き出した動画は2段版だけ。

原本コピーのSHA256は `CCDB5DA3908F8DC0FD730291036964C5B6C2380991CF1DEF238E13A5FA5CE781`。1段目を含む既存画像・元動画・未コミット変更を保持した。検証着手時に見つかったPrototypeCombatValidationの未保存変更は、ユーザーの「保存して続行」の承認後に保存。保存前後のファイルはバイト一致し、収録後はこの元SceneをEditMode・未保存変更なしで開き直した。収録後Console Error 0 / Warning 0。

[検証JSON](previews/attack2-submission-validation.json) / [動画の連続画像](previews/submitted-attacks-1-2-contact.png) / [2段目の原テンポGIF](rough-previews/SubmittedAttack2.gif) / [2段目のコマ表](rough-previews/submitted-attack2-contact.png)。パリィ・かかと落とし等の人の素材、全体の最終品質確認は未達であり、#69の完成判定は行わない。
