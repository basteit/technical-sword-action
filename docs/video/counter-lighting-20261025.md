# カウンターの照明・放電と移行2版目

2026-10-25。「ドット絵以外の範囲で演出を一度変更」「演出ごとの見え方を列挙」「新しいカウンター移行を追加」に対応。移行を更新し、キック本体と手描き電撃の原稿/PNGを保持して、Unityのコードとシェーダーで補助演出を加えた。

[追加演出ありMP4](previews/combat-2stage-hybrid-counter-lighting-after-20261025.mp4)／[追加演出なしMP4](previews/combat-2stage-hybrid-counter-lighting-before-20261025.mp4)／[演出の前後比較画像](previews/counter-lighting-20261025-contact.png)／[移行8コマ](previews/counter-transfer-20261025-contact.png)／[検証JSON](previews/counter-lighting-20261025-validation.json)。**どちらの動画にも新しい移行原稿を使用**し、追加照明だけの差を比較する。

## 演出と見え方

| 演出 | Unity側の変更 | 見え方 |
| --- | --- | --- |
| タメ中の集光 | 5本の細い光の筋を足へ収束させ、タメ後半ほど強くする | 足に電気が集まり、蹴る直前の密度が高くなる |
| 足と人物の青白い光 | 足元の小さな光と、距離に応じた人物の輪郭光を加算 | 足を光源として、主人公と敵が同じ空間にいるように見える |
| 解除時の切り替え | スロー解除の1動画Fで集光を切り、次の命中で強い光へ切り替える | タメから打撃への切り替わりを明確にする |
| 命中の閃光 | 最初3動画Fだけ背景を暗くし、人物を冷たい白青で照らす | 元の蹴りのシルエットが強い明暗の中で浮く |
| ヒットストップ中の放電 | 次の5動画Fで、蹴る方向へ枝状の光を伸ばす | 人物と敵を止めたまま電気だけが外へ抜ける |
| 止め後半の読みやすさ | 残り8動画Fは光を弱くし、背景を通常へ戻す | 長い停止の後半で、接触位置と作画を確認できる |

新しいドットPNGや円の衝撃波は制作していない。追加光はShader内で計算する線・光の広がりと、元の人物Spriteを使った加算照明。受領した電撃の形・色・コマは保持する。新しい光の形を含むため、この追加分は純ドット表現ではない。純ドット切替では追加照明を無効にし、従来の作画電撃を使う。

60fpsで集光は60枚/1.0秒、閃光は220〜222枚目、放電は223〜227枚目、弱い余光は228〜235枚目。236枚目は追加照明なしで同じ接触コマ、237枚目から吹っ飛びが進む。人物/独立電撃の命中9コマ目は従来どおり17動画F保持し、通常パリィ9/Just10F・カウンター16Fの停止を変えない。Pause中は光の時間も止め、戦闘時計やAnimatorを追加で進めない。

## 新しい移行原稿

`ArtSource/Player/Current/Player_counter_transfer.aseprite` へ2版目を登録。SHA256は `460ccce461d0640ffacc70f964f9c36c676fd8b8bc726be0aeeec84f9ae3ec07`。旧版 `299dc684…` はHistoryに保持する。元のDownloadsは変更しない。

原稿は64×64・8×100ms。Hair/Body/Effect/Eyeを原稿の表示どおりにAsepriteで合成し、Eyeの不透明度128も保持。ガイドだけを出力/別名確認コピーで非表示にし、元原稿のレイヤー・表示時間・表示状態は変えない。Actionタグ付き確認コピーを保存後にAsepriteで再読込した。日付付き原稿と確認コピーは `Assets/Art/Player/VideoSubmitted/20261025/` に保持する。

32PPU/ピボット(32,38)、ゲーム用12tick `[1,1,2,2,2,1,1,2]` と既存Sprite GUIDを維持。動画Clip/Controllerの既存未コミット変更を上書きしない。`Refresh Submitted Counter Transfer` の個別メニューは共有PNG/Libraryを再読込し、当該ClipとControllerを再生成しない。素材台帳の仮提出区分は維持し、受領と最終受入を分ける。

