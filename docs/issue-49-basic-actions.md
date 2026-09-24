# Issue #49 基本Actionの実装と検証

2026-09-23 / Unity 6000.3.9f1 / `C:\Users\hirot\GitHub\technical-sword-action`

## 統合先と前提

Issue #49 の作業は指定されたGitHub作業フォルダの `codex/49-basic-actions` ブランチへ統合した。開始時点の攻撃モーション、SampleScene、設計文書などの未コミット変更は保持した。今後もこの一つの作業フォルダを使う要件を `AGENTS.md` と `skills/define-game-requirements/references/current-requirements.md` に追加した。

このブランチの起点は `fix/input-dash-left-trigger`。この既存修正ではゲームパッドBはInteract専用、左トリガーはDashである。Issue #49本文にある旧B共有入力規則より、このブランチで後から確定した割当を優先し、今回の変更で戻さない。

## 実装

| 機能 | 動作 |
| --- | --- |
| Heal | 接地Neutral、HP不足、残数ありで開始。初期3回。30FでHPを2回復し、その時点で1回消費。48FでNeutralへ戻る。適用前の被弾・Disableでは消費せず、適用後の中断では消費を維持する。接地を失った場合も中断する。 |
| 可変Jump | 保持中は通常上昇し、離した時の上向き速度を一度だけ0.5倍にする。Jump直後は接地を解除し、二重Jumpを防ぐ。 |
| 床抜け | 下+Jumpで、接地しているone-way PlatformEffector床だけ衝突無視する。対応床がない場合は通常Jump。最短18F後、重なりが解消した時点で衝突を復元する。被弾・Disable・死亡・安全リセットでも復元する。 |
| 空中Dash | 空中開始で1回分を使用。クールダウンが解消しても着地まで再使用不可。接地判定で回復する。 |
| ParryCounter | ParrySuccess中のAttackを専用追撃へ解決する。6Fで前方にdamage 3の一回判定、24FでNeutral。複数Colliderの同じ敵には一度だけ命中する。中断・Disableでロックと命中記録を解放する。 |
| Death / Respawn | 致死被弾でもGameObjectを非アクティブ化せず、Dead状態で速度・重力と操作を停止する。90Fで初期位置または指定spawnPointへ復帰し、HP・Heal残数・ゲージ初期値・入力・ロック・衝突無視をリセットする。y < -20の落下も死亡へ接続する。 |
| Pause | Esc / Menuを最優先処理する。戦闘時計の既存停止規則を使用し、解除時にゲームプレイ予約を破棄する。 |
| Neutral復帰 | 中央CompleteActionでexecutorのキャンセルも行う。既存のDisable・死亡・Scene変更の安全リセットに床抜けと空中Dashのリセットを接続する。 |
| Interact | 単純カウンタ対象を追加する。検証SceneのPlayerにPlayerInteractor2Dを接続し、E/B案内を表示する。BのInteract、左トリガー/ShiftのDash、EのInteractを維持する。 |

タイミング・回復量・追撃の数値は初期値。既存PlayerにはStartで未登録のHeal/CounterとRespawnを補完し、既存handlerは置き換えない。デバッグ表示にHeal残数とLife状態を追加した。

## 検証シーン

`Assets/Scenes/BasicActionValidation.unity` は、コミット済みのSampleSceneを基に、緑のone-way床と水色のInteractカウンタを追加した専用Scene。未コミットのSampleScene変更や未登録素材には依存しない。`Tools > Technical Sword Action > Build Basic Action Validation Scene` で再生成できる。未保存Sceneがある場合は生成を中断する。

## 検証結果

| 検証 | 結果 |
| --- | --- |
| Unity compile | Success / Error 0 / Warning 0 |
| 全EditMode | 172 / 172 成功 |
| 全PlayMode | 51 / 51 成功 |
| 差分チェック | `git diff --check` 成功 |

新規PlayModeテストは実Heal・Motor・Counter・Respawnを使用し、Heal適用前後と3回消費、床抜け/通常床、Jump短押し、空中Dash再使用、Pause、致死被弾・復帰、中断・Disable、専用追撃の重複命中を確認した。地上・空中の4段Attack/Dash/Parry/Specialは各10回。Parry成功からヒットストップ中のAttack予約・専用追撃・Neutral復帰も時計を進めて確認した。

最初のPlayMode実行では、移送時に既存のB専用Interact修正を2ファイルで上書きしたため入力テストが3件失敗した。既存修正を再統合し、最終実行では51/51成功した。最終結果を受入値として扱う。

## 残課題

- Issue #49の旧B共有規則と、後続の左トリガーDash・B専用Interact修正の文書整合が必要。現行実装は後続修正に従う。
- #48の段別Defense/Late Cancel設定データはこのブランチの起点にまだ含まれない。既存の受付APIとの結合確認は後続の統合時に必要。
- キーボード・マウスと実Xbox系ゲームパッドによる全Action各10回の手動操作受入は未実施。自動テストは実機の手触り確認を代替しない。
- Heal/Counterの正式アニメーション、Pauseメニューの装飾、最終数値調整は今後の作業。

## 2026-09-25 受入フィードバック

- ParryCounterとHealの専用モーションは未設定。ロジックの確認結果と区別して残課題とし、この段階では修正しない。
- 水色のInteractカウンタは、検証SceneにPlayerInteractor2DとInteractionPromptViewの配線がなく、案内も操作も動かない不備だった。Sceneと再生成用Builderを修正した。
- Unity compile: Error 0 / Warning 0。Playモードで対象選択、案内表示、E入力によるカウンタ0→1を確認した。
