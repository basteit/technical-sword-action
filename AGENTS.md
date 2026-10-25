# 作業場所

このプロジェクトの変更、Unity検証、GitHubへの反映は `C:\Users\hirot\GitHub\technical-sword-action` を基準に一つの作業フォルダで進める。別worktreeや複製フォルダを作業先にするのは、ユーザーが明示的に指定した場合だけにする。

着手前にこのフォルダのブランチと `git status` を確認し、既存の未コミット変更を保持する。新しい作業はこのフォルダ内のブランチへまとめ、GitHubへの反映もそのブランチから行う。既存の変更と競合したら内容を統合し、上書きしない。

# プレイヤー素材

プレイヤーの原稿を追加・更新するときは `ArtSource/Player/README.md` と `catalog.json` を先に確認する。`Tools/Art/player_source_library.py register` で現在版と変更しない履歴を保持し、受領原稿とCodex製ラフを区別する。原稿登録とUnityへの反映・検証は別に記録し、未反映の版を使用中や完成済みとして扱わない。
