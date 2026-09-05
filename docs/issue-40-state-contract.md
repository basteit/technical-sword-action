# Issue #40 中央状態契約の検証

2026-09-05 / Unity 6000.3.9f1 / PR #63

## 実装

- Life / Action / Locomotionを分離し、中央Controllerで開始・完了・中断を管理する。PauseはAction外の通知として最優先する。
- `RequestSharedDashInteract()` は呼出時の有効な対象を固定し、対象ありならInteract、対象なしならDashの一要求に解決する。同じ未処理要求への重複呼出は一度だけ採用する。Interact拒否や対象消失からDashへ変換しない。
- 文脈解決後の独立要求は `Dash > Parry > Special > Attack > Heal > Jump > Interact` の順で合法な一件を採用する。
- Defense/Late受付窓、資源不足時のSpecialからJumpへの選択、両者不成立時のAttack継続を中央判定する。
- 状態を離れる際に、その状態が所有した衝突無視を解放する。ParryCounterへの移行で旧Parryのロックを解除する。Disable・死亡・Active Scene変更では予約、実行中Action、時間効果等をリセットする。
- B解決結果と対象IDを公開し、既存デバッグ表示に解決結果を追加する。

## 再実行

未保存Sceneは保存してから、一度に一つずつ実行する。

```powershell
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode
uloop run-tests --test-mode PlayMode --filter-type assembly --filter-value TechnicalSwordAction.PlayerState.PlayModeTests
```

| 検証 | 最終結果 |
| --- | --- |
| コンパイル | Error 0 / Warning 0 |
| EditMode | 157 / 157 成功 |
| PlayMode | 9 / 9 成功 |
| 実行後Console | Error 0 / Warning 0 |

ユーザー指定により、同条件の反復を100回から**各10回**に短縮した。異なる条件を網羅するNeutral要求127組合せは維持している。

PlayModeはB対象あり・なし、重複要求、状態拒否・対象無効化・範囲離脱、全127組合せ、キャンセル窓、Special/Jumpのフォールバックを検証する。接続済みActionとHealアダプタの中断、ParrySuccess/Fail/Counter、ヒットストップ中のDisable/死亡、Scene切替は各10回実行する。実行中コンポーネントの重複、速度、無敵タイマー、入力予約、衝突無視、時間倍率の残留を確認する。自然完了も別途確認する。

## 検証の範囲

- テストは専用GameObjectとSceneを生成し、既存SampleSceneの配置に依存しない。接地状態と候補Colliderの出入り、受付窓はテストから注入する。物理境界や実機入力の操作感の確認とは分ける。
- Heal / ParryCounterはEditor内のアダプタで中央契約を確認する。これらのゲーム内機能全体の完成を意味しない。
- 実入力の割当は#41、戦闘時計は#47、窓データは#48、Heal等は#49で扱う。本PRへ#47の時計実装は含めない。
- テストはユーザーのScene・アニメーション・攻撃時間調整等を保持した作業ディレクトリで実行した。クリーンcheckoutでの再実行ではない。これらのユーザー変更はPRの今回の追加コミットから除外する。
- 初回はPlayMode用asmdefの分類設定、およびテスト内の対象選択更新不足で失敗した。両方を修正し、上表の最終実行では全件成功している。

中央APIの自動検証は完了。マージにはGitHub側の必須レビュー条件を満たす必要がある。
