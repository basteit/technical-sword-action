# Unityテストの実行方針（#38）

体験目的: ゲーム本体を壊していないかを短時間で確認し、変更していない撮影機能の大量確認に埋もれず必要な回帰を選べるようにする。

## 着手時の受入条件

1. 現在のPlayMode全22 Fixtureを用途別に分類し、通常の必須確認と変更領域別の確認をコマンドから選択できる。入力、状態排他、戦闘時計、4段攻撃、パリィ/反撃条件、移動、床抜け、安全中断/復帰の確認は削らない。分類漏れを検出する。
2. 実測で古くなった6件の期待値/計測時点を修正する。動画専用の旧3〜6秒枠に関する7件は受入用へ分離し、通常回帰から外した理由と現状未達を明記する。ゲームの設定をテスト都合で変更したり、全件合格に見せたりしない。
3. 普段用・今回の変更領域・テスト選択ルールを検証し、全体実行では除外した旧動画受入の状況も確認する。実行は常にsingle-flightとし、未保存Sceneの自動保存、タイムアウト後の重複実行をしない。手動15分/物理パッド/Buildの受入を自動合格へ読み替えない。

成功時: 変更箇所に必要な確認を選び、失敗した目的を説明できる。失敗時: 選択漏れや古い期待値を検出し、単なる失敗数の削減を品質改善と取り違えない。

## 分類と実行

ユーザーの追加依頼により、進行中の#84追調整と同じブランチ/PRでテスト運用#38を整理し、意図別にコミットする。分類の正本は`Tools/Tests/test-suites.json`。現在のPlayModeは22 Fixture。機能・安全性のテストは削除せず、実行対象を目的別に絞った。

- 毎回: Compile、EditMode全体、`daily`と変更領域のSuiteの和集合、Console、実画面のスモーク。同じケースを二度走らせない。
- 移動変更: `movement`。攻撃/パリィ/敵条件: `combat`。演出: `presentation`。SE: `audio`。
- 会話: `dialogue`。現行のプロトタイプには会話テスト配置を含めず、専用fixtureだけを作る。
- 動画ツール/撮影Sceneの変更: `video`。旧3〜6秒の受入判断: `video-acceptance`。
- Build前/マイルストーン: `all`。未達の動画受入も含めるため、既知失敗を隠さず記録する。

`daily`だけの合格は、触った領域・全体・製品QAすべての合格を意味しない。テスト本体の安全性/境界/FPS検証は保持し、普段の対象から外すだけで削除しない。

## コマンド

```powershell
# 普段の標準: Compile + 設定検証 + EditMode全体 + PlayMode daily + Console
powershell -ExecutionPolicy Bypass -File .\Tools\Prototype\Validate-Combat.ps1

# 変更領域を一回のPlayMode実行へ追加（例: 演出）。重複するケースは一度だけ実行
powershell -ExecutionPolicy Bypass -File .\Tools\Prototype\Validate-Combat.ps1 -AdditionalSuite presentation

# 移動だけの確認を単独実行。移動はdailyに含まれるため、daily合格後の再実行は不要
powershell -ExecutionPolicy Bypass -File .\Tools\Tests\Invoke-UnityTests.ps1 -Suite movement

# Unityを操作せず、選択対象/正規表現だけを表示
powershell -ExecutionPolicy Bypass -File .\Tools\Tests\Invoke-UnityTests.ps1 -Suite video -PlanOnly

# 分類漏れ/必須群の除外/旧動画受入の混入を検出
powershell -ExecutionPolicy Bypass -File .\Tools\Tests\Test-UnityTestPlan.ps1

# 旧動画の受入枠も含む全体。現状は旧6秒枠の7件が未達
powershell -ExecutionPolicy Bypass -File .\Tools\Tests\Invoke-UnityTests.ps1 -Suite all
```

