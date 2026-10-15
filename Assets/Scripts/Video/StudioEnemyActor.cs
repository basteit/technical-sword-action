using UnityEngine;

/// <summary>Training controls wrap the real melee AI; attacks still resolve through its hit context.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMeleeChaser2D))]
public sealed class StudioEnemyActor : MonoBehaviour
{
    [SerializeField] private StudioEnemySetup setup = new StudioEnemySetup();
    private Damageable2D health;
    private EnemyMeleeChaser2D ai;
    private Transform target;
    private long nextAttackTick;
    private bool singleInFlight, initialAttackStarted;
    public Damageable2D Health => health != null ? health : GetComponent<Damageable2D>();
    public EnemyMeleeChaser2D AI => ai != null ? ai : GetComponent<EnemyMeleeChaser2D>();
    public StudioEnemySetup Setup => setup;
    public bool IsReady => Health.IsAlive && !Health.IsStunned && !singleInFlight &&
        (AI.CurrentStateName == "Idle" || AI.CurrentStateName == "Approach");

    public void Configure(StudioEnemySetup value, Transform player)
    {
        setup = value.Copy(); target = player;
        health = GetComponent<Damageable2D>(); ai = GetComponent<EnemyMeleeChaser2D>();
        transform.position = setup.position;
        if (Application.isPlaying) ResetActor();
#if UNITY_EDITOR
        else UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
    }

    public void CaptureHome()
    {
        setup.position = transform.position;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
    }

    public void ResetActor()
    {
        if (health == null) health = GetComponent<Damageable2D>();
        if (ai == null) ai = GetComponent<EnemyMeleeChaser2D>();
        health.ResetForCapture(setup.position, Mathf.Max(1, setup.hp));
        ai.ConfigureForTraining(target, setup.windupSeconds, setup.intervalSeconds, setup.damage, setup.moveSpeed);
        ai.ResetForCapture(target, setup.mode != StudioEnemyMode.Chase);
        GetComponent<VideoEnemyAnimationDriver>()?.BindPlayer(target);
        singleInFlight = initialAttackStarted = false;
        nextAttackTick = CombatTimeController.TickCount + Mathf.CeilToInt(setup.intervalSeconds * 60);
    }

    public bool AttackOnce()
    {
        if (!IsReady || target == null) return false;
        if (!ai.BeginCaptureAttack()) return false;
        initialAttackStarted = true;
        singleInFlight = setup.mode != StudioEnemyMode.Chase;
        return true;
    }

    public void Step()
    {
        if (!Health.IsAlive || !isActiveAndEnabled) return;
        // Hold only after the actual attack recovery; never restart a windup or repeat a hit.
        if (singleInFlight && AI.CurrentStateName == "Cooldown")
        {
            ai.ResetForCapture(target);
            singleInFlight = false;
            nextAttackTick = CombatTimeController.TickCount + Mathf.CeilToInt(setup.intervalSeconds * 60);
        }
        if ((setup.mode == StudioEnemyMode.RepeatAttack || setup.mode == StudioEnemyMode.SingleAttack && !initialAttackStarted) && !singleInFlight &&
            CombatTimeController.TickCount >= nextAttackTick) AttackOnce();
    }
}
