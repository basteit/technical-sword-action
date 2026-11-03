using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit encounter-local roles, from windup through recovery. No global video ownership.</summary>
public sealed class EnemyAttackCoordinator2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int maximumAttackers = 1;
    [SerializeField, Min(0)] private int minimumStartGapFrames = 18;
    private readonly HashSet<EnemyMeleeChaser2D> attackers = new();
    private long nextStartTick;
    public int ActiveAttackers => attackers.Count;
    public int MaximumAttackers => Mathf.Max(1, maximumAttackers);
    public int StartCount { get; private set; }

    public bool TryAcquire(EnemyMeleeChaser2D enemy)
    {
        if (!isActiveAndEnabled || enemy == null) return false;
        if (attackers.Contains(enemy)) return true;
        if (enemy.PlayerIsInCounterResponse) return false;
        if (attackers.Count >= MaximumAttackers || CombatTimeController.TickCount < nextStartTick) return false;
        attackers.Add(enemy);
        nextStartTick = CombatTimeController.TickCount + Mathf.Max(0, minimumStartGapFrames);
        StartCount++;
        return true;
    }

    public void Release(EnemyMeleeChaser2D enemy) => attackers.Remove(enemy);
    public void ResetEncounter()
    {
        attackers.Clear(); nextStartTick = 0; StartCount = 0;
    }
    private void OnDisable() => ResetEncounter();
}
