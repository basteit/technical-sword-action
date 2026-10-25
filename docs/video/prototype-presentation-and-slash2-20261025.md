# 斬撃2の復元・プロトタイプ演出・即時パリィ

2026-10-25。攻撃2段目のエフェクト復元、撮影演出のプロトタイプへの反映、攻撃先へのカメラ調整に対応。追加指示により、プロトタイプは入力直後から受付し、**成功時だけ弾きのピークへ進み、空振り時は待機姿勢を保持**する。

[撮影版MP4](previews/combat-2stage-hybrid-restored-slash2-20261025.mp4)／[プロトタイプ実AI版MP4](previews/prototype-parry-counter-presentation-20261025.mp4)／[検証JSON](previews/prototype-visual-sync-20261025-validation.json)／[撮影版の連続画像](previews/restored-slash2-20261025-contact.png)／[プロトタイプの連続画像](previews/prototype-parry-counter-20261025-contact.png)／[斬撃8コマ](previews/attack2-restored-effect-20261025-contact.png)。

## 攻撃2段目

前回の出力は、新原稿で非表示だったEffectをそのまま非表示で合成していたため、斬撃が抜けていた。今回は出力用コピーの**Effectだけを表示**する。新しい剣はBodyに含まれるため、古いSwordレイヤーは戻さない。新しいBody/Hair、キャンバス128px/32PPU/ピボット(32,38)、8×70ms、命中13tick/全34tick/既存リーチを保持。

受領原稿91cb46e8…のCurrent/History/日付付き原本は変更しない。原稿版の追加登録は不要な、出力表示の修正である。Asepriteで合成・保存・再読込し、確認用原稿は `Assets/Art/Player/VideoSubmitted/20261025/Player_attack_2_effects_preview.aseprite`。旧確認コピーも保持する。`export-player-submission.lua` はHorizontalSlashのEffectを標準で復元する。受領時の非表示状態を確認する場合だけ `restore-attack-layers=false` を指定できる。

共有PNGの差し替えで撮影/Studio/プロトタイプへ反映し、Sprite GUID・全Clip/Controllerを保持。動画用の早い1→2段接続、2段撃破、本編4段は維持する。検証後に使用版の証跡だけを更新する。

## プロトタイプの演出

`CounterCinematicProfile.enhancedPresentation` で選択。撮影Directorや撮影用アニメーションドライバーを本編へ追加せず、逆光と輪郭の描画部品を共有する。カメラTransform/画角の所有者は既存の `CombatFeedback2D` のまま。

- 軽パリィ：黄色い作画火花を保つ白い逆光4動画F。成功時の停止は通常9/Just10F、方向付きの揺れ0.22unit/0.15秒。古い図形/追加Tintは重ねない。
- カウンター：0.8秒周期で青白い輪郭を明滅。足元の補助光、命中ピーク4F＋減光20F、16F停止、0.48unit/0.30秒の揺れ、命中時の短い6%ズームを接続。コード製集光/放電は追加しない。手描き電撃は維持。
- Flash/VFX/Cameraの0%/50%/100%を反映し、判定・ダメージ・時間制御を装飾設定から独立させる。伸縮する敵のSpriteはサイズも写し、一時的なFull Rectを再利用する。元Texture/Importerは変更せず、描画警告とコマごとのSprite生成を防ぐ。
- カメラは撮影版で主人公と対象の中間から攻撃側へ0.30unit寄せる。プロトタイプは主人公基準の寄り位置を攻撃側0.45→0.80unitへ変更。通常5.625/寄り3.75、上下位置と出入りの速度は維持。

プロトタイプの移行10〜12F・タメ12〜20F/0.15倍・命中21F・終了45F、90ダメージ/対象固定/無敵/壁判定、3.2unit/高さ0.65/22Fの吹っ飛びは維持。撮影の画角や敵の配置/行動を本編へコピーしない。

## 入力と成功/失敗

プロトタイプ専用ParryTimingは発生5→0F。受付幅を保って5〜18→0〜13F、Just5〜8→0〜3Fへ前倒し。失敗終了26F・通常成功10F/Just8F・Cooldown14Fは維持する。既存の別Scene/未指定設定の既定値は変えない。

入力中と失敗中は受領ParryReadyの最後の構えを保持する。実際の成功通知で同tickに3コマ目・逆光・揺れ・停止を出し、成功Clipは正規化位置0.3から続ける。停止後に1〜2コマ目をやり直さない。成功するまで待つ表示上の追加遅延は廃止した。空振りでは成功動作/逆光/成功停止を出さず、受付終了後は既存の復帰へ戻る。

撮影版は従来の成功→3コマ目の同期と全8コマを維持する。この変更はプロトタイプの操作への応答に限る。成功停止中は既存の入力停止規則を守り、解除後の成功硬直中に反撃を予約できる。

## 検証と再現

Compile Error/Warning0、関連PlayMode **220/220**、EditMode **23/23**合格。プロトタイプ72件には0Fでの実接触、成功同tickのピーク、空振りの待機保持、停止後に予備コマを再生しないこと、左右各10回の通し/Resetを含む。回帰148件は既存行動/4段/作画/共通Feedback/撮影のカメラ・照明等。fps・左右、Pause/中断/対象喪失/装飾0%・半強度、伸縮Sprite/Full Rect再利用も確認。旧タイミングの期待値は最新仕様へ更新した。全件テストや実機ゲームパッド/Development Buildの受入とは区別する。

撮影版は275F/4.583333秒、プロトタイプ実AI版は210F/3.5秒。どちらも1920×1080/60fps/H.264・無音、全フレームをデコードして確認する。実AI版は通常の敵の予兆に合わせてParryを要求し、成功後にAttackを要求する。接触/成功/ダメージはゲーム側が処理し、強ザコHP120→30・主人公HP100・1Hitとなる。確認用録画であり、人による操作感の受入ではない。

撮影版：CombatVideoをPlayして `VideoCaptureUtility.CapturePreview(2,true)`、`encode-captures.ps1 -Revision restored-slash2-20261025 -Variants 2stage-hybrid`。プロトタイプ版：PrototypeCombatValidationをPlayし `PrototypeImpactCaptureUtility.Prepare()`、次のEditorフレーム後に `Capture()`。入力/設定/自動時計/RenderTextureは終了時に復帰する。

今回は差し替えと演出接続を完了する。原稿/演出の最終受入、手描き放電の受領後の接続、最終録画版の確定と#69の全体完成は別に残す。未達Issueは閉じない。
