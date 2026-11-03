# 戦闘Feedback・強ザコ・GitHub Flow（#33 / #52 / #38）

## 目的と範囲

敵の特性に合う耐久と読める予兆を持たせ、命中・パリィ・被弾の手応えを区別しながら、安全に反復開発できる状態にする。

2026-10-17のユーザー指示による3 Issue一括作業。通常の1 Issue = 1タスク・ブランチ・PRに対する明示的な例外とし、Issueごとの意図でコミットを分ける。作業ブランチは `issue/33-52-38-combat-polish`、基点は前回5 Issueの実装 `10ec28e`。未統合の動画PR #77と前回実装を含む依存ブランチであり、mainへの独立した最小差分ではない。自動マージや受入前のIssueクローズは行わない。

既存のScene・素材・会話試作・文書変更を保持する。README、CONTRIBUTING、PRテンプレートには#38に合致する既存変更があり、内容を確認して統合する。現行要件の既存の広範な変更は今回分と分離する。

## 最新の敵耐久方針

- HPは敵定義ごとに持ち、必要な配置個体では明示的に上書きできる。
- 通常雑魚はその時点のプレイヤー水準で反撃一発を調整目安とする。一撃死をカテゴリから強制しない。
- 強ザコは反撃一発に耐えるHPを初期設定とし、元の重量・変則カテゴリを維持する。小ボス化、専用報酬、再出現ルール変更はしない。
- 反撃倍率、軽／強ひるみ、攻撃中の中断可否もデータで調整する。初期数値は仮置きであり、最終バランスは人が評価する。

## 先に固定する受入条件

### #52

1. 個別HP・反撃倍率・ひるみ耐性をコード変更なしで調整でき、通常雑魚は初期反撃で撃破、強ザコは満タンから1回に耐える。
2. パリィ可能斬撃とJump/Dashで避ける不可攻撃を、姿勢・形・テンポから発生前に区別できる。
3. 通常雑魚との組合せで攻撃役を制御し、死亡・ひるみ・画面外・Disable・リセット時に判定や役割を残さない。

成功時は敵ごとの対処を選んで反撃できる。失敗時は攻撃種、予兆、実ダメージ、耐性、攻撃役の状態から理由を説明できる。

### #33

1. 通常命中、4段目／反撃、通常／Just Parry、被弾、撃破を異なる形の視覚情報と音設定から区別できる。
2. 演出は実際の成功Hitから同tickで発火し、空振りと防がれた被弾では成功演出を出さず、同tickの停止は最長値だけ採用する。
3. 揺れ・Flash・VFX・音量を調整でき、0%でも非色情報の命中／予兆を残す。Pause・Disable・リセット・Scene変更で演出・時間を残さない。

成功時は弱い命中から強い反撃への差を感じる。失敗時も予兆と接触を隠さず、派手な演出で誤認させない。最終SEや本制作素材は対象外。

### #38

1. main、Issueブランチ、Draft PR、小さなIssue付きコミット、Squash mergeの運用を文書・テンプレート・設定で一致させる。
2. UnityYAMLMerge、Assetと.meta、生成物除外、差分監査を再現可能にする。
3. developの固有コミットと利用先を監査し、安全な場合だけ廃止する。mainはPR・線形履歴・force push禁止を要求し、一人開発の承認人数や未安定CIを必須にしない。

## 検証・人間の受入

コンパイル、設定検証、関連テスト、既存を含む全回帰、Console、PlayMode表示をエージェントで確認する。15分の操作感・疲労感、実機入力、初見可読性、Development Buildの最終受入は前回5件と統合したチェックリストで人が確認する。未実施項目を合格扱いしない。

## 実装結果と調整経路

- 敵定義は `EnemyCombatDefinition`。maxHp、カテゴリ初期値または明示的な反撃倍率、軽／強ひるみ、軽ひるみ再耐性、ノックバック、Parryひるみを別々に設定する。定義がある敵では旧Breakゲージを使わない。
- 配置個体のDamageable2Dでは `overrideMaxHp / instanceMaxHp` と `overrideCounterMultiplier / instanceCounterMultiplier` を使う。同じ定義を使う他の個体や元Assetを書き換えない。カテゴリによる強制即死はない。
- 重量強ザコは変則カテゴリ、HP120、移動1.25unit/s、軽ひるみ0F、強ひるみ16F、反撃倍率3.0が仮の初期値。通常雑魚はHP30とカテゴリ初期倍率3.0を維持する。
- `StrongMelee.asset` のattacksに `StrongSlash.asset` と `StrongLowSweep.asset` を順番に接続した。空配列の既存データは従来の単発攻撃経路を維持する。

