# パリィ3コマ目への衝撃同期

2026-10-25。受領済み軽パリィの**3コマ目**に、カメラの揺れの最大点、敵のやられ表示開始、ヒットストップを揃えた。[前版](parry-revision-20261024.md)の原稿と黄色い作画エフェクトをそのまま使い、旧Codex製の十字/火花/補助Glowは表示しない。

## 表示と判定

| 原稿のコマ | 主動画での表示 | 役割 |
| --- | --- | --- |
| 1 | 2動画F | 弾く動きの入り。敵の攻撃姿勢を保持し、停止/揺れは始めない |
| 2 | 1動画F | 3コマ目への速い振り |
| 3 | **5動画F** | 黄色い大きな弾き、敵のdown開始、揺れのピーク。4Fのヒットストップを含む |
| 4〜7 | 各1動画F | 振り抜きと火花の減衰 |
| 8 | 2動画F | 収まりから反撃への接続 |

3コマ目は成功から3戦闘tick後。通常の停止4F、Justの停止5Fは元の強さを使い、停止位置を先頭から3コマ目へ移した。揺れは従来の最大0.22unit/9動画Fで、3コマ目の先頭に最大変位を置いて減衰する。短い暗色Flashもその瞬間から始める。停止中も揺れは実時間で進み、Pauseでは両方止まる。

**成功判定・ダメージ防止・実攻撃元への振り向き・反撃登録は従来の成功tickのまま。** 敵の戦闘上のひるみも即時に適用して攻撃を中断する。敵の絵だけを直前の攻撃姿勢で3tick保持し、主人公の3コマ目と同じtickに固定downへ切り替える。やられ開始前の移動、追加ダメージ、別対象への反撃を導入しない。

## 接続

- `VideoSubmitted/timing-manifest.json` と共有Libraryの `ParrySuccessLight.hitFrame` を3へ。2+1Fの先頭2枚を通った位置で、コマ時間の合計10戦闘Fは変更しない。
- `PlayerDamageReceiver2D.ParryFeedbackRequested` を任意の表示接続として追加。動画のAnimationDriverが購読している場合だけ、設定済みの停止時間を3コマ目まで預かる。購読のない通常ゲームの停止/カメラ接続は従来どおり即時。
- `VideoPlayerAnimationDriver` が戦闘時計の整数tickとSprite選択を使い、3コマ目を表示した同tickに停止を要求して `ParryImpact` を1回通知する。
- 撮影Directorと自由Studioがこの通知で揺れ/Flash/VFXを呼ぶ。撮影イベントには成功の `Parry` と表示接触の `ParryImpact` を分けて記録する。
- 敵Driverも同じ整数tickを使って攻撃絵の保持を解除する。Reset/SessionReset/Disableで予約を解除し、死亡や新たな被弾で古い予約を残さない。
- 元の原稿、全PNG/瞳、全Clip/Controller、Current/History/catalog、本編反撃Profile、各Sceneは変更しない。原稿版は増やさない。

## 成果物と確認

[MP4](previews/combat-2stage-hybrid-parry-impact-cel3-20261025.mp4)／[連続画像](previews/parry-impact-cel3-20261025-contact.png)／[イベント表](previews/combat-2stage-hybrid-parry-impact-cel3-20261025.json)／[検証JSON](previews/parry-impact-cel3-20261025-validation.json)。チャット内で再生できるプレビューを更新した。

**267F / 4.45秒、1920×1080・60fps・H.264・無音。** 全267Fをデコード。既存の全イベント/対象/tick/実時間と撮影メタデータは前版と一致し、`ParryImpact` だけを追加。記録上は成功2.15秒→表示衝撃2.20秒（3tick差）。実収録132枚目で3コマ目・敵のdown・揺れの最大点が一致し、3コマ目の5枚は同じ戦闘tick/姿勢で停止する。

2体目の冒頭接近と0.50秒の攻撃開始、敵攻撃全12コマ/96戦闘F、2段撃破、同じEnemy_Bへの背後反撃、タメ1.0秒/最後の8コマ目7動画F、解除の次tickの命中、キック全15枚と電撃同期、10F命中停止、カメラ距離/吹っ飛び/停止/消滅を保持した。

- Compile Error 0 / Warning 0。
- 関連PlayMode **30ケースの最終確認が合格**。30/60/120fps×左右の3コマ目/揺れピーク/down開始、前後のPause、4F/Just5F/キック10F停止、同対象反撃、Studio、完走/時間と左右各10回＝20回のリセットを確認。
- Reset、Driver Disable、SessionReset、強制被弾、対象消失を3コマ目の前へ挟み、遅延した停止/揺れが漏れないことを確認。表示接続のない通常のJustは即時5F停止を維持する。
- 初回24/30。Studioの破棄済みPlayerから購読解除しようとした箇所を、保持したDriver参照から安全に解除するよう修正。次の30件は28件合格で、残る2件は追加したJust用Fixtureが入力適用前に接触していた。Fixtureを戦闘tickで入力が適用されてから接触させ、**その2件だけ再実行して2/2合格**。ランタイムは28件合格後に変更していない。全件/旧80回の組み合わせは再実行していない。
- 全Fの進行記録で、先頭2コマの停止/揺れ0、3コマ目の残停止[4,3,2,1,0]、同一tick/down保持、全8コマ/電撃同期/終端復元を確認。
- Aseprite再出力でManifestと全8PNGが現在のAssetsに一致。原稿を変更せず、出力手順からも3コマ目の接触を再現できる。
- 撮影/終了後Consoleは全種0。元の `PrototypeCombatValidation` へEditMode・未保存変更なしで復帰。
- 開始時2387ファイル中、意図した12点以外の2375点をバイト保持。既存の未コミット変更、全原稿/PNG/Clip/Controller/Scene・Current/History/catalogと本編反撃Profileを保持。今回17ファイルのステージ済みRepository監査Passed。

今回の同期と再録画は完了。動画全体の最終受入は別に残し、#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。

## 再現

既存の保存済み `CombatVideo.unity` をPlayにして `VideoCaptureUtility.CapturePreview(2,true)`、続いて `Tools/Video/encode-captures.ps1 -Revision parry-impact-cel3-20261025 -Variants 2stage-hybrid`。素材の再反映は `Video > Refresh Submitted Light Parry` を使い、他アクション/Controllerを一括再生成しない。
