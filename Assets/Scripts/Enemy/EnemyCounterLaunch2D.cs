using UnityEngine;

/// <summary>One collision-aware, combat-clock owner for a counter's enemy launch, including a lethal hit.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Damageable2D))]
public sealed class EnemyCounterLaunch2D : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private LayerMask obstacles;
    private Damageable2D health;
    private BoxCollider2D shape;
    private CounterCinematicProfile profile;
    private Quaternion restRotation;
    private Vector2 origin;
    private int direction, elapsed;
    private bool active, settling;
    private float fallSpeed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    public bool IsActive => active;
    public int ElapsedFrames => elapsed;
    public float TravelDistance => Mathf.Abs(transform.position.x - origin.x);
    public string LastEndReason { get; private set; } = "NotStarted";
    public int CombatTickOrder => 330;
    public void Configure(Transform visual, LayerMask layers) { visualRoot = visual; obstacles = layers; }
    private void Awake()
    {
        health = GetComponent<Damageable2D>(); shape = GetComponent<BoxCollider2D>();
        restRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
    }
    private void OnEnable()
    {
        if (health == null) health = GetComponent<Damageable2D>();
        health.ResetPerformed += OnReset; CombatTimeController.Register(this);
    }
    public void BeginLaunch(CounterCinematicProfile settings, int sign)
    {
        if (!isActiveAndEnabled || settings == null || !settings.IsValid) return;
        Stop(false, "Restarted"); profile = settings; direction = sign < 0 ? -1 : 1;
        origin = transform.position; elapsed = 0; active = true; settling = false; fallSpeed = 0;
        health.AcquireMotionControl(this); health.AcquireStun(this, (profile.launchFrames + 4) / 60f);
        health.HoldDeathPresentation(this, 3f / 60f); LastEndReason = "Launching";
    }
    public void CombatTick()
    {
        if (!active) return;
        health.HoldDeathPresentation(this, 3f / 60f);
        health.AcquireStun(this, 3f / 60f);
        elapsed++;
        if (settling)
        {
            fallSpeed = Mathf.Min(18, fallSpeed + 30f / 60f);
            float distance = fallSpeed / 60f;
            bool blocked = MoveAxis(Vector2.down * distance);
            if (blocked || elapsed >= profile.launchFrames + 180) Stop(false, blocked ? "Grounded" : "FallTimeout");
            return;
        }
        float t = Mathf.Clamp01(elapsed / (float)profile.launchFrames);
        Vector2 desired = origin + Vector2.right * direction * profile.launchDistance * (1 - Mathf.Pow(1 - t, 3)) +
            Vector2.up * profile.launchHeight * Mathf.Sin(t * Mathf.PI);
        MoveAxis(Vector2.right * (desired.x - transform.position.x));
        MoveAxis(Vector2.up * (desired.y - transform.position.y));
        if (visualRoot != null) visualRoot.localRotation = restRotation * Quaternion.Euler(0, 0, -direction * 95 * Mathf.Sin(t * Mathf.PI * .8f));
        if (t >= 1)
        {
            // The enemy can have been launched off a platform. Do not leave a living target floating.
            settling = true; fallSpeed = 0;
            if (MoveAxis(Vector2.down * .02f)) Stop(false, "Complete");
        }
    }
    private bool MoveAxis(Vector2 delta)
    {
        float distance = delta.magnitude;
        if (distance < .000001f) return false;
        float permitted = distance;
        if (shape != null && obstacles.value != 0)
        {
            var scale = transform.lossyScale;
            Vector2 size = Vector2.Scale(shape.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            Vector2 center = (Vector2)transform.position + Vector2.Scale(shape.offset, scale);
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacles, useTriggers = false };
            int count = Physics2D.BoxCast(center, size, 0, delta.normalized, filter, hits, distance + .005f);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == null || hits[i].collider == shape || Vector2.Dot(delta.normalized, hits[i].normal) >= -.001f) continue;
                var effector = hits[i].collider.GetComponent<PlatformEffector2D>();
                if (effector != null && effector.enabled && effector.useOneWay && hits[i].collider.usedByEffector &&
                    (delta.y >= 0 || hits[i].normal.y <= .5f || center.y - size.y * .5f < hits[i].collider.bounds.max.y - .03f)) continue;
                permitted = Mathf.Min(permitted, Mathf.Max(0, hits[i].distance - .005f));
            }
        }
        transform.position += (Vector3)(delta.normalized * permitted);
        return permitted < distance - .0001f;
    }
    public void CancelLaunch() => Stop(true, "Canceled");
    private void Stop(bool ground, string reason)
    {
        if (active && ground) MoveAxis(Vector2.down * 30);
        active = settling = false; fallSpeed = 0;
        if (visualRoot != null) visualRoot.localRotation = restRotation;
        if (health != null)
        {
            health.ReleaseMotionControl(this); health.ReleaseStun(this); health.ReleaseDeathPresentation(this);
        }
        LastEndReason = reason;
    }
    private void OnReset(Damageable2D _) => Stop(false, "Reset");
    private void OnDisable()
    {
        Stop(false, "Disabled");
        if (health != null) health.ResetPerformed -= OnReset;
        CombatTimeController.Unregister(this);
    }
}
