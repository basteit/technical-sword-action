# パリィ火花の逆光・黄色保持と白火花の比較

2026-10-25。「長さはこのくらい」「火花の逆光で主人公と敵が白く光って見える」「黄色い火花を残す版も」に対応。**パリィ成功軽の3コマ目から4動画F（0.066667秒）**を保ち、火花の接触点を光源として、主人公と実際に弾いた敵だけを白く照らす。黄色い作画火花を残す版を標準とし、火花も白く光らせる比較版を保存した。

[黄色保持のMP4](previews/combat-2stage-hybrid-parry-backlight-gold-20261025.mp4)／[白火花の比較MP4](previews/combat-2stage-hybrid-parry-backlight-white-20261025.mp4)／[接触・復帰の連続画像](previews/parry-backlight-20261025-contact.png)／[検証JSON](previews/parry-backlight-20261025-validation.json)／[前版の白黒反転4F](parry-update-invert4-20261025.md)。チャットにも各版を再生できるプレビューを用意した。

## 見え方

- 背景と床を暗く落とし、人物のシルエット、髪の形、元の透明部分を保って白く照らす。接触点側の輪郭を強くし、内部には淡い青白さと明暗を少し残す。
- ドット＋短い発光の版では、火花の背後に小さな光の広がりを付ける。純ドットではこの柔らかい補助光だけを切る。
- 黄色保持では、受領原稿に描かれた火花の色と形を使う。白火花の比較版は同じ形を白く照らす。元のPNGやAsepriteを描き換えない。
- 旧Codex製パリィの十字・火花・輪は非表示のまま。画面全体の白黒反転も標準では使わない。

132〜135枚目（動画PTS 2.183333〜2.233333秒）だけに適用し、136枚目の先頭（2.250000秒）から通常色へ戻す。パリィ3コマ目の全10動画Fのうち、残り6枚は通常色で読める。カメラの揺れのピークと敵のdown開始は132枚目のまま。

## 実装と保持範囲

`VideoParryBacklight` が、撮影カメラ・主人公SpriteRenderer・実際の攻撃元・原稿の接触座標を受け取る。接触時だけ同じSprite/左右反転/透明度の表示を重ね、カメラの描画前後で有効範囲を限定する。別カメラの描画とScene Viewには適用しない。Reset/SessionReset/Disable/対象消失で参照と表示を解除し、Scene破棄時に生成表示を解放する。

`VideoParryBacklight.shader` と専用Materialを追加。撮影・攻防切替・自由操作Studioの3SceneにはMaterial参照1行ずつを追加し、Scene全体の再生成/再保存は行わない。今後の撮影Scene生成でも同じMaterialを接続する。従来の反転用Feature/Shaderは比較用に保持する。

原稿・出力PNG・Clip・共有Art Library・素材台帳はすべて開始時とバイト一致。新しい原稿の受領や登録はない。待機2版目/成功軽3版目と後ろ髪、通常9/Just10F・キック16F停止、タメ1.0秒/直前8コマ目7動画F/解除→命中1tick、全15キックコマ、反撃対象/無敵、配置・カメラ距離・揺れ強度は保持した。本編側の素材/判定/Profile/URP設定も変更しない。

## 検証

Unity Compile Error/Warning 0、シェーダー診断0。**関連PlayMode34/34合格**。30/60/120fpsの4実時間F、左右の接触同期、実画素での両人物の白い照明と黄色保持/白火花、純ドット/混合、別カメラへの非適用、Pause/Reset/SessionReset/Disable/対象消失、従来Featureのカメラ破棄順、9/Just10F・キック16F、待機受付、Studioの実攻撃元と同じ敵への反撃を確認した。

左右各10回＝20回の通し/Resetで、二重ダメージ・別対象への反撃・操作不能や、時間倍率・カメラ・AI・VFX・逆光の残留なし。全件/旧80回の組み合わせとEditModeは今回は再実行しない。初回の画素テストで見つかったMaterialPropertyBlockの初期化位置をAwakeへ修正し、最終34件は全合格した。

**各275F / 4.583333秒・1920×1080・60fps・H.264・無音**。2本とも全フレームをデコード。全イベント表と、逆光/反転の状態列・セッション時計の開始値以外の全フレーム記録が前版と一致した。2本の未圧縮接触画像の差は、原稿の黄色火花8625画素の色だけ。終端は両敵非表示・VFX0・停止0・倍率1・カメラ/揺れ復元・逆光/反転なし・提出Idle。

テスト直後のConsoleはTest Runnerの情報2件だけで、Error/Warning 0。情報ログを整理後のConsole全種0。元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰した。開始時2437ファイル中、意図した既存9点以外2428点と既存未コミット変更をバイト保持。固定フォルダと同じ作業ブランチから反映する。

## 再現と調整

`CombatVideo.unity` をPlayにし、`VideoCaptureUtility.CapturePreview(2,true)` で黄色保持版を収録する。書き出しは `Tools/Video/encode-captures.ps1 -Revision parry-backlight-gold-20261025 -Variants 2stage-hybrid`。白火花との比較は、PlayModeで撮影前だけ次の設定を変更し、収録後に必ず標準へ戻す。

```csharp
var presentation = UnityEngine.Object.FindFirstObjectByType<VideoCombatPresentation>();
var settings = new UnityEditor.SerializedObject(presentation);
settings.FindProperty("preserveParrySparkColor").boolValue = false;
settings.ApplyModifiedPropertiesWithoutUndo();
try { return VideoCaptureUtility.CapturePreview(2, true); }
finally {
    settings.Update();
    settings.FindProperty("preserveParrySparkColor").boolValue = true;
    settings.ApplyModifiedPropertiesWithoutUndo();
}
```

白火花の書き出しは同じコマ数を `-Revision parry-backlight-white-20261025 -Variants 2stage-hybrid` で保存する。先に黄色版をエンコードしてから白版を収録し、共用の連番を上書きしても前者のMP4/イベント表を保つ。

設定は `parryBacklight=true`・`preserveParrySparkColor=true` が標準。従来名の `parryInversionFrames=4` は逆光にも共通の表示時間で、`parryBacklight=false` なら前版の反転を比較できる。0なら従来の短い黒Flash。ゲームのヒットストップとは別の実時間で管理する。イベント表はStepCapture終了時刻、動画PTSはフレーム先頭なので、表示との同期はフレーム番号で確認する。

今回の演出比較と動画全体の最終受入は区別する。残件は原稿の最終版確認、全体の接触/VFX密度と最終録画版の確定。#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
