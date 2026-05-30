# W3-D20 Balance Pass (2026-05-30)

## Target Feel (Normal Difficulty)
-爽快感重視（狙った実感はある）
-難しすぎないが連打は通らない
-少し距離があってもパリィが吸い付く

## Updated Values
### Parry Timing
- Parry Window: `0.20s` (from `0.16s`)
- Just Window: `0.07s` (from `0.05s`)

### Anti-Spam
- Parry Cooldown: `0.22s` (from `0.14s`)
- Parry Fail Lock: `0.30s` (from `0.24s`)

### Parry Snap
- Parry Snap Distance: `1.9`
- Hit source distance within snap range can resolve parry.

## Updated Scripts
- `Assets/Scripts/Player/PlayerParry2D.cs`
- `Assets/Scripts/Player/PlayerDamageReceiver2D.cs`

## Next Test Focus
1. 通常プレイ15分でパリィ成功率 `30% - 45%` を維持できるか
2. パリィ連打時の失敗リスクが十分に働くか
3. 遠隔弾/近接攻撃で「少し届く」吸い付き感があるか
