using UnityEngine;

/// <summary>Moves/resizes the drawing, never the body or its collision/hit origins.</summary>
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(12000)]
public sealed class PrototypePlayerArtPresentation : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private SpriteRenderer source;
    [SerializeField] private SpriteRenderer artwork;
    [SerializeField] private BoxCollider2D body;
    [SerializeField] private PrototypePlayerArtSet artSet;
    private bool originalForceRenderingOff;
    private bool ownsVisibility;
    private object impactOffsetOwner;
    private float impactOffsetX;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private PlayerAttack2D attack;
    private long jumpStartTick = -1, apexStartTick = -1;
    public string MobilityPhase { get; private set; } = "None";
    public int MobilitySourceFrame { get; private set; }
    public int CombatTickOrder => 1200;

    public SpriteRenderer Artwork => artwork;
    public PrototypePlayerArtSet ArtSet => artSet;
    public float ImpactOffsetX => impactOffsetX;
    public void SetImpactOffset(object owner, float worldX)
    {
        if (owner == null || !float.IsFinite(worldX) || impactOffsetOwner != null && !ReferenceEquals(impactOffsetOwner, owner)) return;
        impactOffsetOwner = owner; impactOffsetX = worldX; RefreshVisual();
    }
    public void ReleaseImpactOffset(object owner)
    { if (ReferenceEquals(impactOffsetOwner, owner)) { impactOffsetOwner = null; impactOffsetX = 0; RefreshVisual(); } }

    public void Configure(SpriteRenderer sourceRenderer, SpriteRenderer artworkRenderer, BoxCollider2D collider, PrototypePlayerArtSet set)
    {
        ReleaseVisibility();
        source = sourceRenderer; artwork = artworkRenderer; body = collider; artSet = set;
        RefreshVisual();
    }

    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>(); motor = GetComponent<PlayerMotor2D>(); attack = GetComponent<PlayerAttack2D>();
        ResetMobility();
        if (Application.isPlaying)
        {
            CombatTimeController.Register(this);
            CombatTimeController.SessionReset += ResetMobility;
        }
        RefreshVisual();
    }
    private void LateUpdate() => RefreshVisual();

    public void CombatTick()
    {
        if (state != null && motor != null && state.LastDecisionTick == CombatTimeController.TickCount &&
            state.LastAcceptedRequest == TechnicalSwordAction.PlayerState.PlayerActionRequest.Jump && !motor.IsDroppingThrough)
        { jumpStartTick = CombatTimeController.TickCount; apexStartTick = -1; }
    }

    public void CombatTickTimers()
    {
        MobilityPhase = "None"; MobilitySourceFrame = 0;
        if (source == null || artSet == null || !artSet.HasSubmittedJump || state == null || motor == null) return;
        if (motor.IsGrounded) { jumpStartTick = apexStartTick = -1; }
        if (state.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Alive) return;
        if (state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.Dash)
        { MobilityPhase = "Dash"; return; } // Its normal clip owns the submitted three-cel dash.
        if (state.ActionState != TechnicalSwordAction.PlayerState.PlayerActionState.Neutral ||
            attack != null && attack.IsRecovering || motor.IsGrounded || DialogueController.GameplayInputBlocked) return;

        int index;
        float speed = motor.Velocity.y;
        long elapsed = jumpStartTick >= 0 ? CombatTimeController.TickCount - jumpStartTick : long.MaxValue;
        int takeoff = Mathf.Max(1, artSet.takeoffCelFrames);
        if (elapsed < takeoff * 3 && speed > artSet.apexVerticalSpeed)
        { index = (int)(elapsed / takeoff); MobilityPhase = "Takeoff"; }
        else if (speed > artSet.apexVerticalSpeed)
        { index = 3; MobilityPhase = "Rise"; }
        else if (jumpStartTick >= 0 && speed >= -artSet.apexVerticalSpeed && !motor.IsDroppingThrough)
        {
            if (apexStartTick < 0) apexStartTick = CombatTimeController.TickCount;
            index = 4 + (int)((CombatTimeController.TickCount - apexStartTick) / Mathf.Max(1, artSet.apexCelFrames) % 2);
            MobilityPhase = "Apex";
        }
        else { index = 6; MobilityPhase = "Fall"; }
        MobilitySourceFrame = index + 1;
        // Animator evaluation has already finished for this tick. Override only its sprite,
        // never the action, gravity, body, timing, facing or input ownership.
        source.sprite = artSet.jumpFrames[index];
        RefreshVisual();
    }

    private void ResetMobility()
    { jumpStartTick = apexStartTick = -1; MobilityPhase = "None"; MobilitySourceFrame = 0; }

    public void RefreshVisual()
    {
        if (source == null || artwork == null || source == artwork || body == null || artSet == null) return;
        if (!ownsVisibility)
        {
            originalForceRenderingOff = source.forceRenderingOff;
            ownsVisibility = true;
        }
        source.forceRenderingOff = true;
        artwork.enabled = source.enabled;
        artwork.sprite = artSet.DisplaySpriteFor(source.sprite);
        artwork.color = source.color;
        artwork.flipX = source.flipX; artwork.flipY = source.flipY;
        artwork.sortingLayerID = source.sortingLayerID;
        artwork.sortingOrder = Mathf.Max(source.sortingOrder, artSet.foregroundSortingOrder);
        artwork.sharedMaterial = source.sharedMaterial;
        if (source.sprite == null) return;

        // Some unmodified Aseprite samples are 100 PPU with a bottom pivot.
        // Normalize their displayed pixels without reimporting or editing the artist's files.
        float scale = source.sprite.pixelsPerUnit / Mathf.Max(1f, artSet.pixelsPerUnit);
        artwork.transform.localScale = new Vector3(scale, scale, 1f);
        float feetY = body.offset.y - body.size.y * .5f;
        artwork.transform.localPosition = new Vector3(0,
            feetY + (artSet.IsFeetPivot(source.sprite) ? 0 : artSet.commonFeetBelowPivot), 0) +
            artwork.transform.parent.InverseTransformVector(Vector3.right * impactOffsetX);
    }

    private void OnDisable()
    {
        CombatTimeController.Unregister(this);
        CombatTimeController.SessionReset -= ResetMobility;
        ResetMobility();
        impactOffsetOwner = null; impactOffsetX = 0;
        RefreshVisual();
        ReleaseVisibility();
        if (artwork != null) artwork.enabled = false;
    }

    private void ReleaseVisibility()
    {
        if (ownsVisibility && source != null) source.forceRenderingOff = originalForceRenderingOff;
        ownsVisibility = false;
    }
}
