# Issue #47: 60Hz戦闘時計の実装・検証記録

## 版と作業範囲

- 作業日: 2026-09-05、Unity 6000.3.9f1。
- Issue: https://github.com/basteit/technical-sword-action/issues/47
- 引き継ぎ: https://github.com/basteit/technical-sword-action/issues/47#issuecomment-5551394234
- 着手時main: `6efbeddaabad33a0d48c4e31cae7d5e7a170ff7d`。
- 先行PR #63は2026-09-05にマージ済み。現在の基点は統合後mainの `e9f059ce88a954cfa561ed616f0b851fc3eae815`。
- `issue/47-combat-clock`の3コミットを統合後mainへ載せ直し、同じcheckoutで再検証した。新規worktree・別checkoutは作成していない。純C#時計は独立し、実Componentへの接続は#40の中央状態契約に統合している。
- 規則はCONTRIBUTING.md、内容は現行要件3.4・3.6・12.1、design-summary-guide、9月末ロードマップ、development-plan、completion-roadmap-2026-09-05、Issue本文・引き継ぎを参照した。ローカルの未コミット要件文書を最新仕様として読んだが、この変更には含めない。
- #40の共有B中央APIとアダプタを含む状態契約を維持する。Heal・標準入力Router・Cancel設定・撃破スロー効果は追加していない。

## 時間の所有権

`CombatClock`はUnity非依存の60Hz時計。unscaled秒を積算し、1/60秒単位で実行する。Pauseはcatch-upを蓄積しない。ヒットストップは所有者ごとの残りFを最長値で合成し、同じ所有者の短い要求で長い停止を短縮しない。死亡・復旧時もtick番号は巻き戻さない。

`CombatTimeController`はPlay開始時に自動生成する。Sceneへの参照追加は不要。Time.timeScale、Time.fixedDeltaTime、Physics2D.simulationModeの書込みはこのクラスに集約する。固定時間は1/60秒、2D物理はScript modeで同じtick内から1回ずつSimulateする。時計Disable時は所有開始前の設定を復元する。

実行順:

1. Dynamic Updateで入力を採取し、次のtickまでラッチする。
2. Motorの接地・向き確認、Stateの合法遷移・キャンセル調停、Motor速度適用、敵の行動・移動を行う。
3. SyncTransforms、Physics2D.Simulate(1/60秒)で物理移動する。
4. 敵の物理後Hit、Specialの発生・資源処理を行う。Specialは敵の被弾処理より後で、発生tickの被弾による取消を優先する。
5. 登録Animatorを手動で1/60秒評価し、既存攻撃のAnimation Eventを同じ時計で処理する。
6. Action・無敵・予約・敵の残り時間を減らす。秒指定値はFへ切り上げ、反復誤差による1F延長を防ぐ。

tick中の停止要求は次tickから効き、現在tick内の後続判定・資源処理を途中で欠落させない。tick中に生成した弾のlistener参加は次tickからとし、その間の物理接触を保持する。弾の破棄は即時に非アクティブ化し、描画末尾のDestroy待ち中に別tickで再Hitさせない。

既存Clip/Eventとfallbackの契約は維持する。#48の設定表への置換、全攻撃の判定収集・相打ちを含む戦闘仕様全体の受入を、この時計テストだけで認定しない。

## 入力・停止API

| API | 契約 |
| --- | --- |
| `CombatTimeController.SetPaused(bool)` | Pauseの切替。Esc / MenuのInputActionからも呼ばれる。会話中はSkipが優先され、通常のPause入力は開始しない |
| `RequestHitstop(owner, seconds)` | 所有者付き停止要求。実行中tickを完走後に停止する |
| `ReleaseOwner(owner)` | その所有者だけを解除。別所有者やPauseは解除しない |
| `ResetSession()` | 停止要求を解放。tick内の復旧は残りcatch-up時間を失わない |
| `RequestDefeatSlow(source)` / `DefeatSlowRequested` | 後続向け通知入口だけ。倍率・継続時間・効果なし |
| `ICombatTickListener` | 登録された行動・移動処理。OnEnable/OnDisableで登録・解除 |
| `ICombatHitListener` / `ICombatTimerListener` | 物理後の判定、tick末尾の時間更新 |
| `BeforeCombatTick` / `AdvanceFrame` | tick番号付き再生・検証の入口。実機入力Routerへの接続は#41 |

Stateの予約はAttack/Jump=6F、Dash/Parry/Special/Heal=4F、Interactは次tickのみ。合法な1件が成立したら同時要求群を消費し、落選要求を後から発火させない。

Pause中は既存予約の残量を保持し、新規Gameplay予約を作らない。解除時は現行要件12.1に従い予約を破棄し、同じ描画フレームの入力も抑止する。ヒットストップ中は新規押下を採取し、残量減算・Action成立を止め、解除後のtickで1件だけ成立させる。攻撃中の独自コンボ予約も同じ停止規則に従う。

