# 戦闘プロトタイプ統合：#30 / #42 / #43 / #44 / #49

## 目的と作業範囲

通常攻撃、敵の予兆、パリィからの反撃、回復・死亡復帰を一続きに試せ、失敗理由と終了復帰を診断表示と自動テストから説明できる状態にする。

2026-10-16のユーザー指示により5 Issueを同じタスク・ブランチへまとめる。通常の1 Issue = 1タスク規則に対する今回の明示的な例外である。作業場所は既存の指定フォルダ。基点は動画機能と標準歩行を含む `d8fc5e8`。動画用PR #77の未統合コミットを依存として含むため、今回のブランチだけをmainへ統合すると動画作業も含まれる。

## 受入の軸

1. 全基本Actionは終了・中断・死亡・Disableから安全に復帰し、重複命中や資源の二重消費を起こさない。
2. パリィ反撃は実際の敵個体ごとに保持し、選択した1件だけを開始時に消費して、その対象だけへ倍率を適用する。
3. 入力の採用・拒否、攻撃フェーズ・受付境界、敵状態、被弾原因を表示し、回帰テストを1コマンドで実行できる。

成功時は予兆を読んで弾いた相手へ攻勢を返せる。失敗時は受付外・範囲外・状態ロック・対象消失を区別し、操作不能を残さない。

## 実装

| Issue | 内容 | 主な接続 |
| --- | --- | --- |
| #30 | 3グループの診断パネル、入力・B解決理由・tick・停止理由・速度所有者・受付窓・ストック取得順・被弾元・5秒/累積DPS | PlayerDebugOverlay / PlayerCombatTelemetry |
| #42 | 既存テストと追加のPrototypeCombatContractTests、Compile→設定/参照検証→EditMode→PlayMode→Consoleの逐次実行 | Tools/Prototype/Validate-Combat.ps1 |
| #43 | 近接敵のデータ化、予兆→Active→後隙→待機、個体/Attack ID、重複防止、壁・画面外開始制限、非色情報の仮予兆 | EnemyCombatDefinition / EnemyMeleeChaser2D / EnemyCombatCue2D |
| #44 | 個体別保持・登録順・適格性選択・消費・破棄、単体命中、敵カテゴリ倍率、6F予約、防御キャンセル、中断時拘束解除 | ParryCounterDefinition / PlayerParryCounter2D |
| #49 | B文脈解決、回復中40%移動、コヨーテ、死亡時ゲージ保持、必殺のCollider重複排除、行動表示 | InputRouter / Motor / Heal / Respawn / Special |

## 最新攻撃要件への接続

現行要件3.5の4フェーズ＋共通キャンセル硬直を `PlayerAttackDefinition` へまとめた。設定ありのSceneでは戦闘tickを正とし、Animation Eventは二重命中・二重終了を起こさない。持続中に接触した対象へ1段1Hit。1〜3段の共通窓ではAttackで即座に次段、Parry/Special/Dashで遷移する。復帰ではNeutralへ戻り、全行動を受け付け、Attackは1段目から始める。Jumpは共通窓の遷移先へ追加せず、Neutral/復帰から使用する。

プロファイル未指定の既存Scene・動画は従来のAnimation Event経路を維持する。既存のキャラクター絵は編集せず、検証Sceneの各Hit/復帰時刻は既存Clipから生成する。最終作画・接触品質の受入は #50 と人の確認に残す。

ParryTimingDefinitionは発生0〜4F、Just5〜8F、受付5〜18F、失敗終了26F、成功10F/Just8F、終了後CD14F。検証Sceneへ接続し、動画のパリィ時刻は保持する。

反撃の本編プロファイルは基礎威力30、Hit18F、End35F、Dash/Parry25〜35F、終了前6F予約、命中ゲージ+3。通常/変則3.0、小ボス級2.0、中ボス/ボス1.5を最後に掛け一度だけ四捨五入する。動画の頭上かかと落としは撮影プロファイルを維持する。Opportunityは別のAction契約として選択順位だけを接続し、大技・90F機会そのものは今回新設しない。

## 検証Sceneと設定

- Scene: `Assets/Scenes/PrototypeCombatValidation.unity`
- 設定: `Assets/Settings/PrototypeCombat/`
- 初回生成: `Tools > Technical Sword Action > Prototype > Build Combat Validation Scene`
- 生成メニューは既存の同名Sceneを上書きしない。通常は生成済みSceneを開く。
- 灰色床、壁、すり抜け床、検証用Interact、通常雑魚を配置。2体目は初期無効。
- F2 / 画面右上のボタンで1体または2体へリセット。HP、ゲージ、回数、位置、AI、ストック、入力、時間、計測を復元する。
- ゲームパッドBは対象ありでInteract、なしでDash。既存LTのDashも補助入力として維持。ShiftとEは独立。
- 主人公HP100、回復40/3回/36F適用/54F終了、初期ゲージ100、初期Special威力60/消費50。通常雑魚HP30。数値は調整前の検証値。
- #52の強ザコと、そのカテゴリ矛盾の解消は今回の範囲外。

