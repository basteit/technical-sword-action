# プレイヤー原稿ライブラリ

[素材一覧を開く](INDEX.md)

2026-10-25移行更新：`counter_transfer` の2版目（64px/8×100ms、後ろ髪を含む）をCurrent/Historyへ登録し、共有PNG8枚へ反映。ゲーム用12tick/ピボット/全Clip/Controllerを保持し、個別更新メニューを追加しました。動画は関連PlayMode、本編は共有SpriteとClipのEditMode確認を行い、使用版を別々に記録します。[演出比較・素材・検証](../../docs/video/counter-lighting-20261025.md)。原稿の仮提出区分と、動画全体の最終受入は維持して区別します。

2026-10-25素材更新：パリィ待機2版目と成功軽3版目（後ろ髪を含む）を受領し、Current/Historyへ登録、動画/Studioの共有PNGへ反映しました。キャンバス・32PPU・ピボット・コマ時間・判定を保持。白黒反転は2→4動画Fへ延長。本編は共有PNG/Clipの参照確認までとし、動画のPlayMode受入と区別しています。[変更・動画・限定検証](../../docs/video/parry-update-invert4-20261025.md)。

2026-10-25パリィ追加調整：[停止9F・カウンター16F維持](../../docs/video/parry-stop9-20261025.md)。同じ原稿でパリィ3コマ目を10動画F表示します。新しい原稿版やPNG/Clipの変更はありません。

2026-10-25停止延長の試用：[パリィ7F/カウンター16Fの比較動画](../../docs/video/longer-hitstop-20261025.md)。同じ原稿/PNG/Clipを使い、パリィ3コマ目の表示は8動画F、キック命中は17動画Fになりました。停止時間の調整のみで、新しい原稿版は追加していません。

2026-10-25表示同期：[軽パリィの3コマ目への衝撃同期](../../docs/video/parry-impact-cel3-20261025.md)。同じ2版目の原稿/PNG/Clipを保持し、動画/Studioの3コマ目へ揺れのピーク・敵のやられ開始・停止を移しました。3コマ目は通常4Fの停止込みで5動画F。原稿版やネイティブの80msは変更していません。

2026-10-24軽パリィ更新：`parry_light` の2版目（128px/8×80ms、人物＋黄色い弾き）を登録・反映しました。旧原稿をHistoryへ保持し、撮影/StudioではCodex製パリィVFXの重ね描きを停止。個別更新メニューで共有PNG/Libraryと撮影/本編の成功Clipだけを更新できます。[原稿・反映範囲・動画・限定検証](../../docs/video/parry-revision-20261024.md)。本編側はClip/参照のEditMode確認までで、実戦操作の受入や全体完成とは区別します。

2026-10-24撮影時間の追加調整：同じ原稿版を使い、蹴り出す直前の8コマ目を撮影側だけ1→7動画Fへ延長しました。人物と独立電撃を同期させ、タメ全体は1.0秒。Current/History・原稿の表示時間・PNG・Clip・共有Library・本編用時間表は保持し、撮影ゲートと表示フレーム対応で延ばしています。[最新の動画・時間配分・検証](../../docs/video/early-rear-charge-hold-20261024.md)。原稿版の追加登録はしていません。

2026-10-24素材更新/比較時点：`counter_kick` は細部修正の5版目、`counter_kick_effect` は2版目を使用します。人物128px/電撃256pxの各15コマを同期し、現在版をCurrentへ、旧版をHistoryへ保持。動画用と本編用を検証し、従来の撮影距離とゲーム画面に近い距離で比較撮影しました。[素材・比較動画・検証](../../docs/video/counter-revision-and-camera-comparison-20261024.md)／[位置合わせ・再出力手順](../../docs/video/counter-split-20261024.md)。この比較時点ではタメ0.9秒と蹴った後の長さを維持しました。[前版の時間配分](../../docs/video/counter-follow-through-20261019.md)と初回Sample等は履歴です。

これまで受領したAsepriteを、動画用・本編用に共通して探せる場所です。現在の編集用原本を `Current/` に集め、登録したすべての版を `History/<動作ID>/` に保持します。Asepriteのレイヤー・表示状態・タグ・コマ・表示時間は変えません。

- **Current**：次に作画・修正するときに開く現在版。ファイル名と場所を更新後も固定します。
- **History**：SHA256で識別する変更しない原本履歴。更新しても前の版へ戻れます。
- **catalog.json**：動作ID、区分、原稿サイズ、各コマの時間、レイヤー、タグ、履歴、ゲームで使用した版を記録する台帳。
- **INDEX.md**：台帳から自動更新する人向けの一覧。

編集元をUnityの外に置くことで、整理・登録だけでゲームのSpriteや判定が変わることを防ぎます。既存の `Assets/Art/Player/Player_*.aseprite`、`VideoSubmitted/`、48pxサンプルは従来の参照を保つためそのまま保持します。登録時のCurrentとHistoryは、その原本と画素・レイヤーを含む全バイトが同一です。

## 追加・更新

