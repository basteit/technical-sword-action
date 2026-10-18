# 敵の受領素材

`Submissions/20261019/` はユーザー提出のAseprite原本5点。元のレイヤー・表示時間・画素を保持する。更新時は版名を増やし、受領済み原稿を上書きしない。

Unity用は `Assets/Art/Enemy/Submitted/20261019/`。`Tools/Art/export-enemy-submissions.lua` で黒一色のガイドレイヤーだけを非表示にして出力する。

| 原稿 | 接続 |
| --- | --- |
| idle | 待機、10枚・1秒ループ |
| walk | 移動、10枚・1秒ループ |
| attack | 1〜7枚目を予兆30tick、8〜14枚目を攻撃16tick |
| chance | パリィやられ。未提出の通常被弾にも一時流用 |
| down | 撃破。接触停止後に表示して消滅 |

32PPU、Pivot=(32px,38px)。攻撃だけ128px、他64pxのキャンバスをそのまま使う。原稿の攻撃2秒は保持し、撮影の予兆/命中時刻へ接続するゲーム用Clipだけを短縮する。`EnemySubmissionSetup.Apply()` は敵のLibrary参照と7Clipだけを更新し、主人公のClipやSceneを再生成しない。撮影/Studioの共通敵に適用し、本編の別敵Prefabは今回の対象に含めない。
