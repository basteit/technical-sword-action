using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Visual snapshots of actual dash travel, aged by the combat clock.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(12010)]
public sealed class PrototypeDashTrail : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private CombatFeedback2D feedback;
    [SerializeField, Min(1)] private int intervalFrames = 2;
    [SerializeField, Min(1)] private int lifetimeFrames = 9;
    [SerializeField] private Color tint = new(.28f, .69f, .94f, .48f);
    private const int Capacity = 6;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[Capacity];
    private readonly int[] remaining = new int[Capacity];
    private GameObject pool;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private Rigidbody2D body;
    private PrototypePlayerArtPresentation art;
    private Vector2 beforeMove;
    private bool sampleTravel;
    private long nextEmissionTick;
    private int nextSlot;

    public int CombatTickOrder => 1210;

    private float Intensity => feedback != null && feedback.Settings != null
        ? Mathf.Clamp01(feedback.Settings.vfx) : 0f;
    private bool CanShow => state != null && state.isActiveAndEnabled && state.LifeState == PlayerLifeState.Alive &&
        (state.ActionState == PlayerActionState.Dash || state.ActionState == PlayerActionState.Neutral) &&
        motor != null && motor.isActiveAndEnabled && art != null && art.isActiveAndEnabled && body != null;

    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>();
        motor = GetComponent<PlayerMotor2D>();
        body = GetComponent<Rigidbody2D>();
        art = GetComponent<PrototypePlayerArtPresentation>();
        CombatTimeController.Register(this);
        CombatTimeController.SessionReset += Clear;
    }

    public void CombatTick()
    {
        sampleTravel = CanShow && motor.IsDashInvincible && state.ActionState == PlayerActionState.Dash && Intensity > 0;
        if (sampleTravel) beforeMove = body.position;
    }

    public void CombatTickTimers()
    {
        float intensity = Intensity;
        if (!CanShow || intensity <= 0) { Clear(); return; }
        for (int i = 0; i < Capacity; i++)
            if (remaining[i] > 0 && --remaining[i] == 0) ghosts[i].enabled = false;

        if (sampleTravel && state.ActionState == PlayerActionState.Dash && CombatTimeController.TickCount >= nextEmissionTick)
        {
            Vector2 travel = body.position - beforeMove;
            if (Mathf.Abs(travel.x) >= .02f)
            {
                // Animator and physics have resolved this tick. Copy the visible cel but leave
                // the snapshot behind the player at the start of the completed movement.
                art.RefreshVisual();
                var drawing = art.Artwork;
                if (drawing != null && drawing.sprite != null && drawing.enabled && !drawing.forceRenderingOff)
                {
                    Emit(drawing, travel);
                    nextEmissionTick = CombatTimeController.TickCount + Mathf.Max(1, intervalFrames);
                }
            }
        }
        UpdateOpacity(intensity);
    }

    private void LateUpdate()
    {
        // Settings or an external interrupt may change even while the clock is stopped.
        float intensity = Intensity;
        if (!CanShow || intensity <= 0) Clear();
        else UpdateOpacity(intensity);
    }

    private void Emit(SpriteRenderer drawing, Vector2 travel)
    {
        if (pool == null)
        {
            pool = new GameObject("PrototypeDashAfterimages");
            SceneManager.MoveGameObjectToScene(pool, gameObject.scene);
            for (int i = 0; i < Capacity; i++)
            {
                var ghost = new GameObject("DashAfterimage");
                ghost.layer = drawing.gameObject.layer;
                ghost.transform.SetParent(pool.transform, false);
                ghosts[i] = ghost.AddComponent<SpriteRenderer>();
                ghosts[i].enabled = false;
            }
        }
        var renderer = ghosts[nextSlot];
        var pose = drawing.transform;
        renderer.transform.SetPositionAndRotation(pose.position - (Vector3)travel, pose.rotation);
        renderer.transform.localScale = pose.lossyScale;
        renderer.sprite = drawing.sprite;
        renderer.flipX = drawing.flipX;
        renderer.flipY = drawing.flipY;
        renderer.sharedMaterial = drawing.sharedMaterial;
        renderer.sortingLayerID = drawing.sortingLayerID;
        renderer.sortingOrder = drawing.sortingOrder - 1;
        remaining[nextSlot] = Mathf.Max(1, lifetimeFrames);
        renderer.enabled = true;
        nextSlot = (nextSlot + 1) % Capacity;
    }

    private void UpdateOpacity(float intensity)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (remaining[i] == 0) continue;
            var color = tint;
            color.a *= Mathf.Clamp01((float)remaining[i] / Mathf.Max(1, lifetimeFrames)) * intensity;
            ghosts[i].color = color;
        }
    }

    private void Clear()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (remaining[i] > 0 && ghosts[i] != null) ghosts[i].enabled = false;
            remaining[i] = 0;
        }
        nextSlot = 0;
        nextEmissionTick = 0;
        sampleTravel = false;
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.SessionReset -= Clear;
        Clear();
    }

    private void OnDestroy()
    {
        if (pool != null) Destroy(pool);
    }
}
