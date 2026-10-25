# 接近/攻撃開始の前倒しと最後のタメ（2026-10-24）

2体目は冒頭から歩き始め、1体目へのコンボ中に攻撃準備へ入る。敵の攻撃モーションは12コマ/96戦闘Fを保つ。キックは蹴り出す直前の8コマ目を延長し、人物/独立電撃を同期させた。

## 実測した進行

| 場面 | 前版 | 今回 |
| --- | ---: | ---: |
| 2体目の接近開始 | 0.633333秒 | 0.016667秒 |
| 2体目の攻撃動作開始 | 1.383333秒 | 0.500000秒 |
| 1体目撃破 | 1.050000秒 | 1.050000秒 |
| パリィ | 2.966667秒 | 2.150000秒 |
| キック命中 | 4.300000秒 | 3.583333秒 |
| 全尺 | 5.166667秒 | 4.450000秒 |

初期配置/カメラは保持し、主2段テイクの接近速度を2.4→3.6unit/秒へ変更。攻撃開始を早め、口元の大きい発光→突き出し→パリィを全コマ見せる。攻撃の戦闘時間は不変で、開始マークから接触までの差は95tick（先頭コマを含む96F）。途中の斬撃停止も全員へ共通に掛かる。1体目撃破後からパリィまでの待ちは約1.92→1.10秒。

## 最後のタメ

ユーザー確認により対象は蹴り出す直前の8コマ目。従来の通常速度1動画Fから、**7動画F / 約0.116667秒**へ延長した。タメ2〜7コマの6〜7動画Fと同程度。うち6動画Fをスロー中に表示し、解除時の1Fも同じ8コマ目を保つ。次の戦闘tickで9コマ目の接触へ進み、振り抜きの速さは保持する。

タメ全体は実時間0.900→**1.000秒**。撮影用の解除21F→命中22F→接地/終了46Fに揃えた（前版20/21/45F）。解除→命中1tick、命中→接地24tickと後半10〜15コマ各4動画Fを保持。Slowのlease上限1.1秒は余裕で、実際は解除イベントで1.0秒時点に解放する。

共有の原稿/PNG/Clip/VideoArtLibrary、本編CounterCinematicProfileの33tick配分は保持する。撮影用ゲートへ1tick加え、`VideoPlayerAnimationDriver.CounterArtFrame` で元の8コマ目へ対応させる。`VideoCombatPresentation` の独立電撃も同じ対応を使い、後続コマは両方同時に1tickずらす。主撮影/攻防切替/Studioの保存設定とScene生成/再適用手順を揃えた。原稿版の追加登録はしていない。

## 確認

Unity Compile Error/Warning0、関連PlayMode **41ケース合格**（前倒し/全コマ/タメ/接触/電撃/Pause/Reset等38＋30/60/120fpsの完走/時間3）。左右各10回＝20回の通し/Resetも合格。全件テストや従来80回の組み合わせは再実行していない。

実進行267Fの人物/電撃/敵/時刻を記録し、全15キックコマ、8コマ目7動画F、表示中の電撃との一致、通常速度への復帰、同じEnemy_Bへの反撃、停止/消滅を確認。敵の吹っ飛びA距離0.80/高さ0.18、B距離1.60/高さ0.27、固定downコマ、強い揺れ/命中アップ、パリィ4F/Just5F・キック10F停止を維持した。

MP4は **267F / 4.45秒・1920×1080・60fps・無音**。全267Fデコード、連続画像で接近/攻撃/口元発光/パリィ/8コマ目/命中/着地を確認。最終Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-early-rear-charge-hold-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-early-rear-charge-hold-20261024.json) / [連続画像](previews/early-rear-charge-hold-20261024-contact.png) / [検証JSON](previews/early-rear-charge-hold-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision early-rear-charge-hold-20261024 -Variants 2stage-hybrid`。今回の調整と全体の最終受入は区別し、#69/#71/#72/#76はOpen、#73/#75はClosedを維持する。
