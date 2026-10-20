# Windows試遊ZIP（#30 / #42）

目的: Unityを持っていない第三者が、現在の戦闘プロトタイプを展開して操作確認できるようにする。製品版の公開・素材承認・Steam発売判定とは分ける。

受入条件:

1. PrototypeCombatValidationだけを含むWindows 64bitのDevelopment Buildと、実行に必要なフォルダ・DLL・README・ビルド識別情報をZIPにする。製品用Scene一覧は変更しない。
2. READMEに起動・終了・実入力の操作・敵の出し方・3秒のパリィ反撃・仮素材と未完成部分を記載する。ソース、原稿、Editor用DLL、不要なデバッグ情報を配布しない。
3. ZIPの全ファイルをハッシュ照合し、Standalone版の起動と基本入力を確認する。実機ゲームパッド・15分の操作感・他PCの互換性は別途人間の受入として残す。

## 再作成

- EditorのPlayModeを止め、未保存Sceneを確認する。
- `Tools > Technical Sword Action > Prototype > Build Windows Playtest`、またはULoopから `PrototypePlaytestBuild.Queue();` を実行する。
- `PrototypePlaytestBuild.Status` と `Builds/Playtest/<buildId>/BuildReport.json` で成功を確認する。
- `Tools/Build/Package-Prototype.ps1 -BuildReport <BuildReport.json>` でZIPを作る。既存の同名パッケージは上書きしない。

ビルド中だけ1280×720・ウィンドウ・サイズ変更可能へ切り替え、成功／失敗のいずれでも元のPlayerSettingsへ復帰する。Sceneや素材は自動で保存・破棄しない。既存の作業中変更を含む場合はBUILD_INFO.jsonのworkingTreeDirtyで区別する。パッケージは生成物としてGit対象外とする。
