# プレイヤー素材一覧

[運用・追加更新](README.md) / [機械可読の台帳](catalog.json)

`Current/` が現在の編集用原本。`History/` は変更しない履歴。コマ数と時間はAseprite原稿の値です。

| 動作 | 現在版 | 区分 | 原稿 | 使用版の記録 | 履歴 |
| --- | --- | --- | --- | --- | --- |
| 待機 (`idle`) | [Player_idle.aseprite](Current/Player_idle.aseprite) | 受領済み | 64×64 / 6コマ / 600ms | video：登録版一致 | [1版](History/idle/) |
| 歩行 (`walk`) | [Player_walk.aseprite](Current/Player_walk.aseprite) | 受領済み | 64×64 / 8コマ / 640ms | video：登録版一致 | [1版](History/walk/) |
| 攻撃1段目 (`attack_1`) | [Player_attack_1.aseprite](Current/Player_attack_1.aseprite) | 仮提出 | 128×128 / 9コマ / 630ms | video：登録版一致 | [1版](History/attack_1/) |
| 攻撃2段目 (`attack_2`) | [Player_attack_2.aseprite](Current/Player_attack_2.aseprite) | 仮提出 | 128×128 / 8コマ / 560ms | video：登録版一致 | [1版](History/attack_2/) |
| パリィ待機 (`parry_wait`) | [Player_parry_wait.aseprite](Current/Player_parry_wait.aseprite) | 受領済み | 64×64 / 8コマ / 800ms | video：登録版一致 | [1版](History/parry_wait/) |
| パリィ成功（軽） (`parry_light`) | [Player_parry_light.aseprite](Current/Player_parry_light.aseprite) | 仮提出 | 64×64 / 7コマ / 700ms | video：登録版一致 | [1版](History/parry_light/) |
| 反撃への移行 (`counter_transfer`) | [Player_counter_transfer.aseprite](Current/Player_counter_transfer.aseprite) | 仮提出 | 64×64 / 8コマ / 800ms | video：登録版一致 | [1版](History/counter_transfer/) |
| 反撃キック (`counter_kick`) | [Player_counter_kick.aseprite](Current/Player_counter_kick.aseprite) | 仮提出 | 128×128 / 15コマ / 1480ms | video：登録版一致 | [1版](History/counter_kick/) |
| ジャンプ開始サンプル (`jump_start`) | [Player_jamp_start_sample.aseprite](Current/Player_jamp_start_sample.aseprite) | 既存サンプル | 48×48 / 5コマ / 500ms | 使用記録なし | [1版](History/jump_start/) |
| ジャンプ上昇サンプル (`jump_up`) | [Player_jamp_up_sample.aseprite](Current/Player_jamp_up_sample.aseprite) | 既存サンプル | 48×48 / 1コマ / 100ms | 使用記録なし | [1版](History/jump_up/) |
| 旧待機サンプル（参考） (`idle_legacy_48`) | [Player_idle_sample.aseprite](Current/Player_idle_sample.aseprite) | 参考 | 48×48 / 4コマ / 800ms | 使用記録なし | [1版](History/idle_legacy_48/) |

## 素材ごとの注意

- **待機**：現在の64px待機。ゲーム用は6コマ×100ms。旧48px待機サンプルと区別。
- **歩行**：標準の歩行で使用。原稿の表示時間とゲーム内の移動速度は別に管理。
- **攻撃1段目**：縦斬りの仮提出。後ろ髪・復帰の追加前。128pxの余白を含め原本保持。
- **攻撃2段目**：1段目から逆側へ返す斬撃。現在の動画はこの2段目で撃破。
- **パリィ待機**：入力時の構え。成功軽・成功重とは別素材。
- **パリィ成功（軽）**：軽く弾く反応の仮提出。成功重の専用作画は未受領。
- **反撃への移行**：原稿のEffect/Eyeの表示と不透明度を保持。移動・暗転・残光は使用先で調整。
- **反撃キック**：Sample表示・Body非表示の動き確認用シルエット。人物清書・目・足の青白い電気は未完成。
- **ジャンプ開始サンプル**：既存の48pxサンプル。サイズや接地位置をこの整理で描き直さない。
- **ジャンプ上昇サンプル**：既存の48px・1コマ。落下専用の完成原画とは区別。
- **旧待機サンプル（参考）**：旧48px版。現在使用している64px待機とは別の参考原稿。

## Codex製ラフ・既存の書き出し

以下は受領原本と区別して保持します。

- [初期アクション・復帰ラフ](../../Assets/Art/Player/VideoRough/Player_actions_rough.aseprite)：Codex製の編集可能ラフ。受領原稿とは別。
- [移動アクションのラフ](../../Assets/Art/Player/VideoShowcase/Player_mobility_rough.aseprite)：Codex製のジャンプ/移動等の動作確認用。
- [旧かかと落としラフ](../../Assets/Art/Player/VideoHeelDrop/Player_heel_drop_rough.aseprite)：提出キック以前の案。現在版のキックと区別。
- [動画向けPNG・提出レビュー](../../Assets/Art/Player/VideoSubmitted)：Unityで使う書き出し、日付別の旧保存先とタグ付きレビュー。
- [原稿とUnityの追加・更新ガイド](../../docs/video/player-cleanup-handoff.md)：動画の提出・差し替えと残る作画の記録。
- [本編の検証ステージ](../../docs/prototype-action-stage.md)：本編の素材使用版は、各接続作業の反映・検証後に台帳へ記録する。
