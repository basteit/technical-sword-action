using UnityEngine;

/// <summary>Prototype posture/weapon cues; readable without hue or sound.</summary>
[RequireComponent(typeof(EnemyMeleeChaser2D))]
public sealed class EnemyCombatCue2D : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private Transform silhouette;
    [SerializeField] private Transform weapon;
    [SerializeField] private SpriteRenderer cue;
    private EnemyMeleeChaser2D enemy;
    private Damageable2D health;
    private Vector3 silhouetteScale;
    public int CombatTickOrder => 290;
    private void Awake()
    {
        enemy = GetComponent<EnemyMeleeChaser2D>(); health = GetComponent<Damageable2D>();
        if (silhouette != null) silhouetteScale = silhouette.localScale;
    }
    private void OnEnable() => CombatTimeController.Register(this);
    private void OnDisable() => CombatTimeController.Unregister(this);
    public void CombatTick()
    {
        bool winding = enemy.IsWindingUp;
        bool active = enemy.ActiveRemainingFrames > 0;
        bool stunned = health.IsStunned;
        bool lowSweep = enemy.AttackKind == "Unparryable";
        float direction = enemy.Facing.x < 0 ? -1 : 1;
        if (silhouette != null)
        {
            silhouette.localScale = Vector3.Scale(silhouetteScale, winding ? new Vector3(lowSweep ? 1.35f : 1.2f, lowSweep ? .65f : .8f, 1) : Vector3.one);
            silhouette.localRotation = Quaternion.Euler(0, 0, stunned ? -direction * 25 : winding ? direction * 12 : 0);
        }
        if (weapon != null)
        {
            weapon.localPosition = new Vector3(direction * (active ? .85f : .45f), lowSweep ? -.45f : winding ? .55f : .1f);
            weapon.localRotation = Quaternion.Euler(0, 0, direction * (lowSweep ? winding ? -35 : 0 : winding ? 65 : active ? -10 : 90));
        }
        if (cue != null)
        {
            cue.enabled = winding || active;
            cue.transform.localScale = Vector3.one * (enemy.WindupRemainingFrames <= 6 ? 1.4f : 1);
            cue.transform.localRotation = Quaternion.Euler(0, 0, enemy.AttackKind == "Unparryable" ? 45 : 0);
            cue.color = enemy.AttackKind == "Unparryable" ? Color.red : Color.yellow;
        }
    }
}
