# 固定被弾ポーズで浮いて止まるノックバック（2026-10-24）

敵は小さく浮いて後退し、ノックバック終了後に停止してから消滅する。パリィ後も撃破時と同じ提出 `EnemySample_down_01` をその場で固定する。採用済みの構図/距離、強くした揺れと命中アップ、タメ、ヒットストップ、原稿は保持した。

## 動きとポーズ

主2段版の値は以下。実時間は停止を除いた戦闘時間で、両方18戦闘F＝0.30秒で終了する。

| 対象 | 後方距離 | 上昇量 | 終了後 |
| --- | ---: | ---: | --- |
| 1体目・2段目撃破 | 0.40unit | 0.12unit | 着地位置で静止→消滅 |
| 2体目・カウンター | 0.80unit | 0.18unit | 着地位置で静止→消滅 |

1体目は以前の小さい上昇を基準に後方距離を増やした。2体目は同じ軌道で少し高く、後方距離は1体目の2倍。横移動は従来の短い減速曲線、縦は小さい弧で、地上を滑り続けない。39Fの消滅処理より先に18Fで移動が終わり、フェード中の座標は変わらない。実在する壁の判定は維持する。

ノックバック中はdownコマを固定し、追加の回転や姿勢補間をしない。命中から主人公側を向き、停止中も同じコマ/位置を保つ。downの最下端を床に合わせる2原画pxの描画補正は維持。パリィ後も同じdownコマ/補正位置で静止し、ひるみ解除時は通常の描画原点へ戻る。chance原稿/Clip自体は保持する。

`VideoEnemyAnimationDriver.ApplySwordHitback` は非致死の従来量を保ち、撃破時だけ小さい弧を付ける。4段目の撃破距離は0.80unit。Directorの反撃は1体目の設定距離×2を使うため、4段比較でも関係を保つ。自由操作Studioの反撃は標準2段版の0.80unitを使う。本編の敵/CounterCinematicProfileや全体4段攻撃、受領Asepriteを変更しない。

## 検証

Unity Compile Error/Warning0、関連PlayMode **28/28**。初回24/28で、Animator評価前の旧コマが命中時に取り込まれる問題を検出した。接触時からdownを明示し、Animator評価後にも固定するよう修正し、同じ28件を再実行して全合格。左右の固定コマ/回転/向き、ひるみ解除時の原点、接触停止、有限軌道と停止、Pause/再開、通常/4段、VFX同期30/60/120fps、壁、Studioを確認した。

左右各10回＝計20回の通し/Resetで、二重命中/別対象、時間/カメラ/VFX/姿勢の残留がないことを確認。移動後はフェード中も位置が一定。Aの実測は距離0.40/高さ0.12、Bは0.80/0.18、比率2.0。パリィ後80動画Fは同じdownコマと位置/角度/向きが一定。全件テストや従来80回の組み合わせは再実行していない。

MP4は **310F / 5.166667秒・1920×1080・60fps・無音**。全310Fデコード。前版と全レイアウト/イベント種類/対象/戦闘tick/実時刻が一致。カメラは通常3.375/タメ2.8125/命中最小2.64375、パリィ4F/Just5F・キック10F、タメ0.900秒を保持。連続画像と実進行の位置/向き/透明度で、上昇→着地停止→消滅を確認。最終Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-enemy-knockback-arc-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-enemy-knockback-arc-20261024.json) / [連続画像](previews/enemy-knockback-arc-20261024-contact.png) / [検証JSON](previews/enemy-knockback-arc-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision enemy-knockback-arc-20261024 -Variants 2stage-hybrid`。今回の調整確認と動画全体の最終受入は区別し、#69/#71/#72/#76はOpen、#73/#75はClosedを維持する。
