# プロトタイプの汎用雑魚表示（#43）

> #43時点の基準と検証記録。現在の通常ザコの尺・パリィ姿勢・反撃の吹っ飛び、通常攻撃速度の既定は[動画準拠比較 #34](prototype-reference-combat-trial.md)を優先する。元の30F/chance/回転ありと元の攻撃データは比較設定として保持する。

## 体験目的

F1の近接通常雑魚を、動画で使用した受領済みの汎用雑魚サンプルへ差し替え、予兆・被弾・パリィよろめき・撃破をキャラクターの姿で確認できるようにする。

## 受入条件（実装前）

1. `PrototypeCombatValidation` のF1と、同じ通常雑魚を使うF3/F5で、動画の受領済みEnemySampleが表示される。F2と起動時は従来どおり敵なし。F4の強ザコは対象外。
2. 待機・移動・予兆・攻撃・被弾・パリィよろめき・撃破が実際のAI/HP/戦闘tickへ追従し、左右反転と足元が崩れない。予兆は現在の30Fへ合わせ、撮影専用96Fを通常AIへ持ち込まない。
3. HP30、攻撃10、反撃倍率3、よろめき180F、各判定と敵の移動規則を維持する。Pause/Hitstopで表示が停止し、リセット・無効化後に姿勢・透明度・死亡表示の所有を残さない。動画の移動/吹っ飛びDriverと本編の吹っ飛びを二重適用しない。

正しい対処で敵の弾かれた姿から反撃へつながる気持ちよさを重視する。失敗時は現在の攻撃予兆・判定の意味を変えず、見た目だけを速くして不意打ちにしない。

## 作業範囲

- `issue/43-prototype-enemy-art`、基点 `26d3956f17cb647d918b71e48cccd75e07bf472b`。最新プロトタイプと会話が未統合のためPR #78 / #79へ依存する。別worktreeを作らず、既存の未コミット変更を保持する。
- `Assets/Art/Enemy/Submitted/20261019/` の既存Spriteを再利用。原本、PNG、動画のLibrary/Clip/Sceneは変更しない。
- 本編専用の最小Enemy Art Snapshotと表示コンポーネントを追加する。判定とroot移動は既存AI、Damageable、EnemyCounterLaunchが所有し続ける。
- 被弾には未提出の専用モーションの代わりにchanceを流用し、パリィよろめきはchance、撃破はdownを表示する。撃破の表示・フェードだけを戦闘時計で進める。
- 通常攻撃のテンポについては提案のみ。プレイヤー原稿、攻撃データ、Clipの再生尺、Hitstopは今回変更しない。

## 検証結果

- 2026-10-25: Compile Error0 / Warning0、素材EditMode 3/3、関連PlayMode 103/103（今回の表示12件、パリィ反撃、会話）合格。
- 30/60/120fpsで3秒のよろめき表示、左右・足元、30F予兆の全12コマ、実Hit、Pause/Hitstop、被弾・撃破・フェード、反撃の吹っ飛び所有、10回リセットを確認した。
- 初回のテスト1件は、反撃命中のHitstop中に回転するという誤った期待を修正した。停止解除後の戦闘tickで回転を確認する。ゲーム側の停止規則は変更していない。
- idle/chanceの不透明画素の最下端は原稿下から6pxでPivotより32px下。32PPUで1unitとして足元をCollider下端へ合わせる。downだけは2px高いため、描画を2/32unit下げて床へ合わせる。原稿やColliderは変更しない。
- ULoopの `simulate-keyboard F1` は補助UIのMissing Script / `InputVisualizationCanvas component not found on prefab` で完了しなかった。今回の検証CLIだけを停止し、PlayModeを終了して補助オブジェクトを破棄した。パッケージとSceneの保存データへこの補助UIを持ち込まない。F1と同じ `ResetArena(false)` の経路で画面確認を行う。
- 最終確認（実装 `fc32f93`）: Compile Error0 / Warning0、全EditMode **220/220**、関連PlayMode **104/104**（表示13件＋パリィ反撃＋会話）。予兆中にリセットしても、次のtickを待たず待機絵へ戻ることを追加で確認した。
- F1と同じ処理で通常雑魚1体、HP30、`EnemySample_idle_01`、足元world y=0を[1920×1080 Game View](validation/prototype-common-enemy-2026-10-25/Rendering_20261025_222003_450.png)で確認した。
- 最後は敵なし・通常入力・通常時計へ戻してEditModeへ復帰。対象Scene dirty=false、Missing Script0、Console Error0 / Warning0。[検証要約](validation/prototype-common-enemy-2026-10-25.json)。全体PlayModeは再実行しておらず、先行版の既知13件を今回解決した扱いにしない。

物理ゲームパッドや15分の操作感、最終アート品質の承認を自動テストで代用しない。旧配布ZIPは更新しない。