揺れはunscaledDeltaTime、デバッグUIのFPS表示はunscaled時間で更新する。時計tick・Pause・残りヒットストップFも表示する。通常カメラ追従の独立実装は現行Scripts内にはなく、将来追加時は戦闘時計へ接続する。

## 検証

再現コマンド（プロジェクトroot）:

```powershell
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode
uloop run-tests --test-mode PlayMode
uloop get-logs --log-type Error
uloop get-logs --log-type Warning
```

### #40統合後の最終結果（2026-09-05 13:32 UTC）

- コンパイル: Error 0 / Warning 0。
- EditMode: **172 / 172成功**。
- PlayMode全体: **20 / 20成功**（時計10件、状態契約10件）。
- 実行後Console: Error 0 / Warning 0。
- ユーザー指定によりDisable・死亡・Scene遷移・時計設定復元など同条件の反復を各10回へ短縮。127通りの入力組合せと180tickの時間列比較は維持した。
- #40で追加されたB共有要求にもPauseの入力拒否、停止中の選択対象保持、予約期限切れ後の再入力を適用し、回帰テストを追加した。Scene変更時の一括リセットはTime Controllerへ集約し、二重通知を避けた。
- 下記の初期検証記録は統合前の履歴。現在の合否は上記の結果を参照する。

### 初期検証の履歴

- EditMode: 169 / 169成功。既存State resolverと純時計テストを含む。
- PlayMode初期7件: 7 / 7成功（2026-09-05 11:36:59 UTC）。停止中予約、物理・実Animator・ダメージ・ゲージ、Disable/死亡/Scene遷移各100回、時計Disableの設定復元100回、3秒/30秒の整数F境界を確認。
- fps分割テストは30/60/120の各frame秒を同じdriverへ渡し、180tickの状態・生死・HP・Hit・ゲージ・位置・無敵・ロック・Break・Animator状態と時刻の列を比較する。実Animation Eventで敵に2Hit、プレイヤー被弾2回、HP3、敵HP48、ゲージ24、timeout fallback 0を必須とし、ゼロHitの空回しでは合格しない。
- 統合前PlayMode: 10 / 10成功（2026-09-05 11:41:44 UTC）。実描画ループでvSyncを0、targetFrameRateを30/60/120に設定し、180tick終了までの181個のsnapshotを比較するテストを追加。攻撃中コンボ予約のPause保持・解除破棄、ヒットストップ中の保持と解除後の単一queueも成功。
- この実描画テストは設定上限を変更したUnity Editor PlayModeであり、実機Bindingの録画や最低環境の性能計測ではない。時間・数値の厳密比較はframe分割テストで保証する。
- 最終PlayMode後のConsole: Error 0 / Warning 0。
- SampleScene実動スモーク: 通常再生で60Hz/Script物理、Idle表示を確認。Pause中に300ms待ってtickは1408のまま、Attack要求はfalse、揺れタイマーは0まで進行した。解除後はtick1422、timeScale=1、停止owner=0へ復帰。終了後はEditMode、保存済SampleScene、timeScale=1、fixedDeltaTime=1/60へ戻した。
- 表示証跡（ローカル生成物、コミット対象外）: `tmp/issue47/Game_20260905_204314_869.png`、`tmp/issue47/Game_20260905_204354_430.png`。
- ユーザー手動確認: 提示したSampleSceneの操作確認項目に対して「問題ないです」との報告を受領。所要時間・試行回数・パリィ成功率などの実測値は未申告のため補完しない。

## 既存編集の保護と残る受入

着手前に未コミットファイルのSHA-256をローカルの`tmp/issue47/baseline.json`へ記録した。Scene以外の素材・AnimationClip・Animator Controller・文書等はその内容を維持する。PlayerAttack2Dの既存4か所のfallbackDuration調整は作業コピーに保持し、コミット対象のコードから分離する。

SampleSceneの未保存内容はユーザーの明示許可を受けて保存した。Sceneは今回のコミットに含めない。テスト用Sceneはテスト内で生成・解放し、ユーザーのSceneを破棄しない。

統合後の確認では、ユーザーの素材・Scene・文書等のハッシュ一致を確認した。攻撃時間の4か所の調整も保持し、今回のPR差分には含めない。テストは未コミットのユーザー変更を保持した作業ディレクトリで実施しており、クリーンcheckoutそのものの実行結果ではない。

後続Issueでの受入:

- #41の標準Binding・実入力記録再生を通した検証。本テストは中央APIへtick単位に同じ要求を渡すため、まだ未実装のRouterや実機Binding全体の証跡ではない。
- SampleSceneの提示項目についてユーザー確認は問題なし。長時間試験の所要時間・回数・定量指標は未記録。既存4段目のfallbackとClip時刻の調整を時計実装へ混ぜない。
- 敵全アーキタイプ、同tickの相打ち・全判定収集、全入力・全Actionの組合せを含む本番回帰。今回の成功は時計の代表統合試験であり、後続Issueの受入を置き換えない。

Issue close、自動マージ、mainへの統合は実施しない。