## 一括自動検証

Unityをこのフォルダで開き、保存前のEditor Scene変更を手動で整理してから実行する。一括手順は未保存Sceneを自動保存しない。

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Prototype\Validate-Combat.ps1
```

`-Focused` はPlayModeを今回追加した契約テストへ絞る。完全回帰は引数なし。結果は `.uloop/outputs/PrototypeValidation/*.json` へ開始/完了時刻・コミットSHA・各結果を保存する。ULoopの失敗時XMLも参照できる。テストは同時実行しない。

## 後で人がまとめて確認する項目

- [ ] 検証Sceneでキーボード/マウスとXbox系ゲームパッドを各15分操作。Move/Jump/床抜け/空中Dash/4段Attack/Parry/Counter/Special/Heal/Interact/Pause/Hit/Death/Respawnを一巡する。
- [ ] Bを対象あり・なし各10回。ありはInteractのみ、なしはDashのみ。対象が押下後に消えても遅延Dashなし。Shift/Eは対象有無に依存せず独立。
- [ ] 地上・空中4段を各10回。キャンセル硬直の早押し/開始/最終F/直後、次段、Parry/Special/Dashを確認。復帰中はMove/Jump/Heal/Interactを含む全行動を開始でき、Attackは1段目。
- [ ] 敵の予兆を最初の5分観察。姿勢・武器・形から通常攻撃を発生前に読めるか、後隙へ反撃できるかを確認。音なし・色を区別しにくい表示でも確認。
- [ ] 近接雑魚を4段、ParryCounter、DashからのAttack、Specialで撃破。接触だけでダメージが発生せず、壁越し・画面外開始・演出裏からの不意打ちがないことを確認。
- [ ] 2体設定でA→Bをパリィし2件、A→Aでは1件。時間・一時範囲外・通常被弾で保持し、開始時だけ1件消費。反撃中の対象固定、別対象の保持、死亡・リセット時の破棄を確認。
- [ ] 反撃の命中、終了前6Fの予約、保持入力で連続しないこと、Dash/Parryキャンセル、対象消失・中断からの復帰を確認。
- [ ] 回復の適用前後に被弾し、適用前は回数維持・適用後は消費維持。歩行中の回復、3回上限、死亡復帰時の補充を確認。
- [ ] Pause前後の入力連打、空中、床抜け中、被弾中、死亡時を確認。解除直後の攻撃・Dash暴発、衝突無視・無敵・時間倍率残留がないことを確認。
- [ ] 診断パネルの3グループを切り替え、実際の入力/敵/被弾/ストックと一致すること、DPS計測リセット、文字の読みやすさ、表示による体感負荷を確認。
- [ ] 通常Scene・BasicActionValidation・撮影Studio/動画Sceneを開き、今回の変更前に可能だった操作・撮影が維持されているか確認。
- [ ] Windows Development Buildで両入力を確認し、1〜3分録画、設定値、既知問題、テスト結果を同じコミットへ紐づける。

人の確認待ちを残した状態では #35/#36 や今回の5 Issueの最終受入を自動的に完了・クローズしない。

## 2026-10-16 自動検証結果

検証した実装コミットは `3ea74469f067d375dca1170e1083cd7c3edccdc5`。日本時間10:49:54〜10:51:05に、一括検証スクリプトを引数なしで実行した。

- Compile: Error 0 / Warning 0
- 設定・Clip・Scene参照検証: Passed
- EditMode: 186 / 186 Passed
- PlayMode: 274 / 274 Passed（今回追加の契約77件、リセット直後の入力1件を含む）
- Console Error: 0
- 既存動画・Studioの回帰を含む。人の15分プレイ、実機ゲームパッドの手触り、Windows Development Buildは未実施。

版管理した結果要約は [検証記録](validation/prototype-combat-2026-10-16.json)。詳細なローカル出力は `.uloop/outputs/PrototypeValidation/20261016-104954.json`。Game Viewは `.uloop/outputs/Screenshots/Game_20261016_104431_129.png`。RenderingモードのPNGはGame Viewウィンドウと色の見え方が異なったため、ウィンドウ画像でも確認した。

実装コミット:

- `3cb652b`: 敵・反撃・基本操作・4フェーズ攻撃の契約と自動テスト
- `3a1b153`: リセット/復活直後のInteract対象再取得と入力回帰
- `3ea7446`: 診断パネル、専用Scene、設定、参照検証、一括実行手順

元からあったSampleScene/BasicActionValidation、入力設定、Editor設定、要件・運用文書、素材等の未コミット変更は今回のコミット対象へ含めていない。ブランチは `issue/30-42-43-44-49-prototype`。プッシュ・PR作成・マージ・Issueクローズは未実施。
