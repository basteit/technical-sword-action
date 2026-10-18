# 作画済みキックの統合（2026-10-19）

受領した `Player_counter_kick.aseprite` を、従来のSampleシルエットから人物・青白い足・電流・大きな軌跡のある版へ差し替える。全15コマを使い、1〜3コマ目も含めてタメを見せる。反撃対象は弾いたEnemy_Bのまま、背後への移動・無敵・1回命中・大きな吹っ飛びを保つ。

## 原稿と表示

- 編集用現在版：`ArtSource/Player/Current/Player_counter_kick.aseprite`。
- 新版SHA256：`a763a95f3ff125bb379c8edee7f2b15c360c8afe6810edc710b1cdfae7898862`。`register --id counter_kick` で受領済みとして登録し、旧版 `705817e7…` をHistoryへ保持した。原稿登録とUnityの使用版記録は分ける。
- 128×128px・15コマ、1〜10コマ各100ms／11〜15コマ各60ms＝原本1300ms。Body／Effectを表示、Sword／Hair／Sampleを非表示のまま保持。Sampleの不透明度105も原本どおり。原本のレイヤー・表示時間・色は変更しない。
- Unity側アーカイブ：`Assets/Art/Player/VideoSubmitted/20261019/`。別名レビューはガイドLayer 1だけを隠し、全15コマへCounterKickタグを付けて保存・再読込した。今回のBodyは孤立参照点を除去する必要がない。
- Unity PNGは既存の `VideoSubmitted/CounterKick_01〜15.png` と同じGUIDを使う。32PPU・Point・無圧縮・Pivot(32,38)。旧日付の原本・レビュー・過去動画は保持する。

## 予備動作と接触

| 反撃開始からの戦闘tick | 絵と処理 |
| --- | --- |
| 0〜10F | 既存の移行原稿。元の位置で沈み込む |
| 10〜12F | 実際に弾いた敵の背後へ移動。影・目の残光を残す |
| 12F | 新キック1コマ目を表示、同時に0.15倍スロー・暗転・アップへ入る |
| 12〜20F | 1〜7コマを2/1/1/1/1/1/1tickで順に見せる。足に電気が溜まり、電流が散る過程を読む |
| 20F | 8コマ目の最後のタメでスローを解除 |
| 21F | 初めて脚が伸びる9コマ目で接触、1回ダメージ・8F停止・青白い衝撃波・方向付きの揺れ |
| 21〜34F | 振り抜き・電気の軌跡の消散・接地。敵は3.2unit／高さ0.65unitで吹っ飛ぶ |
| 34F | 最終接地姿勢で行動・無敵を解放し、既存待機へ直接戻る |

ゲーム用は `[2,1,1,1,1,1,1,1,1,2,2,2,2,2,2]` の22F＋移行12F。タメは8戦闘tick／0.15倍で、要求上限0.95秒。原本のコマ時間をこのゲーム用時間へ上書きしない。

新版の足先はBodyの(83,86)px（旧版73,86）。足先オフセットを(51,4)/32へ更新し、同じ敵の頭の接点へ合わせる。背後の移動先は敵から58/32＝1.8125unit。敵のひるみ中の向きは保持し、接触より前に撃破しない。

## 電気と本編での共有

作画のBody／Effectをそのまま使う。予備動作は目を閉じているので、偽の瞳を足さない。既存移行のEyeマスクと残光は保つ。新しい軌跡を隠さないよう、Unityの衝撃波は従来の輪郭を青紫・水色・青みの白へ揃えた専用Fx_ElectricKick、倍率1.15。元の暖色Fx_Kickは保持する。編集可能な8コマの `Assets/Art/Video/PlayerElectricKickImpact.aseprite` と再生成Luaを用意した。

本編の既存キック表示も同じ原稿へ更新。プロトタイプ専用の反撃Clipは、現在の命中18F・威力・地上反撃の規則へ絵を合わせて再生成する。動画のワープ・スロー・無敵設定を本編へ追加しない。歩行ガイド除去とパリィ中の移動停止の直前の修正は維持する。

## 検証と動画

実装コミット：`746084c`、基点：`cefb675`。同じ作業フォルダ・作業ブランチを使用する。

- [通しMP4](previews/combat-2stage-hybrid-submitted-kick-20261019.mp4)：**253F / 4.216667秒、1920×1080・60fps・無音**。全253フレームをデコードし、H.264・音声stream 0を確認した。
- [イベント表](previews/combat-2stage-hybrid-submitted-kick-20261019.json)：ChargeStart 2.433秒 → Release 3.333秒 → Kick 3.350秒 → Landing 3.700秒。タメ実測0.900秒、解除→命中1tick、命中→接地13tick。Parry/Kickは同じEnemy_B。
- [描画された姿勢](previews/submitted-kick-20261019-poses.png) / [検証JSON](previews/submitted-kick-20261019-validation.json)。全15コマを実Sceneで観測し、1〜3コマ目だけで合計27動画F＝**0.450秒**を確保した。1〜7コマは合計54動画F＝0.900秒、8コマ目1F、9コマ目は停止込み9F、10〜15もすべて表示された。
- Compile Error 0 / Warning 0、**EditMode196/196・PlayMode396/396 Passed**。撮影統合150件、30/60/120fps×左右の全7予備姿勢と接触、Pause/中断/対象消失、時間・無敵・重力・カメラ・VFXの復帰、本編のキック命中/表示、直前の歩行・パリィ移動修正を含む。
- 通常/攻防切替それぞれ2/4段×左右×10回＝**計80回Reset**。二重ダメージ・別対象・操作不能・時間/カメラ/暗転/AI/VFX等の残留なし。通常2/4段は4.216667/5.616667秒、攻防切替2/4段は4.700000/5.733333秒。
- Game Viewでタメ5コマ目（elapsed17 / slow .15 / dim .965）と命中9コマ目（elapsed21 / slow1 / 1Hit / Hitstop）を確認。人物・足の電流・軌跡と寒色の補助波が表示された。収録後Console Error 0 / Warning 0、元のPrototypeCombatValidation SceneへEditModeで戻し、未保存変更なし。
- 1333ファイルの基準に対し、Current/台帳・新キックPNG・時間表・運用READMEの意図した21ファイルだけを更新し、**残る1312ファイルを全バイト保持**。着手時の未コミット変更・旧原稿・過去動画・旧暖色VFXも保持した。新原稿＝Current＝HistoryのSHA256が一致する。
- 検証済みの新版を、台帳のvideo／prototype使用版へ記録した。原稿登録だけの状態と区別し、清書・全体受入の完了を自動で意味する記録にはしない。

## 再現と残る確認

保存済みCombatVideo Sceneで `Tools > Technical Sword Action > Video > Apply Submitted Attack and Heel Drop` を実行すると3つの撮影Sceneへ反映する。PlayModeで `VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision submitted-kick-20261019 -Variants 2stage-hybrid` で書き出す。本編を再生成する場合はPrototype/Install Submitted Player Artも実行する。

キックの新作画の統合と、動画全体の完成受入は区別する。移行・成功軽などの残る仮素材、完成VFXの全体調整、清書を含む最終動画の受入と書き出しが揃うまで#69は完了にしない。原稿の非表示Hair等を勝手に完成扱いにしない。
