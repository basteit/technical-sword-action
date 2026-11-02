using UnityEngine;

/// <summary>Gameplay-only drawing. Does not move the body, run AI or issue hits.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnemyMeleeChaser2D))]
public sealed class PrototypeEnemyArtPresentation : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField] private SpriteRenderer artwork;
    [SerializeField] private bool useDownParryPose;
    private EnemyMeleeChaser2D ai;
    private Damageable2D health;
    private EnemyCounterLaunch2D launch;
    private Vector3 restPosition;
    private int elapsed, attackElapsed, hitRemaining, deathElapsed;
    private bool parried, attackRecoveryVisible, deathFacingLeft;
    private bool counterFacingHeld, counterFacingLeft;
    private object parryDrawingOwner;
    private bool capturedAttackForParry;
    private int capturedAttackFrame, parryDrawingFrame;
    private int parryNaturalLeadFrames, parryDrawingLeadFrames;
    public int ParryImpactSourceFrame => library?.Find("Enemy_Attack")?.parryImpactFrame ?? 0;
    public int CombatTickOrder => 320;
    public string MotionName { get; private set; } = "Idle";
    public SpriteRenderer Artwork => artwork;
    public VideoArtLibrary Library => library;
    public bool UsesDownParryPose => useDownParryPose;
    public bool IsCounterFacingHeld => counterFacingHeld;
    public bool HasAuthoredParryCue => isActiveAndEnabled && artwork != null && artwork.enabled &&
        library != null && library.Find("Enemy_Windup") is { HasValidParryCue: true };
    public int WindupSourceFrame { get; private set; } = -1;
    public bool IsAuthoredParryCueVisible => HasAuthoredParryCue && ai != null && ai.IsWindingUp &&
        health != null && health.IsAlive && !health.IsStunned &&
        WindupSourceFrame >= library.Find("Enemy_Windup").parryCueFrame &&
        WindupSourceFrame < library.Find("Enemy_Windup").parryCueFrame + library.Find("Enemy_Windup").ParryCueCelFrames;
    public void SetDownParryPose(bool value) { useDownParryPose = value; Show(); }

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
        if (launch != null) launch.LaunchStarted += OnCounterLaunch;
        CombatTimeController.Register(this);
        ResetDrawing();
    }

    private void OnAttack() { attackElapsed = 0; attackRecoveryVisible = true; Show(); }
    private void OnHit(Damageable2D _, int damage, Vector2 direction)
    {
        parryDrawingOwner = null;
        hitRemaining = library?.Find("Enemy_Hit")?.TotalFrames ?? 6;
        parried = attackRecoveryVisible = false; Show();
    }
    private void OnParried(Damageable2D _, ParryResult result)
    {
        capturedAttackForParry = attackRecoveryVisible;
        capturedAttackFrame = Mathf.Max(0, attackElapsed - 1);
        parried = true; hitRemaining = 0; attackRecoveryVisible = false; Show();
    }
    public bool TryBeginParryDeflection(object owner, int maxApproachFrames, out int delayFrames)
    {
        delayFrames = 0;
        var motion = library?.Find("Enemy_Attack");
        if (owner == null || !capturedAttackForParry || motion == null || !motion.HasValidParryImpact ||
            !isActiveAndEnabled || health == null || !health.IsParryStaggered) return false;
        parryNaturalLeadFrames = ParryImpactDelayFrames(motion, ai.AttackActiveFrames, ai.AttackRecoveryFrames, capturedAttackFrame);
        delayFrames = parryDrawingLeadFrames = Mathf.Min(parryNaturalLeadFrames, Mathf.Max(0, maxApproachFrames));
        parryDrawingOwner = owner;
        parryDrawingFrame = SampleParryDeflectionFrame(capturedAttackFrame, parryNaturalLeadFrames, parryDrawingLeadFrames, 0);
        Show(); return true;
    }
    public bool IsParryDeflectionOwnedBy(object owner) => ReferenceEquals(parryDrawingOwner, owner) && parryDrawingOwner != null;
    public void SampleParryDeflection(object owner, int elapsedFrames)
    {
        if (!IsParryDeflectionOwnedBy(owner)) return;
        parryDrawingFrame = SampleParryDeflectionFrame(capturedAttackFrame, parryNaturalLeadFrames, parryDrawingLeadFrames, elapsedFrames); Show();
    }
    public void ReleaseParryDeflection(object owner)
    { if (IsParryDeflectionOwnedBy(owner)) { parryDrawingOwner = null; Show(); } }
    public static int ParryImpactDelayFrames(VideoArtLibrary.Motion motion, int active, int recovery, int elapsedAttack)
    {
        if (motion == null || !motion.HasValidParryImpact) return 0;
        return Mathf.Max(0, Mathf.Max(1, active) + Mathf.CeilToInt(motion.parryImpactFrame * Mathf.Max(1, recovery) / (float)motion.TotalFrames) - Mathf.Max(0, elapsedAttack));
    }
    /// <summary>Compress only the already-parried attack drawing, reaching the same authored peak sooner.</summary>
    public static int SampleParryDeflectionFrame(int captured, int naturalLead, int displayLead, int elapsed)
    {
        captured = Mathf.Max(0, captured); naturalLead = Mathf.Max(0, naturalLead);
        displayLead = Mathf.Clamp(displayLead, 0, naturalLead); elapsed = Mathf.Max(0, elapsed);
        if (displayLead == 0 || elapsed >= displayLead)
            return captured + naturalLead + elapsed - displayLead;
        return captured + (int)System.Math.Floor(elapsed * (double)naturalLead / displayLead);
    }
    private void OnDied(Damageable2D _, Vector2 direction)
    {
        parryDrawingOwner = null;
        deathElapsed = 0; deathFacingLeft = counterFacingHeld ? counterFacingLeft : ai.Facing.x < 0;
        // Keep only the drawing alive. HP, colliders and combat participation already ended.
        health.HoldDeathPresentation(this, 2 * CombatTimeController.StepSeconds);
        Show();
    }
    private void OnCounterLaunch(int direction)
    {
        parryDrawingOwner = null;
        // The kick travels away from its attacker. Keep the drawing facing back toward them,
        // without changing the AI direction that determined the rear approach.
        counterFacingHeld = true; counterFacingLeft = direction > 0;
        deathFacingLeft = counterFacingLeft;
        Show(); // includes the lethal contact/hitstop tick, after Died has already fired
    }
    private void OnReset(Damageable2D _) => ResetDrawing();
    private void ResetDrawing()
    {
        parryDrawingOwner = null; capturedAttackForParry = false;
        elapsed = attackElapsed = hitRemaining = deathElapsed = 0;
        parried = attackRecoveryVisible = false;
        counterFacingHeld = false;
        health.ReleaseDeathPresentation(this);
        // Arena reset notifies health before resetting AI. Do not resample its old windup here.
        MotionName = "Idle";
        WindupSourceFrame = -1;
        if (artwork != null)
        {
            artwork.color = Color.white;
            artwork.sprite = library?.Find("Enemy_Idle")?.At(0);
            artwork.transform.localPosition = restPosition;
            artwork.flipX = ai.Facing.x < 0;
        }
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
        else if (!health.IsStunned)
        {
            parried = false;
            if (launch == null || !launch.IsActive) counterFacingHeld = false;
        }
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
        WindupSourceFrame = -1;
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
        else if (parryDrawingOwner != null && health.IsParryStaggered)
        {
            var attack = library.Find("Enemy_Attack");
            int active = ai.AttackActiveFrames;
            int frame = Mathf.Max(0, parryDrawingFrame - active);
            int peak = ParryImpactDelayFrames(attack, active, ai.AttackRecoveryFrames, 0);
            MotionName = parryDrawingFrame >= peak ? "ParryImpact" : "ParryApproach";
            sprite = parryDrawingFrame < active ? library.Find("Enemy_Windup")?.At(int.MaxValue) :
                attack?.At(Mathf.FloorToInt(frame * attack.TotalFrames / (float)ai.AttackRecoveryFrames));
        }
        else if (health.IsParryStaggered || (parried && health.IsStunned))
        { MotionName = "Parried"; sprite = library.Find(useDownParryPose ? "Enemy_Death" : "Enemy_Parried")?.At(0); }
        else if (ai.IsWindingUp)
        {
            MotionName = "Windup";
            var windup = library.Find("Enemy_Windup");
            int total = Mathf.Max(1, Mathf.RoundToInt(ai.WindupDuration * 60));
            int frame = Mathf.Clamp(total - ai.WindupRemainingFrames, 0, total - 1);
            WindupSourceFrame = MapWindupFrame(windup, frame, total, ai.ParryCueLeadFrames);
            sprite = windup?.At(WindupSourceFrame);
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
        artwork.flipX = counterFacingHeld ? counterFacingLeft : !health.IsAlive ? deathFacingLeft : ai.Facing.x < 0;
        bool down = MotionName == "Death" || (MotionName == "Parried" && useDownParryPose);
        artwork.transform.localPosition = restPosition + (down ? Vector3.down * (2f / 32f) : Vector3.zero);
    }

    /// <summary>Retime only gameplay drawing. Keep the authored large flash readable and the AI contact tick unchanged.</summary>
    public static int MapWindupFrame(VideoArtLibrary.Motion motion, int elapsed, int total, int cueLead)
    {
        if (motion == null || motion.TotalFrames <= 0) return 0;
        total = Mathf.Max(1, total); elapsed = Mathf.Clamp(elapsed, 0, total - 1);
        int sourceTotal = motion.TotalFrames;
        if (!motion.HasValidParryCue) return Mathf.Min(sourceTotal - 1, Mathf.FloorToInt(elapsed * sourceTotal / (float)total));
        int lead = Mathf.Clamp(cueLead, 1, total), targetStart = total - lead;
        int sourceStart = motion.parryCueFrame, sourceHold = motion.ParryCueCelFrames;
        int hold = Mathf.Min(sourceHold, Mathf.Max(1, lead - 1));
        if (elapsed < targetStart)
            return Mathf.Min(sourceStart - 1, Mathf.FloorToInt(elapsed * sourceStart / (float)targetStart));
        if (elapsed < targetStart + hold)
            return sourceStart + Mathf.Min(sourceHold - 1, elapsed - targetStart);
        int tail = total - targetStart - hold, sourceTail = sourceTotal - sourceStart - sourceHold;
        if (tail <= 0 || sourceTail <= 0) return sourceTotal - 1;
        return Mathf.Min(sourceTotal - 1, sourceStart + sourceHold +
            Mathf.FloorToInt((elapsed - targetStart - hold) * sourceTail / (float)tail));
    }

    private void OnDisable()
    {
        parryDrawingOwner = null;
        counterFacingHeld = false;
        CombatTimeController.Unregister(this);
        if (health != null)
        {
            health.HitReceived -= OnHit; health.Parried -= OnParried;
            health.Died -= OnDied; health.ResetPerformed -= OnReset;
            health.ReleaseDeathPresentation(this);
        }
        if (ai != null) ai.AttackStarted -= OnAttack;
        if (launch != null) launch.LaunchStarted -= OnCounterLaunch;
        if (artwork != null) { artwork.transform.localPosition = restPosition; artwork.color = Color.white; }
    }
}
