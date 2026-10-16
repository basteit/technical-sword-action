using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded damage measurements using combat time, so pause and hitstop do not dilute DPS.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatTelemetry : MonoBehaviour
{
    private struct Hit { public long tick; public int amount; }
    private readonly Queue<Hit> recent = new();
    private readonly Dictionary<string, int> receivedBySource = new();
    private PlayerAttack2D attack;
    private PlayerParryCounter2D counter;
    private PlayerSpecialSkill2D special;
    private PlayerDamageReceiver2D health;
    private long startTick;
    public int TotalDamage { get; private set; }
    public int TotalReceivedDamage { get; private set; }
    public IReadOnlyDictionary<string, int> ReceivedBySource => receivedBySource;
    public float CumulativeDps => TotalDamage / Mathf.Max(1f / 60f, (CombatTimeController.TickCount - startTick) / 60f);
    public float RecentDps
    {
        get
        {
            while (recent.Count > 0 && CombatTimeController.TickCount - recent.Peek().tick >= 300) recent.Dequeue();
            int amount = 0;
            foreach (Hit hit in recent) amount += hit.amount;
            return amount / 5f;
        }
    }
    private void Start() => Connect();
    private void OnEnable() { if (attack != null) Connect(); }
    private void Connect()
    {
        Disconnect();
        attack = GetComponent<PlayerAttack2D>(); counter = GetComponent<PlayerParryCounter2D>();
        special = GetComponent<PlayerSpecialSkill2D>(); health = GetComponent<PlayerDamageReceiver2D>();
        if (attack != null) attack.HitConfirmed += OnAttackHit;
        if (counter != null) counter.HitConfirmed += OnCounterHit;
        if (special != null) special.HitConfirmed += OnSpecialHit;
        if (health != null) health.DamageReceived += OnDamageReceived;
        ResetMeasurements();
    }
    private void OnAttackHit(Damageable2D enemy, int step, Vector2 point) => RecordDamage(enemy.LastDamageTaken);
    private void OnCounterHit(Damageable2D enemy, Vector2 point) => RecordDamage(enemy.LastDamageTaken);
    private void OnSpecialHit(Damageable2D enemy, int amount) => RecordDamage(amount);
    public void RecordDamage(int amount)
    {
        if (amount <= 0) return;
        TotalDamage += amount;
        recent.Enqueue(new Hit { tick = CombatTimeController.TickCount, amount = amount });
        while (recent.Count > 512) recent.Dequeue();
    }
    private void OnDamageReceived(int amount, string source, long attackId)
    {
        TotalReceivedDamage += amount;
        receivedBySource.TryGetValue(source, out int previous);
        receivedBySource[source] = previous + amount;
    }
    public void ResetMeasurements()
    {
        startTick = CombatTimeController.TickCount;
        recent.Clear(); receivedBySource.Clear();
        TotalDamage = TotalReceivedDamage = 0;
    }
    private void Disconnect()
    {
        if (attack != null) attack.HitConfirmed -= OnAttackHit;
        if (counter != null) counter.HitConfirmed -= OnCounterHit;
        if (special != null) special.HitConfirmed -= OnSpecialHit;
        if (health != null) health.DamageReceived -= OnDamageReceived;
    }
    private void OnDisable() => Disconnect();
}