| 攻撃 | 予兆／Active／後隙／待機 | 威力 | パリィ | 判定と対処 |
| --- | --- | ---: | --- | --- |
| heavy-overhead-slash | 42 / 4 / 38 / 40F | 14 | 可 | offset(.8,0)、size(1.65,1.3)。ParryまたはDash、後隙へ反撃 |
| heavy-low-unparryable-sweep | 54 / 5 / 48 / 42F | 20 | 不可 | offset(.9,-.5)、size(1.9,.55)。Jumpで低い払いを越える、またはDash。予兆／Active中は通常ひるみに耐える |

距離1.65unit、移動量0、画面外開始禁止。予兆の四角と菱形、沈み込みと武器の高さ、異なる仮音を組み合わせる。色だけに依存しない。遭遇内の攻撃役はこの検証Sceneでは1体まで、開始間隔18F。役割は予兆から後隙まで保持し、ひるみ・Parry・死亡・Disable・リセットで解除する。ParrySuccess／Counter／OpportunityStrike中のPlayerへ新しい攻撃を開始しない。上限は検証用の保守的な値で、製品難易度全体の上限を変更しない。

共通Feedbackは `CombatFeedback.asset`、演出強度は `FeedbackSettings.asset`。通常3F、4段目／Special／反撃5F、通常Parry3F、Just7F、被弾3F、撃破／失敗Parry0F。設定はプロトタイプの仮値であり、撮影用の2/3/8Fを変更しない。Hitstopは既存のCombatTimeControllerへ所有者付きで要求し、同tickは最大値だけを採用する。

命中情報は別形状の最小glyph、装飾VFXは追加線として分離した。揺れ・Flash・装飾VFX・音量0%でもglyphと敵の予兆は残る。全画面の白Flashや新しい撃破スローは追加しない。有限プール、同tickの音の優先置換、Pause・Reset・Disable・Scene変更での復元を持つ。既存MP3を仮SEとして再利用し、本制作音・素材の承認は行っていない。

既存のSampleScene／BasicActionValidation／撮影Sceneは変更しない。専用検証Sceneだけへ接続する。再接続メニューは `Tools > Technical Sword Action > Prototype > Install Strong Enemy and Feedback`。既存の設定Assetを上書きしない。未保存の検証Sceneは自動保存せず停止する。

## GitHub運用の適用結果

- [Draft PR #78](https://github.com/basteit/technical-sword-action/pull/78)を作成。mainとの差分には動画#77と前回5 Issueが含まれる。merge-treeの読取り検証では競合なしだが、依存の統合順と人間の受入が必要で、自動マージしていない。
- main ruleset `25262147` を有効化。PR必須、削除／force push禁止、線形履歴、会話解決。承認0人、追加承認なし、CI必須化なし。Squashのみ、統合後ブランチ自動削除。
- develop `e74bd04` はmainにない固有コミット0件、開いたPR／実行設定で利用なし、default branchでないことを確認し、remote/localから通常削除した。mainへ旧developをマージしていない。
- `.gitattributes` とUnityYAMLMergeのローカル設定手順、生成物除外、Assetと.meta・ブランチ名・コミットの監査を追加した。PowerShell 5.1で空白を含むEditorパスの引用符も読み戻し検証した。
- 運用文書の既存変更は内容を確認して関連分を統合。現行要件は今回の確定事項だけを選択stageし、元からある大きな要件改訂と計画・仕様書・素材・Sceneの作業中差分は保持する。

## 自動検証結果（2026-10-17 JST）

実装 `99f40eaf05d89744ed7542d1840341f2b4da2369` に対し03:25:36〜03:26:49に一括回帰を実行した。

- Compile: Error 0 / Warning 0
- 設定／Clip／Scene参照検証: Passed
- EditMode: 186 / 186 Passed
- PlayMode: 298 / 298 Passed（追加契約18件、実Scene統合6件を含む）
- Console Error: 0。Game View確認後もError 0 / Warning 0。
- 実Sceneで強ザコが反撃90ダメージ後HP30で生存、不可攻撃はParry不可だがJump/Dashで回避、混成10回リセットを確認。
- Sceneアップグレードの再実行前後でファイルSHA256が一致。標準と演出0%のGame Viewを取得し、表示を確認後にプレビューを解除・PlayModeを停止した。

初回の実Sceneテストでは、リセットによる描画1Fの入力抑止中にテストが操作して3件失敗した。テストに描画フレーム待ちを加え、実際と同じ条件で6件すべて合格した。ゲーム側の入力ガードを弱める変更はしていない。

結果要約は[検証記録](validation/prototype-combat-2026-10-17.json)。前回分を含む未実施の人間向け項目は[統合受入チェックリスト](prototype-human-acceptance.md)。#34/#35/#36、#50の素材承認、Windows Buildは合格扱いしていない。
