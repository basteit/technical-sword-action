# プロトタイプのフリーSE差し替え（#92）

## 追加調整: 高い金属音と着地音の再選定（2026-11-07）

体験目的: 刀の空振りは鋭い風切り、命中は短い金属の切断音、パリィは澄んだ金属の余韻として聞き分け、着地だけが低く大きく浮かないようにする。

着手時の受入条件:

1. Kenney以外の公式配布サイトから、公開Gitと商用配布に使える素材を選び、作者・作品名・原音・ライセンス・加工と使用先を記録する。クレジット必須素材はビルドにも通知を含める。
2. 空振りに命中/弾きの金属衝突音を鳴らさず、実命中と同期パリィでだけ接触音を出す。着地を短い靴底の接地音へ替え、好評だったJump.wav・ジャンプ音量/ピッチを変更しない。
3. 音色とミックスの2軸だけを調整する。通常攻撃ORIGINAL、パリィ/反撃、停止、カメラ、判定を維持し、音源検証と関連EditMode/PlayModeテストを行う。聴感の最終採否は人が確認する。

同じIssue #92 / ブランチ / Draft PR #99の修正として進める。以下の初回Kenneyのみという素材方針とCC0のみという記載は初回の履歴であり、本調整の素材表を現在の採用として優先する。

### 現在の差し替え素材

| 用途 | 採用素材 / 変更 |
| --- | --- |
| 空振り1〜4段 | SoundBible「Sword Swing」Mike Koenig + Kenneyの薄い風切り/抜刀。金属の衝突音は入れず、命中より音量を抑える |
| 通常命中 / 4段命中 | SoundBible「Swords Collide」Sound Explorer / 「Swords Clashing」Mike Koenig。低いパンチ/肉切り成分を外し、短い高音の金属接触へ |
| パリィ / Just | 同上の剣同士の衝突を別配合。最初の1打だけを抽出し、高い余韻を0.65秒まで残す。火花と停止のインパクト時だけ鳴る |
| 着地 | BigSoundBank「Footsteps, Shoe on Concrete」#0514、Joseph SARDIN。単独の靴底接触を0.18秒へ加工し、低音を削る。音量0.27→0.22 |
| ジャンプ | Jump.wavはSHA-256 `9e923b4a2ffe056a4eb271416676f11c95fb141a7e87f74a26822a260f882c7c` のまま。音量0.25・ピッチ1・変化幅0.025も維持 |

