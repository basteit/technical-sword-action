# 右寄せ構図と背後から歩いてくる敵（2026-10-19）

主動画の2段テイクでは、主人公と敵の組を右へ1unit移す。背後の敵は遠い位置から歩き、右の敵への1段目命中後に近づき始める。右の敵を2段目で倒した後、背後の敵の実攻撃をパリィし、その個体へ反撃する。

## 構図と進行

- 通常方向の開始位置は主人公x=1、右の敵A x=2.85、背後の敵B x=-2.20。Bだけは従来位置よりさらに左へ1.6unit離す。左右反転テストでは配置全体を反転する。
- Aへの最初の斬撃が当たった次の戦闘tickにBの追跡を解放する。移動速度2.4unit/秒、攻撃距離1.6unitで、通常AIの接近・障害物確認・予兆・攻撃・被弾判定を使う。Bを画面内の所定位置へ瞬間的に置き直す処理は使わない。
- A撃破後にBの攻撃を弾き、同じBの背後へ移動してキックする。移動前後で、主人公の反撃用キャンバス全体に左右の余白があることを30/60/120fps×両向きで検証する。
- タメ0.9秒、20F解除→21F命中→45F接地、命中後0.533秒、更新キック全15コマと目の残光は維持する。
- 今回の選択2段テイクだけへ適用する。任意4段比較・接近歩行比較・攻防切替・自由Studio・本編の配置と進行は従来のまま。

`EnemyMeleeChaser2D.BeginCaptureApproach` は追跡速度/攻撃距離/好適距離の一時設定を保持し、`ResetForCapture` で設定を復元する。通常AIの設定やプロトタイプの反撃へ今回の演出値を持ち込まない。

保存済みのSceneから `Tools > Technical Sword Action > Video > Apply Rear Entrance Layout` で主撮影Sceneだけ更新できる。アートやClipの再生成は不要。従来の撮影Scene再生成と提出素材適用からも同じ配置を設定できる。

## 成果物と再現

実装は `859153b`、基点は `fa800ea`。同じ作業フォルダの `issue/33-52-38-combat-polish` で進めた。

- [MP4](previews/combat-2stage-hybrid-rear-entrance-20261019.mp4)：**242F / 4.033333秒、1920×1080・60fps・無音**。H.264、音声stream 0、全242Fをデコード済み。保存版CRF16、チャット内プレビューは同解像度/fps/全コマのCRF20。
- [イベント表](previews/combat-2stage-hybrid-rear-entrance-20261019.json) / [流れの確認](previews/rear-entrance-20261019-contact.png) / [反撃姿勢](previews/rear-entrance-20261019-poses.png) / [検証JSON](previews/rear-entrance-20261019-validation.json)。
- Slash1 0.583333秒 → B接近開始0.633333秒 → Slash2/A撃破1.050000秒 → B予兆1.383333秒 → パリィ1.866667秒 → タメ2.250000秒 → 解除3.150000秒 → 命中3.166667秒 → 接地3.700000秒。
- Bはx=-2.20から約-0.56まで実際に歩き、6枚の移動絵を使う。待機状態から遠隔で攻撃だけを発生させない。主人公は反撃移行までx=1でその場の斬撃を続け、最後の待機位置はx≈-2.3725。
- 全15キックコマを表示。1〜3の合計0.450秒、1〜7の合計0.900秒、9の既存停止を含む9動画F、10〜15の各4動画Fを保持。命中後の倍率は1で、追加スロー/停止はない。

保存済みSceneへレイアウト更新メニューを実行し、`Assets/Scenes/CombatVideo.unity` をPlayする。`VideoCaptureUtility.CapturePreview(2,true)` で連番とイベントを収録し、`Tools/Video/encode-captures.ps1 -Revision rear-entrance-20261019 -Variants 2stage-hybrid` で再出力できる。HP・命中・パリィ・死亡を直接成功させる処理は使わない。

## 検証と保持

- Unity compile Error 0 / Warning 0。全体PlayMode410件の初回は409件合格、旧配置を前提とした手動接触Fixtureの1件が失敗した。接触元を実際の近接範囲へ置き、パリィ成立のAssertを追加した後、**撮影統合164/164**を再実行して合格。他246件は初回の全体実行ですべて合格。途中の結果も検証JSONへ残す。**EditMode196/196**合格。
- 新規8件は30/60/120fps×左右の歩行・描画・初撃/接近/正面撃破/予兆/パリィ/同じ対象への蹴りの順序、反撃キャンバス全体の左右余白、歩行中Pause/ResetとAI設定復元を確認した。
- 60fpsの実測で反撃キャンバスの最小水平余白は画面幅の約4.9%。アップ/揺れ中も画面内に収まる。Game Viewで実歩行と終了後の待機構図も確認した。
- 通常/攻防切替それぞれ2/4段×左右×各10回＝**計80回Reset**。二重Hit、誤対象、操作不能、時間/カメラ/暗転/目/AI/VFX/重力/吹っ飛び等の残留なし。通常2/4段は4.033333/5.616667秒、攻防切替2/4段は4.883333/5.916667秒。
- 収録後Console Error 0 / Warning 0。`PrototypeCombatValidation.unity` へ保存済み・未保存変更なしのEditModeで戻した。
- 記録更新前の基準2121ファイルを照合。今回の実装6ファイルと、別チャットで明示的に調整中の文書2点だけが変わり、2113ファイルはバイト一致。既存のBasicActionValidation/SampleScene/入力/Editor設定差分、原稿・PNG・Clip・過去動画を保持。別チャットの `docs/prototype-player-art.md` と現行要件の編集を今回へ取り込まない。

## 動画関連Issueの残件

| Issue | 状態 | 現在の残件 |
| --- | --- | --- |
| [#70](https://github.com/basteit/technical-sword-action/issues/70) 攻撃 | Open | 提出1/2段の接続・2段撃破・その場の斬撃は実装済み。見え方と接触の最終確認。後ろ髪・復帰の追加作画を今回の必須条件へ戻さない。 |
| [#71](https://github.com/basteit/technical-sword-action/issues/71) 背後パリィ | Open | 待機/成功軽・実攻撃元への振り向き・対象固定は実装済み。弾く手応え、刀/敵武器/火花の見え方の最終調整。今回は軽のみで、重の作画を完了待ちにしない。 |
| [#72](https://github.com/basteit/technical-sword-action/issues/72) 反撃 | Open | 移行・更新キックの受領と接続は済み。背後位置と接触、後半の緩急を含む最終確認。原稿未受領という古い記載は現在の残件ではない。 |
| [#74](https://github.com/basteit/technical-sword-action/issues/74) VFX | Open | 足と敵の接触を隠す衝撃波の整理、目の残光と足の雷の強弱、パリィ/通常Hit/強Hitの見分けと完成品質の受入。 |
| [#76](https://github.com/basteit/technical-sword-action/issues/76) 撮影 | Open | 今回の構図と歩行接近を含めた最終構図・尺・再現・録画版の固定。候補動画の出力と、最終版の確定を区別する。 |
| [#69](https://github.com/basteit/technical-sword-action/issues/69) 全体 | Open | 子Issueの受入がそろった状態で、動画全体の完成判定と最終成果物の固定。 |

[#73](https://github.com/basteit/technical-sword-action/issues/73) スロー/カメラ基盤、[#75](https://github.com/basteit/technical-sword-action/issues/75) 敵のモーション基盤はClosed。今回も再利用して確認する。

本編4段の追加清書、重パリィ、全24モーション、SE/BGM、ボスや遠隔敵、プロトタイプ全体の受入を、この動画の完成待ちへ加えない。