ULoopは`uloop.ps1`ではなく導入済み`uloop.cmd`を使う。`-ULoop`で別のCLI配置を指定できる。正規表現の`|`をcmd.exeのパイプに解釈させないよう引用する。入力機器やSceneの未保存変更を自動保存せず、テスト前後に実行中/PlayMode/一時Sceneの復元を確認する。パラメータ化テストの親を選ぶと子ケースも入るため、動画の除外は親群の選択も抑止する。静的検証もXMLの祖先選択を模擬して検査する。

長い`all`はCLIの180秒上限後もUnity側で継続することがある。その場合は再実行せず、Unityの完了とConsoleの`Saving results to:`が示す最終XMLを確認する。途中のCLIエラーや前回XMLを最終結果として扱わない。スクリプトはテストを自動リトライしない。`Validate-Combat.ps1 -Focused`は従来互換の97件だけの短縮確認で、dailyの代用とはしない。

## 何を整理したか

| 対象 | 扱い |
| --- | --- |
| 本編の入力・状態排他・時計・攻撃/パリィ条件・移動・足場・安全復帰・受領素材 | `daily`の14群。毎回実行 |
| 大量のカメラ/VFX/FPS/向きの比較、攻撃比較モード、SE | 変更領域のSuiteで追加実行。削除しない |
| プロトタイプから撤去した会話テスト | 専用fixtureを`dialogue`で実行。普段のステージへ再配置しない |
| 撮影Scene・撮影ツール | `video`。NUnit UIでもVideoToolingとして分類 |
| 4段動画の旧3〜6秒受入7件 | `video-acceptance`へ分離。`all`には残す。現行6.25〜6.27秒のため未達 |
| キックの跳ね上がり2件 | 16FのHitstop解除後から吹っ飛びを計測するよう修正。Pause/対象固定/復帰の確認は維持 |
| タメ時間3件 | 旧8F固定ではなく現行の設定12→21Fとイベント差の一致を検証 |
| Justの旧期待1件 | 現行0〜3Fに合わせ3F/Just、4F・5F・9F/Normalへ整理。境界の確認を追加 |

旧動画7件を通常回帰から外すのは、撮影専用の尺の受入とゲーム本体の機能回帰を分けるため。動画を3〜6秒へ収めたことにはしない。[旧13件の診断](validation/known-playmode-failures-20261107.md)を参照。

## 検証結果

- 分類とフィルタの静的検証で、全22 Fixtureの登録/用途分類/クラス名、daily必須群、重複しない和集合、video/受入/allの包含と除外、親群からの除外漏れを確認。
- 新しい標準コマンドでCompile Error0/Warning0、設定Validator合格、EditMode343/343、daily287/287、Console Error0。初回の一連の実行は約77秒（範囲/環境による参考値）。
- 動画用267/267。最初の選択ではパラメータ化の親群から旧受入6件が混ざり273件となったため、親群の除外とXMLの祖先選択検証を追加し、267件だけでの全合格を再確認した。
- PowerShell 5.1で分類検証が合格し、`daily + movement`の和集合も重複なしの287/287として実行できた。
- `all`は949件中942合格・7失敗・Skip0。失敗は分離対象の旧動画受入7件だけで、古い期待値6件は解消し、新しい失敗は0。全件合格とはしない。CLIの180秒上限後もUnityの495/949・849/949の進捗を確認し、270.979秒での完了と最終XMLを確認した。`.uloop/outputs/TestResults/full-playmode-test-policy-20261107.xml`は生成物として非追跡。
- 最終949件のXMLでも全22 Fixtureと選択を照合し、PowerShell 5.1の静的検証163項目が合格。現在の件数はdaily287、movement97、input23、combat533、presentation296、audio38、dialogue19、video267、video-acceptance7、all949。Suite間の重複があるため合計しない。
- 未実施の物理パッド・15分操作・Build受入は合格にしない。終了時は入力/時計を復帰した未保存変更なしのPrototypeCombatValidation/EditModeへ戻した。最終Compile/ConsoleはError0・Warning0。保護対象の既存素材/設定等18ファイルはSHA-256不変で、要件の既存大規模編集は選択ステージから除外して保持した。
