# プロトタイプへの受領・仮プレイヤー素材の接続（#49 / #50）

## 目的と受入条件

体験目的: 白グリッドの検証Sceneで、今ある主人公の絵を使って操作と攻撃のつながりを確認できるようにする。

2026-10-18のユーザー指示による素材接続。進行中の `issue/33-52-38-combat-polish` / Draft PR #78 に追加する。#50の24モーション清書・人間の受入完了は意味しない。

実装前に定義するDoD:

1. `PrototypeCombatValidation` の待機・歩行・4段攻撃・パリィ・反撃・移動を、受領素材と同キャラクターの既存ラフへ差し替える。未作画動作は代用と明記し、原本・動画Scene・動画Controllerは変更しない。
2. 攻撃の威力、発生・持続・キャンセル・復帰、移動、Collider、反撃対象・無敵・ストックは変更しない。新しい攻撃の接触姿勢を既存設定の命中tickへ合わせ、イベント契約と4段各1Hitを維持する。
3. 参照検証、関連EditMode/PlayMode、Console、Game Viewで、欠落・旧棒人間への戻り・足元のずれ・状態残留を確認する。差し替え再実行で重複や既存調整の上書きを起こさない。

成功時は斬撃・弾きと入力の結果が見た目で分かること。失敗時も、素材の未完成とゲームロジック上の失敗を区別できることを重視する。演出による追加の入力ロックやスローは入れない。

## 対応と未完成部分

| 動作 | 接続する素材 | 状態 |
| --- | --- | --- |
| 待機 / 歩行 | IdleReference / WalkReference | 既存受領済み、100ms / 80msを維持 |
| 攻撃1 / 2 | VerticalSlash / HorizontalSlash | 提出済み仮素材 |
| 攻撃3 / 4 | RapidSheathe / IaiSlash | 既存ラフ、各段1Hit |
| パリィ構え / 成功 | ParryReady / ParrySuccessLight | 受領済み／仮提出。重は軽へフォールバック |
| パリィ失敗 | ParryReadyの解除側 | 仮の代用 |
| 反撃 | CounterKick | シルエットの仮提出。動画専用の転移・スロー・頭上移動は接続しない |
| ジャンプ / 落下 / 床抜け | jump start / jump upサンプル | 48px原本・100PPUのまま保持し、表示だけ32px/unitと足元へ正規化。落下専用画は未受領 |
| ダッシュ | Dash | 既存ラフ |
| 必殺 / コンボ派生 / 空中斬り | IaiSlash / HorizontalSlash | 仮の共用 |
| 被弾 / 空中受け身 | ParryDeflect / jump up | 仮の代用 |
| 死亡 / 着地衝撃 | AirRoundhouse着地姿勢 | 仮の代用、専用作画は未完了 |
| 回復 / 休憩 / 復帰 | IdleReference | 仮の代用、専用作画は未完了 |

CounterTransferは頭上へ移動する動画専用の転移絵であり、地上の踏み込み反撃へ無理に接続しない。反撃の仕組みを変更する場合は別の明示的な仕様変更として扱う。

## 接続方式

- プロトタイプ専用 `Submitted/PlayerSubmitted.overrideController` で既存24状態のClipだけを上書きする。元Controllerと行動定義は保持する。
- 表示用に元Controllerを `PlayerSubmittedBase.controller` へ複製し、攻撃のAnyState自己遷移だけを禁止する。行動側の直接Play直後に旧Triggerが同じ表示を再開し、約2F遅れることを防ぐ。元Controllerの状態・遷移・Clipを編集しない。
- `PrototypePlayerArtSet.asset` に各動作の出典、完成区分、Clip、接触Spriteと表示時刻を記録する。動画Libraryは生成時に読み取るだけで、実行時の依存を持たせない。
- 攻撃Clipの長さとAnimationEventは元Clipと同一。行動定義によるAnimator速度補正を含め、接触姿勢を現在のactiveStartへ合わせる。復帰のラフは表示だけに使用する。
- 反撃は既存の表示ドライバーの開始が行動タイマーより1F遅れるため、Clip内の接触姿勢を17Fへ置き、ゲーム側18Fの命中と合わせる。命中時刻や威力は変更しない。
- 元SpriteRendererをAnimator/被弾フラッシュの受け口として残し、子 `PlayerArtwork` へ絵・色・向きを転送する。子の位置と描画サイズだけを調整し、Player root・Physics・HitPointは移動しない。
- 再生成は `Tools/Technical Sword Action/Prototype/Install Submitted Player Art`。対象Sceneに未保存変更があれば停止する。動画側の生成処理は実行しない。

## 人間による確認（自動結果と分離）

- [ ] 地上・空中で4段攻撃、各段からDash/Parry/Special、復帰途中の攻撃再開。剣と判定の見た目を確認する。
- [ ] 左右移動、ジャンプの早離し／押し続け、3足場への着地、両側からのすり抜け・床抜け。48pxサンプルの大きさと足元を確認する。
- [ ] 任意のF1/F3/F4/F5プリセットで、パリィ成功と失敗、反撃、被弾、死亡・再開を確認する。反撃の蹴りは地上用の仮表示である。
- [ ] 回復・休憩などの待機代用を清書済みと誤認しない。物理ゲームパッドとキーボードで各15分の操作感確認、Development Buildは別途行う。

## 検証結果

実装・Unity接続後に記録する。並行作業中の未コミット変更を含む環境の結果を、クリーンcheckoutの結果として扱わない。