## 実装と確認

`VideoCounterLighting` と専用Shader/Materialを追加。撮影カメラで描画する直前に表示し、描画後に解除する。別のカメラへ影響させない。タメ中は実際に弾いた敵、命中後は実際に蹴った同じ敵へ照明を固定し、撃破ポーズの敵も表示中は照らす。Reset/SessionReset/Disable/対象消失で表示と参照を解除し、対象が戻っても古い演出を再開しない。

素材の時刻・判定・対象固定・無敵・移動・吹っ飛び・カメラ距離・揺れ強度を保持。パリィの黄色保持/白い逆光4Fも保持する。3撮影Sceneには新Materialの参照を1行ずつ追加し、Scene全体を再生成しない。撮影Scene Builderも参照を接続する。

Unity Compile Error/Warning0・シェーダー診断0。**関連PlayMode57/57、EditMode2/2合格**。新しい照明の18ケースでは30/60/120fps×左右、停止中の実時間変化/Pause、Reset/Disable/SessionReset/対象消失と再有効化、別カメラの実画素、純ドット/比較切替を確認した。回帰35件はパリィ逆光/停止、独立電撃と人物の同期、素材/ピボット/同対象反撃/Studio等。移行接続4件は2/4段・左右・30/60/120fpsの既存組み合わせを確認した。

左右各10回＝20回の通し/Resetで、二重ダメージ・誤対象・操作不能や時間/カメラ/AI/VFX/追加照明の残留なし。本編の確認は共有Sprite/素材Snapshot/Clip時間表のEditMode2件で、実戦の演出受入とは区別する。全件/旧80回の組み合わせは再実行しない。

**各275F / 4.583333秒・1920×1080・60fps・H.264・無音**。両版を全フレームデコードし、イベント表と全フレームの作画コマ/対象/時計/カメラを比較した。追加照明の状態列とセッション時計の開始値以外は一致。終端は両敵非表示・提出Idle・VFX0・停止0・倍率1・カメラ/揺れ復元・パリィ逆光とカウンター照明なし。

収録/終了後のConsole全種0。元のPrototypeCombatValidationへEditMode/未保存変更なしで復帰。素材再読込で未保存になった同Sceneは既存の保存・続行の指示に従って保存し、開始時とのバイト一致を確認した。開始時2450ファイル中、意図した既存20点以外2430点をバイト保持。旧History、他動作、全Clip/Controller、キック/電撃のPNG、既存未コミット変更と本編設定を保持し、今回39ファイルのRepository監査Passed。原稿登録、Unity反映、確認後の使用版記録は別々に実施し、台帳validate成功・更新待ち0。

## 再現

`CombatVideo.unity` をPlayにし、VideoPresentationの `counterLightingEnabled=true` で `VideoCaptureUtility.CapturePreview(2,true)`。書き出しは `Tools/Video/encode-captures.ps1 -Revision counter-lighting-after-20261025 -Variants 2stage-hybrid`。比較版はPlayMode中に同じフラグをfalseにして収録し、`-Revision counter-lighting-before-20261025` で書き出す。収録後にフラグをtrueへ戻す。

素材更新は `player_source_library.py register --id counter_transfer --source <原稿>` → Asepriteの `export-counter-submissions.lua`（kind=transfer）→共有8PNGを更新→個別メニューで再読込→検証→`record-use` の順。原稿の構成やコマ数が変わった場合はExporterの検査を通して時間表とClipを確認する。今回の更新ではコマ数・時間表を変えない。

今回の追加演出版と動画全体の最終受入は区別する。演出と原稿の最終確認、最終録画版の確定が残るため、#69/#70/#71/#72/#74/#76はOpen、#73/#75はClosedを維持する。
