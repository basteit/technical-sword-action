# Week2 Balance Pass (2026-05-15)

## Updated Baseline Values
### Parry
- Parry Window: `0.16s`
- Just Window: `0.05s`
- Parry Cooldown: `0.14s`
- Parry Fail Lock: `0.24s`

### Parry Feedback
- Normal HitStop: `0.035s`
- Just HitStop: `0.06s`

### Damage Reaction
- Hit Lock: `0.16s`

### Special Gauge Gain
- On Attack Hit: `+6`
- On Damaged: `+10`
- On Normal Parry: `+12`
- On Just Parry: `+20`

### Special Skill
- Required Gauge: `60`
- Damage: `7`
- Startup Lock: `0.24s`
- Recovery Lock: `0.55s`

## 15-Min Test Checklist
1. 3分: 近接のみでパリィ成功率確認
2. 3分: 遠隔弾反射ループ確認
3. 3分: 被弾リスク体感（連打対策）
4. 3分: 必殺技回転率確認
5. 3分: 総合の爽快感/理不尽感メモ

## Target Ranges (temporary)
- Parry success rate: `30% - 45%`
- Just parry rate: `8% - 15%`
- Special uses per 5 min: `3 - 6`
- "Unfair hit" memo count: `<= 3 / 15min`

## Next Tuning Levers
- Too easy: shorten parry window to `0.14s`
- Too hard: extend parry window to `0.18s`
- Special too frequent: raise required gauge to `70`
- Special too rare: lower required gauge to `50`
