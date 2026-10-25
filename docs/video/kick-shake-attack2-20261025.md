# キックの揺れ強化と攻撃2段目の更新

2026-10-25。キック命中時の揺れをもう少し強くし、受領した攻撃2段目を差し替える指示に対応。

[更新MP4](previews/combat-2stage-hybrid-kick-shake-attack2-20261025.mp4)／[連続画像](previews/kick-shake-attack2-20261025-contact.png)／[攻撃2段目8コマ](previews/attack2-revision2-20261025-contact.png)／[検証JSON](previews/kick-shake-attack2-20261025-validation.json)／[直前版](counter-pulse-attack1-20261025.md)。

## 変更

キックの揺れを0.38→0.48unit、約26%強化した。非Readable設定も0.36→0.46unit。合成上限は0.42→0.52unitに広げ、増加分が上限で失われないようにした。0.30秒・向き・振動周期・減衰曲線は維持。パリィと通常斬撃の揺れ、通常/タメ/命中の画角、ヒットストップは維持する。全フレームの比較で、揺れの差がキック命中後だけに限定されることを確認。

攻撃2段目は2版目 `91cb46e80401ea592beca3b6ee3c61b3d23928771f28fe0312c6d00aee80b6aa`。Currentへ登録し、旧版ccdb5da3…はHistoryに保持。128×128・8×70msと原稿のレイヤー状態を保持し、Body/Hairを合成、Sword/Effect/Sampleは提出時の非表示状態を尊重した。旧Effectレイヤーの範囲を使う既存リーチは維持。ガイドだけ出力用の別名コピーで隠し、HorizontalSlashタグを付けて保存後再読込した。原稿と確認用コピーは `Assets/Art/Player/VideoSubmitted/20261025/`。

共有PNG8枚を更新。32PPU/ピボット(32,38)、累積時間 `[4,4,5,4,4,4,4,5]`・全34tick・命中13・復帰境界29tickを保持する。1段目からの接続、2段目での撃破、4段攻撃の既存Clip、Sprite GUID、全Clip/Controllerは保持。個別メニュー `Refresh Submitted Attack 2` を追加し、コマ数/時間が同じなら既存Clipを再生成せず素材だけ更新する。

タメ1.0秒、パリィ9/Just10F・キック16F停止、接触9コマ目17動画F、照明の輪郭0.8秒周期・命中ピーク4F/減光20Fは維持。コード製の集光/放電は削除済みの状態を維持し、手描き電撃は変更しない。

## 検証と範囲

Compile Error/Warning0。関連PlayModeは31ケースを確認し、最終的に全31ケース合格。初回は25合格/6失敗で、6件は以前の揺れテストが成功イベント直後の揺れを期待していたため。実装済みの「成功軽3コマ目で衝撃」に測定点を合わせ、同時にキックの強さ0.42超/上限0.52以下を検証し、30/60/120fps×左右の6件が合格した。ゲーム側のパリィのタイミングは変更していない。

攻撃素材の累積時間/スケール、2段撃破、正面リーチ/背後への誤判定防止、反撃の同一対象、9/16F停止、パリィ逆光4F、キックの強い揺れ/ズーム/Pause/復元を確認。左右各10回＝20回の通し/Resetで、二重ダメージ・誤対象・操作不能、時間/AI/カメラ/VFX/照明の残留なし。全テストや旧80回の検証は再実行しない。

EditMode5/5合格。本編の確認範囲は共有PNG・Art Set/Override・既存Clipの時間とEvent参照まで。動画のPlayMode確認と本編実戦の演出受入を区別し、検証後にvideo/prototypeの使用版を記録する。原稿台帳validate成功・更新待ち0。

動画は275F・4.583333秒、1920×1080/60fps/H.264/無音。全フレームをデコードして確認。全イベント表、セッション開始時計とキックの揺れ以外の状態列は直前版と一致。終端は両敵非表示・提出Idle・VFX0・停止0・倍率1・画角3.375・揺れ0・逆光/照明なし。

収録後/最終Console全種0。元のPrototypeCombatValidationへEditMode・未保存変更なしで戻し、開始時のSceneバイト一致を確認。開始時2480点のうち意図した既存16点以外2464点をバイト保持。既存未コミット変更、他の原稿/旧History、全Clip/Controller、本編Profile/URP設定は保持する。

再現：CombatVideoをPlay、`VideoCaptureUtility.CapturePreview(2,true)`、`Tools/Video/encode-captures.ps1 -Revision kick-shake-attack2-20261025 -Variants 2stage-hybrid`。

今回の差し替え/調整と全体の完成判定は分ける。原稿/演出の最終確認、手描き放電の受領後の接続、最終録画版の確定は残り、#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
