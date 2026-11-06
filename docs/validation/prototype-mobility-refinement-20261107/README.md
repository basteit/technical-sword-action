# 移動感の追調整: Game View確認

2026-11-07、PrototypeCombatValidation、1920×1080、通常カメラSize5.625。敵なし。PlayModeの手動戦闘時計で、元の原稿の5→6コマを6F間隔で確認した。水色の会話テスト箱・専用UIは配置していない。

- [頂点5コマ / ジャンプ17F](Rendering_20261107_074403_776.png): world y=2.964224、鉛直速度3.661497。
- [頂点6コマ / ジャンプ23F](Rendering_20261107_074433_065.png): world y=3.158699。ここで6FのHitstopを要求し3F進めても姿勢・位置が変わらないことを確認した。

地上Dashは14FでNeutralへ戻り、無敵false、通常移動速度5.28228、残り再使用待ち14F、距離3.6unit。自動テストはEditMode343/343、関連PlayMode113/113、全PlayMode927/940（従来と同じ13失敗、名前・Assertionとも一致）。詳細は[実装と検証記録](../../prototype-mobility-refinement-20261107.md)。

髪未完成のジャンプ原稿である。物理ゲームパッド、15分の操作感とDevelopment Build/配布ZIPは未確認・未更新であり、この画像をそれらの完了証跡としない。
