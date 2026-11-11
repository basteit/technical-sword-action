using UnityEngine;

/// <summary>Plain blocks and one-way/moving platforms, all advanced by the combat clock.</summary>
[DisallowMultipleComponent]
public sealed class StudioStageGimmick : MonoBehaviour, ICombatTickListener
{
    [SerializeField] private StudioGimmickSetup setup = new StudioGimmickSetup();
    [SerializeField] private LayerMask obstacleLayers;
    private BoxCollider2D platform;
    private Rigidbody2D rider;
    private Collider2D riderCollider;
    private int elapsed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    public StudioGimmickSetup Setup => setup;
    public int ElapsedTicks => elapsed;
    public int CombatTickOrder => -50;
    private void OnEnable() => CombatTimeController.Register(this);
    private void OnDisable() => CombatTimeController.Unregister(this);

    public void Configure(StudioGimmickSetup value, PlayerStateMachine player)
    {
        setup = value.Copy();
        setup.size = new Vector2(Mathf.Max(.25f, setup.size.x), Mathf.Max(.125f, setup.size.y));
        platform = GetComponent<BoxCollider2D>(); platform.size = setup.size;
        var visual = GetComponent<SpriteRenderer>(); visual.drawMode = SpriteDrawMode.Sliced; visual.size = setup.size;
        visual.color = setup.kind == StudioGimmickKind.Block ? new Color(.57f,.63f,.67f) : new Color(.42f,.53f,.61f);
        var effector = GetComponent<PlatformEffector2D>();
        effector.enabled = platform.usedByEffector = setup.kind != StudioGimmickKind.Block;
        if (player != null) { rider = player.GetComponent<Rigidbody2D>(); riderCollider = player.GetComponent<Collider2D>(); }
        ResetGimmick();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            foreach(var component in GetComponents<Component>())UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
#endif
    }

    public void CaptureHome() { setup.position = transform.position; elapsed = 0; }
    public void ResetGimmick() { elapsed = 0; transform.position = setup.position; }
    public void CombatTick()
    {
        if (setup.kind != StudioGimmickKind.MovingPlatform || platform == null) return;
        Bounds old = platform.bounds;
        elapsed++;
        float phase = elapsed / (Mathf.Max(.2f, setup.periodSeconds) * 60f);
        Vector2 desired = setup.position + setup.travel * (.5f - .5f * Mathf.Cos(phase * Mathf.PI * 2));
        Vector2 delta = desired - (Vector2)transform.position;
        if (rider != null && riderCollider != null && rider.linearVelocity.y <= .1f &&
            Mathf.Abs(riderCollider.bounds.min.y - old.max.y) <= .08f &&
            riderCollider.bounds.max.x > old.min.x + .02f && riderCollider.bounds.min.x < old.max.x - .02f)
        {
            // Carry a standing rider, clipping against other solid stage geometry.
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
            int count = riderCollider.Cast(delta.normalized, filter, hits, delta.magnitude);
            float distance = delta.magnitude;
            for (int i=0; i<count; i++)
                if (hits[i].collider != platform && Vector2.Dot(delta.normalized, hits[i].normal) < -.001f)
                    distance = Mathf.Min(distance, Mathf.Max(0, hits[i].distance-.005f));
            rider.position += delta.normalized * distance;
        }
        transform.position = desired;
    }
}
