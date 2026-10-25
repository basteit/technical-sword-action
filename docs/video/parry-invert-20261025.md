# パリィ接触の白黒反転・2F試作

2026-10-25。「白黒反転で一回試したい」の指示に対応する試作。パリィ成功3コマ目の先頭に、画面全体の彩度を抜いて明暗を反転する演出を**2動画F（実時間0.033333秒）**だけ入れる。白い背景が黒くなり、暗い輪郭が白くなる。その後は通常色で接触姿勢を見せる。

通常パリィ9F・Just10F・カウンター16Fの停止、揺れのピークと敵down開始の同期、全素材・配置・尺は前版を維持する。従来の薄い黒フラッシュだけを反転へ置き換え、原稿の火花や別のVFXは追加しない。キックの白フラッシュとスローの暗転は従来通り。

[試作MP4](previews/combat-2stage-hybrid-parry-invert-20261025.mp4)／[反転前の9F版](previews/combat-2stage-hybrid-parry-stop9-20261025.mp4)／[接触の連続画像](previews/parry-invert-20261025-contact.png)／[イベント表](previews/combat-2stage-hybrid-parry-invert-20261025.json)／[検証JSON](previews/parry-invert-20261025-validation.json)。チャット内にも再生プレビューを置く。

`VideoCombatPresentation` の `parryInversionFrames` は0〜4F。今回2、1で短縮、0で従来の黒フラッシュへ戻せる。戦闘時計ではなく既存の実時間の演出更新で進め、Pause中は停止する。Reset/SessionReset/Disable/Scene変更でカメラ登録を解除する。

`VideoParryInversionFeature` は指定された撮影カメラにだけURPのFull Screen Passを投入する。素材を変更せず、合成された画面の表示明度をグレースケール反転し、Pointサンプリングでドットを保つ。`Renderer2D.asset` にはFeatureと参照だけを追加し、既存の全設定を保持する。他カメラや通常の本編では反転パスを投入しない。原稿登録・版・使用中素材の変更はない。

再現は保存済み `CombatVideo.unity` をPlay、`VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision parry-invert-20261025 -Variants 2stage-hybrid`。

**275F / 4.583333秒・1920×1080・60fps・H.264・無音**。反転は132/133枚目（PTS 2.183333/2.200000秒）。イベント表の全項目と、セッション時計の開始値を揃えた全フレーム記録の値は前版と一致する。3コマ目の残停止は[9…0]、カウンター9コマ目は17動画Fを維持する。

Compile Error/Warning0、シェーダー診断0、関連PlayMode **26/26合格**。30/60/120fps×左右の同期、反転の実画素・カメラ限定・Pause・2実時間Fでの解除、Reset/SessionReset/Disable/カメラ破棄、通常9/Just10F・カウンター16F、Studio、左右各10回＝20回の通し/Resetを確認。初回25ケース中、実画素テスト1件が反転未描画を検出した。シェーダーにURP Coreの定義を追加し、比較用カメラのTransformも揃えて全合格。その後カメラ破棄の解除ケースを追加し、最終26ケースを全合格した。全件や旧80回の組み合わせは再実行していない。

全275Fデコード、反転の前後は通常色へ復帰。終端は両敵非表示・VFX0・停止0・倍率1・揺れ/カメラ復元・反転なし・提出Idle。テスト終了後ConsoleはError/Warning0と完了通知2件だけで、通知整理後は全種0。元のPrototypeCombatValidationへEditMode/未保存変更なしで戻した。

開始時2402ファイルのうち2398点と既存未コミット変更をバイト保持。変更した既存ファイルは演出・テスト・Renderer2D・撮影ガイドの4点だけ。Scene/原稿/PNG/Clip/Controller/共有Art/台帳/本編Profileと、開始時に変更されていたURP設定も保持した。今回15ファイルのRepository監査Passed。

これは演出の試用であり、動画全体の最終受入とは区別する。#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
