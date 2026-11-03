# 敵の静止やられ・接地した撃破と命中カメラ（2026-10-24）

採用済みの中間レイアウト/距離を保ち、最新指示の5点をまとめて調整した。適用先は撮影Sceneと自由操作のStudio。本編の敵/カメラ/CounterCinematicProfileや受領Aseprite原稿は変更しない。

## 敵の見せ方

- パリィ後は提出 `EnemySample_chance_01` をその場で表示。従来の3tick後退/傾き→12tick復元を取り除き、root・描画位置・角度・向きを静止させる。中間コマや補間は足していない。主2段テイクでは80動画F保持される。
- 撃破後は提出 `EnemySample_down_01` の被弾ポーズで地面に沿って滑り、薄くなって消える。キック後の高さ0.65unitの放物線と追加回転を使わない。命中時から主人公側を向き、停止中は接触コマ/位置を保持。再開後はdownの最下端をidle/chanceと同じ床へ合わせるため、描画だけ2原画px下げる。
- 消滅まで39戦闘Fの移動を続ける。移動量は `t*(1.2-.2*t)` で緩く減速し、透明になる前に速度を0へ落とさない。実在する壁の判定は保持する。
- 1体目は従来の2段目 `.18unit / 8F` から、初速を保った撃破時専用の約1.46unit/39Fへ変更。通常の非致死ヒットバック量は保ち、4段目の撃破も元の初速に応じて距離を取る。キックは強い3.2unitを保ち、39Fの接地スライドへ変更した。

`VideoEnemyAnimationDriver.ApplySwordHitback` と `ApplyDefeatSlide` をDirector/Studioの両方から使う。以前の `ApplyLaunch` 自体は残し、壁の回帰も確認する。Pause/ヒットストップ中は戦闘時計と一緒に移動とフェードが止まる。

## カメラ

通常ortho3.375、カウンターのタメ2.8125、人物/敵の初期位置は維持する。読みやすさ設定のパリィは強度 `.145→.22unit` / 9F、キックは `.26→.38unit` / 0.30秒へ増やした。合成揺れの上限も `.28→.42unit`。純ドット比較側は `.135→.21` / `.24→.36`。方向と減衰を使う既存の単一カメラ制御へ接続した。

キック命中で2Fかけてortho2.64375まで寄せ、6F保持し12Fで通常へ戻す。タメの距離からorthoを6%縮める短い演出で、停止中にも進む実時間のカメラ演出とした。Pauseでは揺れ/アップの残り時間も止め、Reset/Disable/中断で即解除する。追加量は `VideoCombatPresentation.Impact Zoom Amount`（既定0.06）で調整できる。

通常パリィ4F/Just5F・キック10Fのヒットストップ、0.900秒スロー、主人公のコマ配分、斬撃/電撃、命中判定/対象固定/無敵は保持する。

## 検証と動画

Compile Error/Warning0。関連34ケースが最終的に合格。初回32/34の後、接近時の1px未満のY差と終了時の対象参照解放を考慮した期待値へ修正し2/2、命中中の向きを揃えた最終変更後に接触/停止/10回Resetの6/6を再確認した。30/60/120fps×左右で揺れ/アップ・Pause/再開/Reset、電撃と人物の停止、Studioの同一対象への反撃、壁の判定を確認。最終版で左右各10回、計20回の通し/Resetが合格。全件テストや従来80回の組み合わせは再実行していない。

実収録と同じ310Fの位置/向き/透明度/カメラを記録。パリィ後80Fの静止、両敵のフェード中の連続移動、接地/向き固定、カメラ最小2.64375と通常3.375への復帰、最大揺れ0.38を確認した。フェード中の1動画Fあたりの移動はA最小約0.031unit/B約0.067unitで、見えている間の急停止はない。

MP4は **310F / 5.166667秒・1920×1080・60fps・無音**。全310Fをデコードし、前版と全レイアウト・イベントの種類/対象・戦闘tick・実時刻が一致。連続画像でパリィ・命中・接地した移動/消滅、床の全幅を確認した。収録後Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。

[MP4](previews/combat-2stage-hybrid-enemy-reaction-impact-20261024.mp4) / [イベント](previews/combat-2stage-hybrid-enemy-reaction-impact-20261024.json) / [連続画像](previews/enemy-reaction-impact-20261024-contact.png) / [検証JSON](previews/enemy-reaction-impact-20261024-validation.json)。

再現はCombatVideoのPlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision enemy-reaction-impact-20261024 -Variants 2stage-hybrid`。本変更の接続/確認と動画全体の最終受入は区別する。#69/#71/#72/#76はOpen、#73/#75はClosedを維持する。
