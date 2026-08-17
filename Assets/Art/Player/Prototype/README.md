# 主人公ドット絵・モーション追加ガイド

このフォルダは、主人公の動作確認用スプライトと、今後のAseprite作画をUnityへ反映するための作業メモです。

## まず守る規格

- 1フレームは `64 x 64 px`
- Unity側は `32 Pixels Per Unit`、`Point`、圧縮なし、Mip Mapなし
- キャラクターは右向きで作画。左向きはPlayerルートのX反転で表示
- 足裏の基準線は、上から数えて `58 px` の境界（描画行は `y=57` まで）
- Pivotは `(32, 38) px`。Unity座標ではPlayerルートから足裏までちょうど `-1 unit`
- Transform、SpriteRenderer Color、ルートScaleはアニメーションしない

64pxキャンバス内の目安です。

```text
y=0   ┌──────────────────────────────┐
      │ 上側の演出・武器用余白       │
      │                              │
y=26  │      ○  頭                  │
      │      │                       │
      │     / \                      │
y=57  │    足裏の最後の描画行        │
y=58  ├──── 接地基準線 ─────────────┤
      │ 下側の透明余白 6px           │
y=64  └──────────────────────────────┘
                    ↑ Pivot X=32
```

ジャンプ・落下など、意図的に足が地面を離れるフレームはこの線に合わせなくて構いません。Idle、Move、接地中の攻撃、Dash、Parryなどは、少なくとも片足をこの線へ合わせてください。

## 現在の仮素材

- `PlayerPrototype_Motions.png`: Unityが参照する固定アトラス
- `PlayerPrototype_MotionPreview.png`: 現在の棒人間24モーションの一覧確認用（Aseprite版へ差し替えた後は自動更新されません）
- `PlayerPrototype_Reference.png`: 3頭身棒人間の作画参考
- `../../../Animations/Player/Prototype/PlayerPrototypeAnimator.controller`: 実際に使用するAnimator Controller

収録済みは24モーション、合計141フレームです。

現在ゲームに接続済み:

`Idle / Move / Jump / Fall / Dash / Attack_1～4 / Parry / ParrySuccess / ParryFail / Special / Hit`

将来仕様向けにクリップのみ用意済み:

`DropThrough / ParryCounter / Heal / Death / Respawn / Rest / ComboBranch / AirRecovery / AirDashSlash / LandingShock`

## Asepriteで描き始める手順

1. Asepriteで `64 x 64 px`、RGBA、透明背景のファイルを作ります。
2. `Sprite > Properties...` で `Create UUID for layers` を有効にします。
3. 全フレームでキャンバスサイズを固定し、足裏基準線 `y=58` をガイドとして追加します。
4. 右向きだけを描きます。左右版を別に作る必要はありません。
5. レイヤー名は後からむやみに変更せず、作画レイヤーを整理して保存します。
6. Asepriteタグは、下の名前・順番・フレーム数を大文字・小文字も含めてそのまま使います。

| Row | Aseprite Tag | Frames | Repeat |
|---:|---|---:|---:|
| 00 | `PlayerPrototype_Idle` | 4 | ∞ |
| 01 | `PlayerPrototype_Move` | 6 | ∞ |
| 02 | `PlayerPrototype_Jump` | 4 | 1 |
| 03 | `PlayerPrototype_Fall` | 3 | ∞ |
| 04 | `PlayerPrototype_DropThrough` | 4 | 1 |
| 05 | `PlayerPrototype_Dash` | 5 | 1 |
| 06 | `PlayerPrototype_Attack_1` | 8 | 1 |
| 07 | `PlayerPrototype_Attack_2` | 8 | 1 |
| 08 | `PlayerPrototype_Attack_3` | 8 | 1 |
| 09 | `PlayerPrototype_Attack_4` | 11 | 1 |
| 10 | `PlayerPrototype_Parry` | 5 | 1 |
| 11 | `PlayerPrototype_ParrySuccess` | 4 | 1 |
| 12 | `PlayerPrototype_ParryFail` | 4 | 1 |
| 13 | `PlayerPrototype_ParryCounter` | 7 | 1 |
| 14 | `PlayerPrototype_Special` | 9 | 1 |
| 15 | `PlayerPrototype_Hit` | 3 | 1 |
| 16 | `PlayerPrototype_Heal` | 6 | 1 |
| 17 | `PlayerPrototype_Death` | 8 | 1 |
| 18 | `PlayerPrototype_Respawn` | 6 | 1 |
| 19 | `PlayerPrototype_Rest` | 6 | ∞ |
| 20 | `PlayerPrototype_ComboBranch` | 7 | 1 |
| 21 | `PlayerPrototype_AirRecovery` | 4 | 1 |
| 22 | `PlayerPrototype_AirDashSlash` | 6 | 1 |
| 23 | `PlayerPrototype_LandingShock` | 5 | 1 |