素材をチャットに添付してもらった際も、以下の仕組みで既存版を保持して登録します。同じ動作の更新は**同じID**を使い、新しい動作だけIDを追加します。台帳の区分は原稿の状態であり、ゲーム実装やIssue全体の完成判定とは別です。

PowerShellから、プロジェクトのルートで実行します。`python` がない場合は利用中のPython実行ファイルへ読み替えます。Asepriteは既定のSteam版パスを使い、別の場所なら `register` などのサブコマンドの前に `--aseprite <実行ファイル>` を付けます。

```powershell
# 攻撃1段目の更新。別名で届いてもCurrentの保存先は変わりません。
python Tools/Art/player_source_library.py register --id attack_1 --source 'C:\Users\hirot\Downloads\e\Player_attack_1.aseprite'

# 新規動作。初回は名前も指定します。
python Tools/Art/player_source_library.py register --id parry_heavy --name 'パリィ成功（重）' --status provisional --source 'C:\Users\hirot\Downloads\e\Player_parry_heavy.aseprite'

# Currentを直接編集した場合も、そのファイルを登録します。
python Tools/Art/player_source_library.py register --id counter_kick --source 'ArtSource/Player/Current/Player_counter_kick.aseprite'

# 未登録の編集、履歴の欠落・改変、ゲーム側の使用版とのずれを確認します。
python Tools/Art/player_source_library.py status
python Tools/Art/player_source_library.py validate
```

同一内容の再登録で履歴は増えません。未登録のCurrent編集を別の提出ファイルで置き換える場合は、その作業中の内容も `saved-working-copy` として先に履歴へ保存します。登録処理中にCurrentがさらに編集された場合は上書きせず終了します。元の添付・Downloadsファイルは変更しません。

IDや台帳のCurrent/Historyパスを直接書き換えず、登録コマンドを使ってください。履歴は削除・上書きしません。原稿のサイズは64/128pxだけに限定せず、48pxの既存サンプルも元のサイズで保持します。

## Unityへ反映する場合

原稿の登録とゲームへの反映を分けて追跡します。更新後は、台帳の使用記録が古い原稿を指していれば `status` と素材一覧に「更新待ち」と表示します。

1. 更新素材の表示レイヤー、接地・向き・大きさ、キーポーズと時間を確認する。
2. 動画向けは既存の `Tools/Art/export-*.lua` と `CombatVideoArtBuilder`、本編側は現在の専用接続へ反映する。新しい絵に合わせて接触やキャンセルを調整するときは、該当する既存テスト・PlayModeで確認する。
3. 動作確認と検証結果を記録した後で、使用した原稿版を記録する。`record-use` 自体がUnityへ反映したり、検証を代行したりすることはない。

```powershell
python Tools/Art/player_source_library.py record-use --id attack_1 --consumer video --motion VerticalSlash --reference Assets/Art/Player/VideoSubmitted/timing-manifest.json --reference Assets/Settings/CombatVideoArt.asset --evidence docs/video/counter-submissions-20261018.md
```

`--consumer` は `video` / `prototype` などの使用先ID。`--evidence` はその版で実際に確認した記録を指定します。現状の動画使用版は8素材について記録済み。並行作業中の本編接続は、この整理作業だけで検証済みに変更しません。

後続のプロトタイプ接続 `f071850` では、待機・歩行・攻撃1/2・パリィ待機/成功軽・キック・ジャンプ開始/上昇の9素材を検証し、`prototype` 使用版を登録しました。[接続と仮表示の範囲](../../docs/prototype-player-art.md)／[初回検証](../../docs/validation/prototype-player-art-2026-10-18.json)を参照してください。2026-10-19の明示的な反撃仕様変更 `222d468` では、反撃移行8コマと最新キック15コマを本編専用設定へ接続し、全207/441件と実画面を確認した後に使用記録を更新しました。[反撃演出の検証証跡](../../docs/validation/prototype-counter-cinematic-2026-10-19.json)。移行は仮素材のまま、旧48px待機は未接続です。登録版一致は清書・全24モーション・人間の受入完了を意味しません。

## 初回整理の範囲

受領原稿8種（待機・歩行・攻撃1/2・パリィ待機/成功軽・反撃移行/キック）と、既存ジャンプサンプル2種、旧48px待機サンプル1種を収録しました。キックは表示中のSampleシルエット、移行のEyeは原稿の不透明度のまま。清書や電気の完成作画を済ませた扱いにはしません。

旧待機サンプルは参考区分で、64pxの現在待機と区別します。Codex製ラフとUnityの出力は素材一覧から元の場所へリンクし、受領原稿へ混ぜません。

初回整理（2026-10-18）は11原稿すべてで、元ファイル＝Current＝HistoryのSHA256と、Asepriteでの再読込メタデータ一致を確認しました。追加・更新・重複登録・作業中ファイルの退避・登録中の編集・ゲーム使用版の更新待ち・改変検出などの20テストが合格しています。検証記録は `initial-organization-validation-20261018.json`。

```powershell
python -m unittest discover -s Tools/Art -p test_player_source_library.py -v
```
