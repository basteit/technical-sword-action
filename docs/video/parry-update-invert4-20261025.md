# パリィ2原稿の更新と白黒反転4F

2026-10-25。「反転が短すぎるため肉眼で捉えられる程度に延長」「待機と軽い弾きを更新」に対応する。反転を**2→4動画F（0.033333→0.066667秒）**に延ばし、パリィ3コマ目の先頭へ揃える。残りの停止は通常色で見せる。通常9F・Just10F・キック16Fの停止、カメラ/揺れ/配置/反撃の時間配分は保持する。

[最新MP4](previews/combat-2stage-hybrid-parry-update-invert4-20261025.mp4)／[旧原稿・反転2F版](previews/combat-2stage-hybrid-parry-invert-20261025.mp4)／[連続画像](previews/parry-update-invert4-20261025-contact.png)／[イベント表](previews/combat-2stage-hybrid-parry-update-invert4-20261025.json)／[検証JSON](previews/parry-update-invert4-20261025-validation.json)。チャット内にも再生プレビューを用意する。

受領原稿は `player_source_library.py register` で登録し、Currentへ保存、旧版もHistoryへ保持した。元のDownloadsは変更していない。

| 動作 | 版 | SHA256 | 原稿の時間 |
| --- | --- | --- | --- |
| パリィ待機 | 2 | `c5dffc39dd22ed44d1a6e812386bb619b6c2c5efbf89bed5e8918e4dcb1a96d0` | 64×64、8×100ms |
| 成功・軽 | 3 | `d038a1c76e432c7803be090daa73cd3e6d2062cf1d1347c774d4262d378b1637` | 128×128、8×80ms |

追加された後ろ髪と、原稿で表示中のBody/Effectを反映。ガイドだけを非表示にしてAsepriteでPNGを出力し、別名の編集用確認コピーを再読込した。原稿のレイヤー・表示時間・表示状態は保持する。受領原稿とCodex製ラフを混ぜず、成功軽を最終清書と自動判定しない。

32PPU・ピボット(32,38)とSpriteのパス/GUIDを維持。ゲーム用の待機40tick（[2,2,6,6,6,6,6,6]）、成功軽10tick（[2,1,1,1,1,1,1,2]）、成功から3tick後の3コマ目で衝撃を出す接続も維持する。原稿の弾きエフェクトを使い、旧Codex製パリィVFXは非表示のまま。

個別更新メニューに `Refresh Submitted Parry Ready` を追加し、成功軽と共通の処理で当該PNG/Library/Clipだけを更新できるようにした。全Buildや本編素材のInstallは実行していない。共有PNGを使う本編の待機/失敗/成功Clipにも更新絵が反映される。本編側の検証は参照/ClipのEditMode2件と既存Validatorまでで、今回のPlayModeは撮影/Studioの範囲。

Compile Error/Warning0、関連PlayMode30/30・EditMode2/2合格。30/60/120fps×左右のパリィ接触/反転実画素/4実時間Fでの解除、入力待機の受付/保持姿勢、反撃対象、9/Just10F・キック16F、Pause/Reset/SessionReset/Disable/カメラ破棄、Studioを確認。左右各10回＝20回の通し/Resetも通過。全件/旧80回の組み合わせは再実行しない。

**275F / 4.583333秒・1920×1080・60fps・H.264・無音**。全275Fをデコードし、反転は132〜135枚目（PTS 2.183333〜2.233333秒）だけ。136枚目から通常色へ戻る。全イベント表と、反転列/セッション時計の開始値以外の全フレーム記録が前版と一致する。新原稿16枚の人物PNGと各EyeMaskはAseprite出力と画素一致し、Downloads＝Current＝History＝日付付き原稿の全バイトも一致。確認コピーは別名で再読込済み。

終端は両敵非表示・VFX0・停止0・倍率1・揺れ/カメラ復元・反転なし・提出Idle。撮影中/終了後Console全種0、元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。原稿使用版の記録は確認後に更新し、台帳validate成功・更新待ち0。

開始時2413ファイル中、意図した25点以外2388点と既存未コミット変更をバイト保持。旧History・他動作の台帳・全Clip/Controller/共有Library/Scene・本編Profile・URP/Shader/Rendererは不変。差し替え対象のSpriteパス/GUID・原稿の表示時間/ピボットを維持。今回49ファイルのRepository監査Passed。

素材の再読込で元の検証Sceneが未保存状態になったため、既存の保存・続行の承認に従って保存した。保存後のSceneファイルは開始時とバイト一致。原稿登録・Unity反映・確認後の使用版記録は分けて行う。

再現は `CombatVideo.unity` をPlay、`VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision parry-update-invert4-20261025 -Variants 2stage-hybrid`。反転は `parryInversionFrames=4`、2で前版の長さ、0で従来の黒フラッシュ。

今回の素材差し替え/演出調整と動画全体の最終受入は区別する。#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
