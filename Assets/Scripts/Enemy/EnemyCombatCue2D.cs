using UnityEngine;

/// <summary>Prototype posture/weapon cues; readable without hue or sound.</summary>
[RequireComponent(typeof(EnemyMeleeChaser2D))]
public sealed class EnemyCombatCue2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private Transform silhouette;
    [SerializeField] private Transform weapon;
    [SerializeField] private SpriteRenderer cue;
    private EnemyMeleeChaser2D enemy;
    private Damageable2D health;
    private Vector3 silhouetteScale;
    private Quaternion silhouetteRotation, weaponRotation;
    private Vector3 weaponPosition;
    public int CombatTickOrder => 290;
    private void Awake()
    {
        enemy = GetComponent<EnemyMeleeChaser2D>(); health = GetComponent<Damageable2D>();
        if (silhouette != null) { silhouetteScale = silhouette.localScale; silhouetteRotation = silhouette.localRotation; }
        if (weapon != null) { weaponPosition = weapon.localPosition; weaponRotation = weapon.localRotation; }
    }
    private void OnEnable()
    {
        health.Parried += OnParried; health.ResetPerformed += OnReset; health.Died += OnDied;
        CombatTimeController.Register(this);
    }
    private void OnDisable()
    {
        health.Parried -= OnParried; health.ResetPerformed -= OnReset; health.Died -= OnDied;
        CombatTimeController.Unregister(this); RestorePose();
    }
    private void OnParried(Damageable2D source, ParryResult result) => ApplyPose();
    private void OnReset(Damageable2D source) => RestorePose();
    private void OnDied(Damageable2D source, Vector2 direction) => RestorePose();
    public void CombatTick() => ApplyPose();
    public void CombatTickTimers() => ApplyPose();
    private void RestorePose()
    {
        if (silhouette != null) { silhouette.localScale = silhouetteScale; silhouette.localRotation = silhouetteRotation; }
        if (weapon != null) { weapon.localPosition = weaponPosition; weapon.localRotation = weaponRotation; }
        if (cue != null) cue.enabled = false;
    }
    private void ApplyPose()
    {
        if (!health.IsAlive) { RestorePose(); return; }
        bool winding = enemy.IsWindingUp;
        bool active = enemy.ActiveRemainingFrames > 0;
        bool stunned = health.IsStunned;
        bool lowSweep = enemy.AttackKind == "Unparryable";
        float direction = enemy.Facing.x < 0 ? -1 : 1;
        bool staggering = health.IsParryStaggered;
        // The body and lowered sword sway together for exactly the counter window.
        // Only drawing children move; the collider/root and launch owner stay untouched.
        float sway = staggering ? Mathf.Min(1, health.ParryStaggerRemainingFrames / 12f) *
            (20 + 6 * Mathf.Sin(health.ParryStaggerElapsedFrames * .28f)) : 0;
        if (silhouette != null)
        {
            silhouette.localScale = Vector3.Scale(silhouetteScale, staggering ? new Vector3(1.08f, .9f, 1) :
                winding ? new Vector3(lowSweep ? 1.35f : 1.2f, lowSweep ? .65f : .8f, 1) : Vector3.one);
            silhouette.localRotation = silhouetteRotation * Quaternion.Euler(0, 0, staggering ? -direction * sway : stunned ? -direction * 25 : winding ? direction * 12 : 0);
        }
        if (weapon != null)
        {
            weapon.localPosition = new Vector3(direction * (active && !staggering ? .85f : .45f), staggering ? -.25f : lowSweep ? -.45f : winding ? .55f : .1f);
            weapon.localRotation = Quaternion.Euler(0, 0, direction * (staggering ? 60 - sway : lowSweep ? winding ? -35 : 0 : winding ? 65 : active ? -10 : 90));
        }
        if (cue != null)
        {
            // Yellow means "parry now", not the entire (possibly 96F) preparation.
            // Red retains the full warning for an attack which cannot be parried.
            cue.enabled = !stunned && (lowSweep ? winding || active : enemy.IsParryCueActive);
            cue.transform.localScale = Vector3.one * (enemy.WindupRemainingFrames <= 6 ? 1.4f : 1);
            cue.transform.localRotation = Quaternion.Euler(0, 0, enemy.AttackKind == "Unparryable" ? 45 : 0);
            cue.color = enemy.AttackKind == "Unparryable" ? Color.red : Color.yellow;
        }
    }
}
