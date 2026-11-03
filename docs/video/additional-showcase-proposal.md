# 戦闘の魅力を見せる追加シーン案（2026-10-14）

既存の#69映像は「前の敵をコンボ撃破→背後を弾く→蹴り撃破」という流れ。追加映像では、攻撃を出し切る前に状況へ反応し、防御・反撃・移動・再攻撃がつながることを見せる。

## 候補と選定

| 案 | 見せたい魅力 | 映像の構成 | 選定理由/課題 |
|---|---|---|---|
| A・今回制作 | 攻防の切り替えと操作の自由さ | 斬撃途中の背後パリィ→空中蹴り→着地ダッシュ→元の敵へ4段/居合 | 既存動画と違う判断の瞬間を見せ、剣・防御・反撃・移動を1カットで結べる。実装済みの取消/時計/判定を使う |
| B | 複数の攻撃を見てさばく技量、個体ごとの反撃ストック | 前後を続けて弾く→各敵へ連続反撃 | 弾きの連続はわかりやすい。ストックの個体別の意味は小さい目印等で補う必要があり、今回は構成提案まで |
| C | 間合い・位置取りと速さ | 攻撃をダッシュ回避→背後へ回る→振り向いて居合撃破 | 移動の爽快感を伝える。敵の空振りとダッシュ無敵の時刻を明確に合わせる追加撮影が必要。今回は構成提案まで |

今回はAをラフまで制作。締め2段版と4段版を比較し、4段版を推奨する。約4.72秒の中に、短い弾きから反撃、方向転換と移動、納刀から居合まで入り、最後に立ち姿へ戻れる。2段版は3.75秒で切り替えの意味を最短で見る比較用。

## 制作済み動画

- 推奨：[4段で締める版](previews/combat-defense-cancel-4stage-rough-hybrid.mp4)、283F / 4.716667秒。
- 比較：[2段で締める版](previews/combat-defense-cancel-2stage-rough-hybrid.mp4)、225F / 3.75秒。
- [4段の連続画像](previews/defense-cancel-4stage-contact.png)、[2段の連続画像](previews/defense-cancel-2stage-contact.png)、[追加ダッシュラフ](rough-previews/Dash-showcase.gif)。

いずれも1920×1080/60fps/H.264/音声なし。白いグリッド、単色床、簡略ダミー、HUDなし、採用済みドット＋短い発光。実際の判定から録画し、全フレームをデコードして確認した。冒頭の縦斬り1Hitとは別に、締めのコンボを2段/4段に切り替える。

## 実時間の見せ場（両版共通の前半）

| 実時間 | 場面 | 実装で確認すること |
|---|---|---|
| 0.10秒 | 背後の敵が攻撃の予兆を出す | 主人公への攻撃元は敵B。敵AのHPは残っている |
| 0.483秒 | 前の敵へ縦斬りを当てる | 敵Aの最初のHP減少は1回 |
| 0.60〜0.617秒 | 攻撃中の防御受付でParry入力→背後への弾き | Attackから実際にParryへキャンセルし、成功した敵へ振り向く。成功/ひるみ/ストックは通常の契約 |
| 0.833秒 | 同じ敵へ瞬間接近する反撃を開始 | Attack入力、対象固定、予備動作は残像/影で見せる |
| 1.117〜1.783秒 | 0.15倍のタメとアップ、強い暗転/瞳の残光 | 実時間0.666667秒。物理・Animator・判定は共通時計 |
| 1.850秒 | 蹴りで敵Bを撃破 | 接触した足/実対象へ大きい衝撃波。8F停止、吹き飛び |
| 2.117〜2.150秒 | 低い着地からダッシュへ | 着地で無敵/行動ロックを終え、新しいDash入力で切り替える。無入力時の自然な復帰は元動画に残る |
| 2.317秒 | 前の敵へ再攻撃を開始 | ダッシュ終了後のAttack入力。敵Aへ戻って1段目からコンボを再開 |

2段の締めは縦2.50秒→横2.883秒。4段の締めは縦2.50秒→横2.883秒→連続斬り/納刀3.317秒→居合3.85秒。4段版は4.716667秒で敵/効果の消滅と立ち姿まで収まる。

## Sceneと再現

Sceneは `Assets/Scenes/CombatVideoDefenseCancel.unity`。既存 `CombatVideo.unity` と既存動画は保持する。制作ブランチは `codex/69-combat-video`、作業場所は従来の固定フォルダ。

素材生成が必要な場合：

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe' -b --script-param root=C:/Users/hirot/GitHub/technical-sword-action --script-param out=C:/Users/hirot/GitHub/technical-sword-action/Assets/Art/Player/VideoShowcase --script-param showcase-only=true --script Tools/Art/build-player-video-rough.lua
```

追加素材は `Assets/Art/Player/VideoShowcase/Player_mobility_rough.aseprite`。64×64/右向き/約3頭身/32PPU/8レイヤー/2タグ/7コマ。Dash4コマ/12F、自由なRecovery_Dash3コマ/11F、瞳マスクとタイミング表を保存/再読込検証。既存119コマの攻撃ラフと人の作画中ファイルへ追記しない。清書用レイヤーは空。

UnityのTools → Technical Sword Action → Video → Build Art and Clips、続いてBuild Defense Cancel Showcase Sceneで再生成できる。未保存の別Sceneは保存・破棄せず停止する。追加Sceneのダッシュ速度は6unit/s、時間は既存0.18秒。敵B初期x=-1.45、前の敵のHPは冒頭1Hit＋締めコンボ分で3/8。背後敵の蹴り吹き飛びは横1.75unitとして、画面内に残す。元動画の速度/敵配置/横2.25unitの吹き飛びは維持する。

撮影SceneのPlayModeで：

```powershell
uloop execute-dynamic-code --code 'return VideoCaptureUtility.ValidateDefenseCancelResetRuns();'
uloop execute-dynamic-code --code 'return VideoCaptureUtility.CaptureDefenseCancelPreview(2,true);'
uloop execute-dynamic-code --code 'return VideoCaptureUtility.CaptureDefenseCancelPreview(4,true);'
./Tools/Video/encode-showcase.ps1
```

連番は `.uloop/outputs/video/defense-cancel-{2,4}stage-hybrid`、メタデータのtakeKindはdefense-cancel。録画APIはSceneの種類を検査し、元動画の出力先へ追加Sceneを誤って書き出すことを拒否する。手動再生は `CombatVideoDirector.StartTake(2,true)` / `(4,true)`。GameObjectのDefenseCancelTake設定を使用する。

## 検証と完成の範囲

[検証記録](previews/defense-cancel-validation.json)、[40回反復](previews/defense-cancel-reset-validation.json)、[動画デコード](previews/defense-cancel-encode-validation.json)を参照。PlayMode追加17件は2/4段×30/60/120fps×両向き、40回反復、Dash中のPause/Reset、対象消失/Counter Disable/死亡による中断を確認。既存動画の回帰も同時に実施。

これは魅力の伝え方を判断する追加ラフで、#69の当初動画を置き換えず、正式完成とも扱わない。既存の必須清書60＋4段追加25枚はそのまま。追加ダッシュ7枚の人の清書を19日までの必須作業へ自動で加えない。この追加案を正式版へ進める場合は、共通清書とダッシュ素材の差し替え、接触/構図の再調整、テスト/反復/最終録画を行う。