SoundBibleの3作品は各作品ページでCC BY 3.0を確認した（[Sword Swing](https://soundbible.com/1176-Sword-Swing.html)、[Swords Collide](https://soundbible.com/1980-Swords-Collide.html)、[Swords Clashing](https://soundbible.com/857-Swords-Clashing.html)）。作者・題名・出典・[ライセンス](https://creativecommons.org/licenses/by/3.0/)・加工表示を保持する。BigSoundBankは[作品ページ](https://bigsoundbank.com/footsteps-shoe-on-concrete-s0514.html)と[利用条件](https://bigsoundbank.com/licenses.html)でCC0と商用/再配布を確認した。OtoLogicは取得制限があり今回採用しなかった。

原音と条件は `ThirdParty/AdditionalAudio`、全24音の現行ハッシュ・加工レシピは `ThirdParty/PrototypeAudio/manifest.json` に統合した。`Tools/Audio/Import-PrototypeSfx.ps1` はGit同梱の選択原音だけで再加工できる（FFmpegは別途必要）。旧Kenney専用manifestは統合先へ移し、古いハッシュを現行と誤認させない。旧パス互換MP3のうち命中/弾き3本も今回の金属音へ更新する。

必須クレジットは `Assets/StreamingAssets/AudioCredits.txt` に置き、Unityの通常ビルドでData/StreamingAssets以下へそのまま同梱する。今回配布ZIPの再作成は行わない。ジャンプ以外の変更は上表7音と音量/一部ピッチだけで、反撃の蹴り・帯電、敵の口攻撃、足音などは保持した。

## 体験目的

現在の各モーションと、空振り・命中・弾き・反撃・被弾の違いを音でも確認でき、繰り返しても主要な接触音が埋もれない試遊版にする。

2026-11-07のユーザー依頼により、既存SEを追加で重ねずフリー素材へ差し替える。候補調査だけだった `docs/playtest/prototype-se-candidates.md` とは別の採用・実装記録とする。

## 着手時の受入条件

1. 出典、CC0ライセンス、取得日、原音名・ハッシュ、加工方法、使用先を記録し、旧4音の録音内容を置き換える。公開Git管理と商用ゲームへの同梱を可能にする。
2. 歩行、ジャンプ、着地、ダッシュ、4段の空振り/命中、パリィの構え/成功/失敗、反撃の転移/帯電/蹴り/命中、被弾/死亡、敵の攻撃/撃破と、既存の回復/必殺/インタラクトへ接続する。接触音は実際の成立時だけ、パリィ音は同期インパクト時だけ発音する。
3. 重複Collider/複数敵の命中で過剰に重ならず、Pause、被弾、死亡、対象消失、中断、Reset、Disable、Scene変更と音量0%で不要な音が残らない。関連自動テストとUnityのコンパイル/Consoleで確認する。

成功時は金属の弾きと低い反撃命中で報酬を強調する。失敗時は空振りの動作音と小さな解除音に留め、成功したように聞かせない。音量0%でも現在の視覚情報と判定は保持する。

## スコープと既存作業の保護

- 関連Issue: [#92](https://github.com/basteit/technical-sword-action/issues/92)。本編40SE、BGM、ボイス、CRIWARE導入は対象外。
- 基点: `bbb8b7f`、依存する未統合PRは [#98](https://github.com/basteit/technical-sword-action/pull/98)。既存の最新パリィ/反撃を維持するため、その先端から同じ作業フォルダ内で `issue/92-free-sfx` を作る。既存ブランチの改名、rebase、別worktreeは行わない。
- 大きな既存未コミット差分（原稿、SampleScene、BasicActionValidation、要件ベースライン、試遊README等）は保持し、今回のコミットへ含めない。
- 今回の調整軸は音色/音量と発音タイミング。モーション、速度、受付、判定、ダメージ、停止時間、敵の予兆とカメラは変更しない。
- 音源はKenneyの公式配布物を使用する。効果音ラボ/OtoLogicの候補は採用しない。
- 旧 `Assets/Audio/HitSound_*.mp3` は別Sceneでも参照されるため、GUIDとパスを維持してCC0音源へ内容を置換する。元の録音は現行Assetから除かれるが、Git履歴の改変・消去は行わない。
- 未提出の3/4段アニメーションを完成扱いにしない。現行4段の有効フレームへ個別の動作音を合わせ、将来の3連斬り用の多段判定や固定尺の連続SEを勝手に追加しない。

## 追加依頼: 通常攻撃を遅めにする（#34）

2026-11-07の同一作業中の明示依頼により、通常攻撃の既定だけを保存済みORIGINALへ戻す。SE接続確認との一括作業の例外として記録する。速度以外の既存設定は戻さない。

| 段 | 命中F: FAST → 今回 | 全体F: FAST → 今回 |
| --- | --- | --- |
| 1 | 12 → 15 | 30 → 38 |
| 2 | 9 → 12 | 24 → 32 |
| 3 | 12 → 15 | 30 → 38 |
| 4 | 30 → 38 | 49 → 62 |

再生速度は直前FAST比でおよそ20〜25%低下する。通常攻撃の定義と専用Animator Controllerを一緒に戻し、当たり判定と絵の同期を保つ。横リーチ、威力、移動、1/2段8F停止、敵の96F予兆、パリィ・反撃・カメラは維持する。F10のFAST/SNAPPY比較は削除しない。SEの振り抜き時刻は現在選択中の攻撃定義から算出する。

## 素材

- [Kenney RPG Audio](https://kenney.nl/assets/rpg-audio)
- [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds)
- [Kenney Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds)
- 各パックは公式ページと同梱License.txtでCC0を確認した。商用利用・加工・再配布可、クレジット任意。[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
- 取得日: 2026-11-07（日本時間）。原音・同梱ライセンスは `ThirdParty/KenneyAudio`、使用音は `Assets/Audio/PrototypeFreeSfx`、再加工手順は `Tools/Audio/Import-PrototypeSfx.ps1` に分離する。

## 検証記録

2026-11-07（日本時間）、Unity 6000.3.9f1 / ULoop 2.1.3、同じ作業フォルダで確認した。

- Compile: Error 0 / Warning 0。
- EditMode: `PrototypeSfxAssetTests` 10/10、`PrototypeCombatTrialAssetTests` 14/14。
- PlayMode: `PrototypeSfxTests` 29/29、`PrototypeCombatTrialTests` 65/65、`PrototypeCounterCinematicTests` 185/185。関連303件すべて合格。全プロジェクトの全テストを実行したという意味ではない。
- 24音のPCMをデコード・計測し、48kHz/mono、ピーク約-6dBFS、原音/出力ハッシュとUnityでの参照を確認した。プロトタイプから旧 `HitSound_*.mp3` パスへの依存は0件。
- 通常PlayModeの反撃Charge中に実際のAudioListener出力を測定し、非ゼロの波形（サンプルピーク0.01709072）、転移/帯電の2イベントとループ再生を確認した。自動確認でUnityが背景になる間だけ `Application.runInBackground` をtrueとし、元のfalseへ復帰した。保存済みプロジェクト設定は変えていない。
- 最初のテストでは、入力ルータ無効化と同じ描画フレームで入力しない既存ガードをテストが守っていなかったため修正した。未登録の仮敵は即時パリィ表示へフォールバックするので、同期テストは受領済み汎用敵の実攻撃で確認した。死亡時のSessionResetと反撃着地で発生し得た二重発音、対象消失時の帯電残留を修正後、上記が合格した。
- Scene保存により、既存の常時アート表示がすでに適用していた主人公Sorting Order 20もシリアライズされた。今回新しく前面化した変更ではなく、見た目は従前と同じ。

未実施: 人によるキーボード/物理ゲームパッド各15分の聴感・操作感確認、ヘッドホン/スピーカー比較、BGM併用、Development Build、既存配布ZIPの再作成。人の最終採否を自動テストの合格で代替しない。#92はUI音や出展範囲の残りも含むため、この作業だけでCloseしない。

人による確認は、(1) 空振り/命中の違いと4段の音量、(2) 火花と弾き音、(3) 転移→帯電→蹴りの同期、(4) 短い連戦で耳に痛くないか、(5) F9無音/復帰とEsc一時停止、(6) 遅くした通常攻撃が重く感じるか、を中心に行う。

## 接続と調整

本編プロトタイプSceneの `PrototypePlayer / PrototypeSfx2D` から `Assets/Settings/PrototypeCombat/FreeSfx.asset` を参照する。Clip、個別音量、ピッチと微小な変化、優先度をInspectorで変更できる。F9の既存Master/SFX設定へ接続し、保存アセット自体をPlay中に書き換えない。

| 用途 | 使用音 | 発音位置 |
| --- | --- | --- |
| 足音 | FootstepA/B（concrete、交互） | 接地中・通常歩行の実移動1.3unitごと。立ち止まり/空中/攻撃移動では鳴らさない |
| ジャンプ/着地 | Jump（cloth3）/Land（soft impact） | 受理された踏切/実接地。床抜けはジャンプ音なし |
| ダッシュ | Dash（knifeSlice2＋cloth2） | 実際の開始に1回。地上/空中共用 |
| 攻撃1/2/3/4 | SwordA / SwordB / SwordAの音色差 / SwordIai | 各段のActive開始2F前。空振りでも動作音のみ再生 |
| 通常命中/4段命中 | LightHit / HeavyHit（chop＋打撃） | 同一攻撃の実命中。同tickの多重Collider/複数命中は音を積まない |
| 納刀 | Sheathe（metalLatch＋clothBelt2） | 実際に復帰へ進んだ後半。次段へ接続した場合や中断時は予約しない |
| パリィ構え/失敗 | ParryReady / ParryMiss | 開始/失敗時。小さな構え・衣擦れで成功の金属音と区別 |
| パリィ成功/Just | ParryClang（金属＋plate） | 敵の攻撃ピークへ同期した表示インパクト。Justは小さな音色差だけで二重に重ねない |
| 反撃転移/帯電 | CounterTransfer / CounterCharge（forceField） | BlinkStarted / ChargeStarted。帯電だけループし、解除/中断/対象消失で止める |
| 蹴り出し/反撃命中 | Dashの短い音色差 / CounterImpact（重打撃＋低音＋薄い電気） | ChargeReleased / 実際のHitConfirmed。転移と命中を混同しない |
| 被弾/死亡 | PlayerHit / Fall | 実ダメージ/死亡状態。死亡音を毎tick再開しない |
| 敵の予備動作/攻撃/不可予兆 | EnemyWindup / EnemyAttack / Danger | 既存の開始イベント/不可攻撃の合図区間。通常敵へ追加の黄色VFXは付けない |
| 敵撃破 | Fall（弱音量） | 撃破成立。同tickの斬撃/反撃命中より低優先度 |
| 回復 | Jumpの小音量 / Heal | 開始/実際の回復適用。適用前中断で回復音を出さない |
| 必殺 | CounterTransferの音色差 / HeavyHit | 開始/実命中。空振りに命中音を付けない |
| インタラクト | Interact（metalClick） | 実際の受理時に1回。会話ボイス・文字送り連続音は追加しない |

待機、上昇、頂点、下降、後ずさり、フェード、F1/F2リセットには独立した音を付けず、モーションの切替ごとに音が連打されることを避ける。重パリィ専用の追加音は未制作とし、既存成功音へフォールバックする。

`CombatFeedback2D` の視覚・停止・カメラはそのまま、発音だけを新しいバスへ委譲する。旧Attack/Special/敵予兆の直接再生は、このバスが有効なときだけ抑制する。バスは動作4声＋接触1声＋帯電1声で上限6声とし、個別Clipを約-6dBFSへ正規化、同時ゲインの合計を1.6以下へ抑える。パリィや反撃の音は同tickの弱い音より優先し、スローで音のピッチを変更しない。

Pauseでは自分が再生中だった音だけを一時停止/再開し、被弾・死亡・中断では帯電と動作予約を止める。SessionReset、Disable、Scene変更では全専用音源を停止する。再開/復活時の初回接地を着地として誤発音しない。