UnityのAseprite ImporterはForward方向だけを正式に扱うため、Ping-pongやReverseにはしません。

## おすすめの反映方法: 固定PNGアトラス経由

このプロジェクトでは、Aseprite Importerが自動生成するControllerをそのまま本番利用するより、既存の固定PNGアトラスを更新する方法が安全です。Sprite名、GUID、既存Animator接続、60fpsの戦闘時刻を維持できるためです。

1. `File > Export Sprite Sheet` を開きます。
2. `Sheet Type: Rows`、`Split Tags: On`、`Columns: 12`、`Scale: 100%` にします。
3. `Trim Sprite / Trim Sheet / Packed / Merge Duplicates / Ignore Empty` はすべてOff、各Paddingは0、Visible LayersのみをRGBA PNGで書き出します。
4. まず一時ファイルへ書き出し、サイズが `768 x 1536 px`、上表の1タグ＝1行になっていることを確認します。
5. 確認したPNGで `PlayerPrototype_Motions.png` を**同じファイル名・同じ場所で上書き**します。`.meta`は削除しません。
6. Unityへ戻り、`Tools > Player Prototype > Build Animation Set` を実行します。
7. `Tools > Player Prototype > Validate Animation Set` を実行します。
8. Game Viewで足元、左右反転、状態復帰、4段コンボを確認します。

行順とフレーム数は `Tools/Art/generate_player_prototype_motions.py` の `build_motions()`、表示時間は `Assets/Editor/PlayerPrototypeAnimationBuilder.cs` の `Motions` が正です。枚数を増減する場合は、PNGだけでなく両ファイルの定義を同時に変更してください。

## `.aseprite`をUnityへ直接入れて確認する場合

ラフのプレビュー用途なら `.aseprite` を `Assets/Art/Player/Prototype/Source/` に置いて直接インポートできます。Inspectorで次を設定します。

- Import Mode: `Animated Sprite`
- Layer Import: `Merge Frame`
- Pixels Per Unit: `32`
- Mesh Type: `Full Rect`
- Generate Physics Shape: Off
- Pivot Space: `Canvas`
- Pivot Alignment: `Custom`
- Custom Pivot: `(0.5, 0.59375)`（正規化値。64pxセルの下端から38px）
- Filter Mode: `Point`
- Compression: `None`
- Generate Mip Maps: Off
- Generate Animation Clips: On
- Individual Events: On
- Model Prefab: Off（本番のPlayerルートは既存Scene側を使用）

タグごとにAnimationClipが生成されますが、Importer生成のClipとControllerは読み取り専用です。最終的なゲーム組み込みは上記の固定PNGアトラス経由を使ってください。

## 攻撃モーションの重要事項

Attack 1～4のAnimation Eventは攻撃判定・コンボ受付・終了処理そのものです。削除、改名、時刻変更をしないでください。

| Clip | Hit | Window Open | Window Close | End |
|---|---:|---:|---:|---:|
| Attack_1 | 0.2500 | 0.3667 | 0.5333 | 0.5833 |
| Attack_2 | 0.2000 | 0.3000 | 0.4500 | 0.4833 |
| Attack_3 | 0.2500 | 0.3667 | 0.5500 | 0.5833 |
| Attack_4 | 0.6333 | 0.7333 | 0.9333 | 0.9833 |

対応メソッドは順に `OnAttackHit`、`OnComboWindowOpen`、`OnComboWindowClose`、`OnAttackEnd` です。`Build Animation Set` はこれらを再設定し、`Validate Animation Set` は名前・引数・時刻まで照合します。

## よくある失敗

- セル中央を足元と思って描く: キャラクターが約1 unit浮きます。必ず `y=58` を接地線にします。
- 下側6pxを切り詰める: Pivot位置が変わり、フレーム間で上下に揺れます。
- フレームごとにキャンバスやPivotを変える: 足元が揺れます。
- PPUをAseprite Importer既定の100のままにする: 表示が小さくなります。32へ変更します。
- Tight Meshを使う: フレームごとに描画形状が変わります。Full Rectを使います。
- PlayerのTransform ScaleやSpriteRenderer ColorをClipへ記録する: 左右反転や被弾フラッシュと競合します。
- `.meta`を削除する: SpriteとAnimationClipの参照が切れます。
- Attack ClipをImporter生成版へ直接置き換える: 戦闘イベントを失う可能性があります。

## 仮素材を再生成する場合

棒人間へ戻したい場合だけ、次の順で再生成します。手描きPNGを上書きするので通常の作画更新時には実行しないでください。

1. `Tools/Art/generate_player_prototype_motions.py` を実行
2. Unityで `Tools > Player Prototype > Build Animation Set`
3. Unityで `Tools > Player Prototype > Validate Animation Set`
