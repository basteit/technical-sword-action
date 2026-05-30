---
name: new-feature-design
description: Unity 2D剣戟アクションRPGで新機能を追加するときに、設計方針・実装順序・テスト観点を統一するために使う。Use when adding any new gameplay/system feature, including player actions, enemy behavior, combat rules, gauges, skills, UI feedback, or timing windows.
---

# New Feature Design Policy

## Core Rule
- 先に体験目的を1文で定義する。
- 体験目的が曖昧なら実装しない。

## Feature Entry Checklist
- 既存Issueに紐づける。なければ先にIssueを作る。
- 受け入れ条件（DoD）を先に3項目書く。
- 「成功時の気持ちよさ」と「失敗時の納得感」を両方定義する。

## Implementation Order
1. 最小のロジックを作る（数値は仮でよい）。
2. デバッグ可視化を入れる（判定/状態/タイマー）。
3. 音・ヒットストップ・フラッシュなど最小演出を入れる。
4. 既存システムとの競合を止める（入力ロック/状態遷移）。
5. 最後に数値調整する。

## System Compatibility Rules
- 新しい行動を追加するときは `PlayerStateMachine` に状態を追加する。
- 行動ロックが必要なら `Motor/Attack/Parry/Special` の全入力経路で止める。
- ダメージ系機能は `IDamageReceiver2D` と `Damageable2D` の両対応を意識する。
- 弾系機能は `owner` と `targetLayers` の切替を設計に含める。

## Timing/Balance Defaults
- 入力受付は短めから始めて、テストで緩める。
- ハイリターン行動には明確な隙を付ける。
- 連打防止には `cooldown` か `fail lock` を入れる。

## Visual/Audio Feedback Rule
- 判定がある行動は、少なくとも1つの視覚フィードバックを持つ。
- 成功/失敗でSEを分ける。
- 調整中はヒットボックス可視化をONにする。

## Test Protocol (Quick)
- 15分テストを1セット実施する。
- 記録する:
  - 成功率
  - 理不尽被弾回数
  - 期待どおりでない挙動
- 1回の調整で変更する軸は最大2つまでにする。

## Done Criteria
- DoDをすべて満たす。
- 既存機能の明確な退行がない。
- デバッグ表示で状態遷移が説明できる。

## Commit/PR Rule
- 1機能1意図でコミットする。
- PR本文に「何を良くしたか」と「何を悪化させるリスクがあるか」を書く。
