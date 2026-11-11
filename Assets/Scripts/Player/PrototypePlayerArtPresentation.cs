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
    private long jumpStartTick = -1, riseStartTick = -1, fallStartTick = -1;
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
        {
            ResetJumpTiming();
            jumpStartTick = CombatTimeController.TickCount;
        }
    }

    public void CombatTickTimers()
    {
        MobilityPhase = "None"; MobilitySourceFrame = 0;
        if (source == null || artSet == null || !artSet.HasSubmittedJump || state == null || motor == null) return;
        long tick = CombatTimeController.TickCount;
        float speed = motor.Velocity.y;
        float apexEdge = Mathf.Max(.01f, artSet.apexVerticalSpeed);
        float apexCenter = Mathf.Clamp(artSet.apexCenterVerticalSpeed, .01f, apexEdge);
        if (motor.IsGrounded) ResetJumpTiming();
        else if (speed < -apexCenter && fallStartTick < 0) fallStartTick = tick;
        if (state.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Alive) return;
        if (state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.Dash)
        { MobilityPhase = "Dash"; return; } // Its normal clip owns the submitted three-cel dash.
        if (state.ActionState != TechnicalSwordAction.PlayerState.PlayerActionState.Neutral ||
            attack != null && attack.IsRecovering || motor.IsGrounded || DialogueController.GameplayInputBlocked) return;

        int index;
        long elapsed = jumpStartTick >= 0 ? tick - jumpStartTick : long.MaxValue;
        int takeoff = Mathf.Max(1, artSet.takeoffCelFrames);
        if (elapsed < takeoff * 3 && speed > apexEdge)
        { index = (int)(elapsed / takeoff); MobilityPhase = "Takeoff"; }
        else if (speed > apexEdge)
        { index = LoopIndex(ref riseStartTick, tick, artSet.riseCelFrames, 3); MobilityPhase = "Rise"; }
        else if (fallStartTick < 0 && jumpStartTick >= 0 && speed >= -apexCenter && !motor.IsDroppingThrough)
        {
            // Cel 6 eases out of ascent; cel 7 straddles the actual apex.
            index = speed > apexCenter ? 5 : 6;
            MobilityPhase = "Apex";
        }
        else { index = LoopIndex(ref fallStartTick, tick, artSet.fallCelFrames, 7); MobilityPhase = "Fall"; }
        MobilitySourceFrame = index + 1;
        // Animator evaluation has already finished for this tick. Override only its sprite,
        // never the action, gravity, body, timing, facing or input ownership.
        source.sprite = artSet.jumpFrames[index];
        RefreshVisual();
    }

    private static int LoopIndex(ref long phaseStart, long tick, int celFrames, int firstIndex)
    {
        if (phaseStart < 0) phaseStart = tick;
        return firstIndex + (int)((tick - phaseStart) / Mathf.Max(1, celFrames) % 2);
    }

    private void ResetJumpTiming() => jumpStartTick = riseStartTick = fallStartTick = -1;
    private void ResetMobility()
    { ResetJumpTiming(); MobilityPhase = "None"; MobilitySourceFrame = 0; }

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
        // The hit can be resolved after the Animator's state was chosen for this tick.
        // Draw the temporary standing contact pose even if that tick begins hitstop.
        Sprite pose = Application.isPlaying && state != null &&
            state.ActionState == TechnicalSwordAction.PlayerState.PlayerActionState.Hit
            ? artSet.Find("Hit")?.contactSprite ?? source.sprite : source.sprite;
        artwork.sprite = artSet.DisplaySpriteFor(pose);
        artwork.color = source.color;
        artwork.flipX = source.flipX; artwork.flipY = source.flipY;
        artwork.sortingLayerID = source.sortingLayerID;
        artwork.sortingOrder = Mathf.Max(source.sortingOrder, artSet.foregroundSortingOrder);
        artwork.sharedMaterial = source.sharedMaterial;
        if (pose == null) return;

        // Some unmodified Aseprite samples are 100 PPU with a bottom pivot.
        // Normalize their displayed pixels without reimporting or editing the artist's files.
        float scale = pose.pixelsPerUnit / Mathf.Max(1f, artSet.pixelsPerUnit);
        artwork.transform.localScale = new Vector3(scale, scale, 1f);
        float feetY = body.offset.y - body.size.y * .5f;
        artwork.transform.localPosition = new Vector3(0,
            feetY + (artSet.IsFeetPivot(pose) ? 0 : artSet.commonFeetBelowPivot), 0) +
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
