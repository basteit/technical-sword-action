using UnityEngine;

/// <summary>Gameplay-only drawing. Does not move the body, run AI or issue hits.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnemyMeleeChaser2D))]
public sealed class PrototypeEnemyArtPresentation : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField] private SpriteRenderer artwork;
    private EnemyMeleeChaser2D ai;
    private Damageable2D health;
    private EnemyCounterLaunch2D launch;
    private Vector3 restPosition;
    private int elapsed, attackElapsed, hitRemaining, deathElapsed;
    private bool parried, attackRecoveryVisible, deathFacingLeft;
    public int CombatTickOrder => 320;
    public string MotionName { get; private set; } = "Idle";
    public SpriteRenderer Artwork => artwork;
    public VideoArtLibrary Library => library;

    private void Awake()
    {
        ai = GetComponent<EnemyMeleeChaser2D>(); health = GetComponent<Damageable2D>();
        launch = GetComponent<EnemyCounterLaunch2D>();
        if (artwork != null) restPosition = artwork.transform.localPosition;
    }

    private void OnEnable()
    {
        health.HitReceived += OnHit; health.Parried += OnParried;
        health.Died += OnDied; health.ResetPerformed += OnReset;
        ai.AttackStarted += OnAttack;
        CombatTimeController.Register(this);
        ResetDrawing();
    }

    private void OnAttack() { attackElapsed = 0; attackRecoveryVisible = true; Show(); }
    private void OnHit(Damageable2D _, int damage, Vector2 direction)
    {
        hitRemaining = library?.Find("Enemy_Hit")?.TotalFrames ?? 6;
        parried = attackRecoveryVisible = false; Show();
    }
    private void OnParried(Damageable2D _, ParryResult result)
    {
        parried = true; hitRemaining = 0; attackRecoveryVisible = false; Show();
    }
    private void OnDied(Damageable2D _, Vector2 direction)
    {
        deathElapsed = 0; deathFacingLeft = ai.Facing.x < 0;
        // Keep only the drawing alive. HP, colliders and combat participation already ended.
        health.HoldDeathPresentation(this, 2 * CombatTimeController.StepSeconds);
        Show();
    }
    private void OnReset(Damageable2D _) => ResetDrawing();
    private void ResetDrawing()
    {
        elapsed = attackElapsed = hitRemaining = deathElapsed = 0;
        parried = attackRecoveryVisible = false;
        health.ReleaseDeathPresentation(this);
        if (artwork != null) artwork.color = Color.white;
        Show();
    }

    public void CombatTick()
    {
        elapsed++;
        if (attackRecoveryVisible) attackElapsed++;
        if (!health.IsAlive)
        {
            // The existing launch owns root/parent motion; fade after it lands, not mid-flight.
            if (launch != null && launch.IsActive) deathElapsed = 0; else deathElapsed++;
            int frames = library?.Find("Enemy_Death")?.TotalFrames ?? 39;
            if (deathElapsed < frames) health.HoldDeathPresentation(this, 2 * CombatTimeController.StepSeconds);
            else health.ReleaseDeathPresentation(this);
        }
        else if (!health.IsStunned) parried = false;
        Show();
    }

    public void CombatTickTimers()
    {
        if (hitRemaining > 0) hitRemaining--;
        Show(); // after AI timers / hit resolution, including exact contact and parry ticks
    }

    private void Show()
    {
        if (artwork == null || library == null || ai == null || health == null) return;
        Sprite sprite;
        if (!health.IsAlive)
        {
            MotionName = "Death";
            var death = library.Find("Enemy_Death"); sprite = death?.At(0);
            float total = death != null ? Mathf.Max(1, death.TotalFrames) : 39;
            Color color = artwork.color;
            color.a = 1f - Mathf.Clamp01((deathElapsed / total - .6f) / .4f);
            artwork.color = color;
        }
        else if (hitRemaining > 0) { MotionName = "Hit"; sprite = library.Find("Enemy_Hit")?.At(0); }
        else if (health.IsParryStaggered || (parried && health.IsStunned))
        { MotionName = "Parried"; sprite = library.Find("Enemy_Parried")?.At(0); }
        else if (ai.IsWindingUp)
        {
            MotionName = "Windup";
            var windup = library.Find("Enemy_Windup");
            int total = Mathf.Max(1, Mathf.RoundToInt(ai.WindupDuration * 60));
            int frame = Mathf.Clamp(total - ai.WindupRemainingFrames, 0, total - 1);
            sprite = windup?.At(Mathf.FloorToInt(frame * windup.TotalFrames / (float)total));
        }
        else if (ai.ActiveRemainingFrames > 0)
        {
            MotionName = "Attack";
            var contact = library.Find("Enemy_Windup");
            sprite = contact?.At(contact.TotalFrames - 1);
        }
        else if (attackRecoveryVisible && ai.CurrentStateName == "Recovery")
        {
            MotionName = "Attack";
            var recovery = library.Find("Enemy_Attack");
            int active = ai.Definition != null ? ai.Definition.activeFrames : 1;
            int frames = ai.Definition != null ? ai.Definition.recoveryFrames : 30;
            sprite = recovery?.At(Mathf.FloorToInt(Mathf.Max(0, attackElapsed - active) * recovery.TotalFrames / (float)Mathf.Max(1, frames)));
        }
        else
        {
            attackRecoveryVisible = false;
            MotionName = ai.CurrentStateName == "Approach" ? "Move" : "Idle";
            sprite = library.Find("Enemy_" + MotionName)?.At(elapsed, true);
        }
        if (sprite != null) artwork.sprite = sprite;
        artwork.flipX = !health.IsAlive ? deathFacingLeft : ai.Facing.x < 0;
        artwork.transform.localPosition = restPosition + (MotionName == "Death" ? Vector3.down * (2f / 32f) : Vector3.zero);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        if (health != null)
        {
            health.HitReceived -= OnHit; health.Parried -= OnParried;
            health.Died -= OnDied; health.ResetPerformed -= OnReset;
            health.ReleaseDeathPresentation(this);
        }
        if (ai != null) ai.AttackStarted -= OnAttack;
        if (artwork != null) { artwork.transform.localPosition = restPosition; artwork.color = Color.white; }
    }
}
