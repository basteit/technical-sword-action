# プレイヤー原稿ライブラリ

[素材一覧を開く](INDEX.md)

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

後続のプロトタイプ接続 `f071850` では、待機・歩行・攻撃1/2・パリィ待機/成功軽・キック・ジャンプ開始/上昇の9素材を検証し、`prototype` 使用版を登録しました。[接続と仮表示の範囲](../../docs/prototype-player-art.md)／[検証証跡](../../docs/validation/prototype-player-art-2026-10-18.json)を参照してください。反撃移行と旧48px待機はプロトタイプへ接続していません。登録版一致は清書・全24モーション・人間の受入完了を意味しません。

## 初回整理の範囲

受領原稿8種（待機・歩行・攻撃1/2・パリィ待機/成功軽・反撃移行/キック）と、既存ジャンプサンプル2種、旧48px待機サンプル1種を収録しました。キックは表示中のSampleシルエット、移行のEyeは原稿の不透明度のまま。清書や電気の完成作画を済ませた扱いにはしません。

旧待機サンプルは参考区分で、64pxの現在待機と区別します。Codex製ラフとUnityの出力は素材一覧から元の場所へリンクし、受領原稿へ混ぜません。

初回整理（2026-10-18）は11原稿すべてで、元ファイル＝Current＝HistoryのSHA256と、Asepriteでの再読込メタデータ一致を確認しました。追加・更新・重複登録・作業中ファイルの退避・登録中の編集・ゲーム使用版の更新待ち・改変検出などの20テストが合格しています。検証記録は `initial-organization-validation-20261018.json`。

```powershell
python -m unittest discover -s Tools/Art -p test_player_source_library.py -v
```
