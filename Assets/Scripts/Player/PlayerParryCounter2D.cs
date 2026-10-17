using System;
using System.Collections.Generic;
using TechnicalSwordAction.PlayerState;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerParryCounter2D : MonoBehaviour, IPlayerActionStateHandler, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private ParryCounterDefinition definition;
    [SerializeField, Min(1)] private int hitFrame = 6;
    [SerializeField, Min(1)] private int endFrame = 24;
    [SerializeField, Min(1)] private int damage = 3;
    [SerializeField] private float radius = 1.1f;
    [Header("Video aerial counter profile")]
    [SerializeField] private bool airborneKick;
    [SerializeField] private Vector2 airborneContactOffset = new Vector2(22f/32f,-22f/32f);
    [SerializeField] private bool invincibleDuringKick = true;
    [SerializeField, Min(1)] private int chargeFrame = 18;
    [SerializeField, Min(1)] private int chargeReleaseFrame = 24;
    [SerializeField, Min(1)] private int landingFrame = 36;
    [SerializeField, Min(0f)] private float landingRollDistance = .12f;
    [SerializeField, Min(0f)] private float jumpHeight = .55f;
    [SerializeField, Min(0f)] private float maxApproach = .6f;
    [SerializeField] private bool blinkApproach;
    [SerializeField, Min(1)] private int blinkTravelFrames = 2;
    [SerializeField] private bool overheadHeelDrop;
    [SerializeField, Min(0f)] private float overheadHeight = 1.35f;
    [SerializeField, Min(0f)] private float heelImpactHeight = 1.15f;
    [SerializeField] private Vector2 heelContactOffset = new Vector2(10f/32f,-22f/32f);
    [SerializeField] private float heelTargetHeadOffsetX = 7f/32f;
    [SerializeField] private LayerMask obstacleLayers;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private int elapsed;
    private bool active, hitApplied, charged,confirmedHit,chargeReleased,landed;
    private float savedGravity;
    private Vector2 origin;
    private Vector2 hoverPosition;
    private float approach;
    private int facing;
    private readonly List<Damageable2D> stocks = new();
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];
    private bool queuedCounter;
    private int selectedDamage;
    public string LastStockReason { get; private set; } = "None";
    public string LastSelectionReason { get; private set; } = "NoStock";
    public int ConsumedCount { get; private set; }
    public bool HasQueuedCounter => queuedCounter;
    public float SelectedMultiplier { get; private set; } = 1f;
    public int SelectedDamage => selectedDamage;
    public bool CanDefenseCancel => active && !airborneKick && definition != null && elapsed >= definition.cancelStartFrame;
    public ParryCounterDefinition Definition => definition;
    public IReadOnlyList<Damageable2D> Stocks => stocks;
    public Damageable2D SelectedTarget { get; private set; }
    public int StockCount { get { PruneStocks(); return stocks.Count; } }
    public bool IsActive => active;
    public bool IsCounterInvincible => active && airborneKick && invincibleDuringKick;
    public bool IsAirborneKick => airborneKick;
    public bool UsesBlinkApproach => airborneKick && blinkApproach;
    public bool IsOverheadHeelDrop => airborneKick && overheadHeelDrop;
    public Vector2 HoverPosition => hoverPosition;
    public Vector2 ContactPoint => (Vector2)transform.position + new Vector2(facing * (IsOverheadHeelDrop?heelContactOffset.x:airborneContactOffset.x),IsOverheadHeelDrop?heelContactOffset.y:airborneContactOffset.y);
    public int ElapsedFrames => elapsed;
    public int HitFrame => hitFrame;
    public int ChargeFrame => chargeFrame;
    public int ChargeReleaseFrame=>chargeReleaseFrame;
    public int LandingFrame=>landingFrame;
    public bool HasReleasedCharge=>chargeReleased;
    public bool HasLanded=>landed;
    public int HitCount { get; private set; }
    public int StartCount { get; private set; }
    public string LastEndReason { get; private set; }
    public PlayerActionState ActionState => PlayerActionState.ParryCounter;
    public int CombatTickOrder => 210;
    public float LockRemaining => active ? Mathf.Max(0, Mathf.Max(endFrame, hitFrame) - elapsed) * CombatTimeController.StepSeconds : 0f;
    public bool CanStartAction => isActiveAndEnabled && !active && state != null &&
        state.LifeState == PlayerLifeState.Alive &&
        (state.ActionState == PlayerActionState.ParrySuccess || state.ActionState == PlayerActionState.Neutral) && FindEligible() != null;
    public event Action Started;
    public event Action ChargeStarted;
    public event Action ChargeReleased;
    public event Action Landed;
    public event Action<Damageable2D, Vector2> HitConfirmed;
    public event Action HitMissed;
    public event Action<bool> Finished;

    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>(); motor = GetComponent<PlayerMotor2D>();
        body = GetComponent<Rigidbody2D>(); bodyCollider = GetComponent<Collider2D>();
        if (definition != null && !airborneKick)
        {
            hitFrame = definition.hitFrame; endFrame = definition.endFrame; damage = definition.baseDamage;
            maxApproach = definition.approachDistance; obstacleLayers = definition.obstacleLayers;
        }
        state?.RegisterActionHandler(this); CombatTimeController.Register(this);
    }
    public void RegisterParriedEnemy(Damageable2D enemy)
    {
        PruneStocks();
        if (enemy != null && enemy.ParticipatingInCombat && !stocks.Contains(enemy))
        {
            stocks.Add(enemy);
            enemy.LeftCombat += RemoveStock;
            enemy.Died += OnStockTargetDied;
            enemy.ResetPerformed += RemoveStock;
            LastStockReason = "Registered:" + enemy.IndividualId;
        }
    }
    public void ClearStocks()
    {
        for (int i = stocks.Count - 1; i >= 0; i--) RemoveStock(stocks[i]);
        queuedCounter = false;
        LastStockReason = "AllCleared";
    }
    private void OnStockTargetDied(Damageable2D enemy, Vector2 direction) => RemoveStock(enemy);
    private void RemoveStock(Damageable2D enemy)
    {
        stocks.Remove(enemy);
        if (enemy == null) return;
        enemy.LeftCombat -= RemoveStock; enemy.Died -= OnStockTargetDied; enemy.ResetPerformed -= RemoveStock;
        LastStockReason = "Removed:" + enemy.IndividualId;
    }
    private void PruneStocks()
    {
        for (int i = stocks.Count - 1; i >= 0; i--)
            if (stocks[i] == null || !stocks[i].ParticipatingInCombat) RemoveStock(stocks[i]);
    }
    public string EligibilityReason(Damageable2D enemy)
    {
        if (enemy == null || !enemy.ParticipatingInCombat) return "TargetLost";
        Vector2 delta = enemy.transform.position - transform.position;
        if (delta.magnitude > 1.9f) return "OutOfRange";
        if (Mathf.Abs(delta.y) > 1.25f) return "Height";
        int direction = motor != null ? motor.FacingSign : 1;
        if (delta.sqrMagnitude > .0001f && Vector2.Dot(delta.normalized, Vector2.right * direction) < .5f) return "Behind";
        if (Physics2D.Linecast(transform.position, enemy.transform.position, obstacleLayers).collider != null) return "Obstructed";
        return "Eligible";
    }
    public bool ReserveNextCounter()
    {
        if (!active || airborneKick || definition == null || endFrame - elapsed > definition.chainBufferFrames)
            return false;
        queuedCounter = true;
        return true;
    }
    private Damageable2D FindEligible()
    {
        PruneStocks();
        LastSelectionReason = stocks.Count == 0 ? "NoStock" : "NoEligibleTarget";
        foreach (var enemy in stocks)
        {
            if (EligibilityReason(enemy) != "Eligible") continue;
            LastSelectionReason = "Selected:" + enemy.IndividualId;
            return enemy;
        }
        return null;
    }
    public bool TryStartAction()
    {
        if (!CanStartAction) return false;
        SelectedTarget = FindEligible(); RemoveStock(SelectedTarget);
        ConsumedCount++; LastStockReason = "Consumed:" + SelectedTarget.IndividualId;
        SelectedMultiplier = definition != null && !airborneKick ? SelectedTarget.CounterMultiplier : 1f;
        selectedDamage = Mathf.Max(1, Mathf.FloorToInt(damage * SelectedMultiplier + .5f));
        queuedCounter = false;
        active = true; elapsed = 0; hitApplied = charged = confirmedHit = chargeReleased = landed = false; StartCount++;
        origin = body != null ? body.position : (Vector2)transform.position;
        facing = motor != null ? motor.FacingSign : 1;
        hoverPosition = (Vector2)SelectedTarget.transform.position + new Vector2(facing*(heelTargetHeadOffsetX-heelContactOffset.x),overheadHeight);
        approach = Mathf.Clamp((SelectedTarget.transform.position.x - origin.x) * facing - .85f, 0, maxApproach);
        if (body != null) { savedGravity = body.gravityScale; if (airborneKick) body.gravityScale = 0; }
        SelectedTarget.AcquireStun(this,Mathf.Max(hitFrame, endFrame) * CombatTimeController.StepSeconds + .1f);
        Started?.Invoke(); return true;
    }
    public void CombatTick()
    {
        PruneStocks();
        if (!active) return;
        if (!hitApplied && (SelectedTarget == null || !SelectedTarget.ParticipatingInCombat)) { Finish("TargetLost"); return; }
        if (body == null) return;
        if (!airborneKick)
        {
            if (definition != null)
            {
                float fraction = Mathf.Clamp01((elapsed + 1f) / Mathf.Max(1, definition.approachFrames));
                Vector2 groundDesired = new Vector2(origin.x + facing * approach * fraction, body.position.y);
                Vector2 groundDelta = groundDesired - body.position;
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
                int count = bodyCollider != null && groundDelta.sqrMagnitude > .000001f ? bodyCollider.Cast(groundDelta.normalized, filter, movementHits, groundDelta.magnitude) : 0;
                float distance = groundDelta.magnitude;
                for (int i = 0; i < count; i++)
                    if (Vector2.Dot(groundDelta.normalized, movementHits[i].normal) < -.001f)
                        distance = Mathf.Min(distance, Mathf.Max(0, movementHits[i].distance - .005f));
                body.linearVelocity = new Vector2(groundDelta.normalized.x * distance / CombatTimeController.StepSeconds, body.linearVelocity.y);
            }
            return;
        }
        float frame=elapsed+1f;
        float travelFrames=blinkApproach?Mathf.Max(1,blinkTravelFrames):hitFrame;
        float rise=Mathf.Clamp01(frame/(blinkApproach?travelFrames:Mathf.Max(1,chargeFrame-2)));
        float fall=Mathf.Clamp01((frame-hitFrame)/Mathf.Max(1,landingFrame-hitFrame));
        float height=frame<=hitFrame?jumpHeight*(1-(1-rise)*(1-rise)):jumpHeight*(1-fall*fall);
        float x = origin.x + facing * approach * Mathf.Clamp01(frame / travelFrames);
        x+=facing*landingRollDistance*Mathf.Clamp01((frame-landingFrame)/4f);
        Vector2 desired = new Vector2(x, origin.y + height);
        if(overheadHeelDrop)
        {
            float arrive=Mathf.Clamp01(frame/Mathf.Max(1,blinkTravelFrames));
            desired=Vector2.Lerp(origin,hoverPosition,arrive);
            if(frame>chargeReleaseFrame)
            {
                float strike=Mathf.Clamp01((frame-chargeReleaseFrame)/Mathf.Max(1,hitFrame-chargeReleaseFrame));
                desired.y=hoverPosition.y-(overheadHeight-heelImpactHeight)*strike*strike;
            }
            if(frame>hitFrame)
            {
                float drop=Mathf.Clamp01((frame-hitFrame)/Mathf.Max(1,landingFrame-hitFrame));
                float impactY=hoverPosition.y-overheadHeight+heelImpactHeight;
                desired.y=Mathf.Lerp(impactY,origin.y,1-(1-drop)*(1-drop));
            }
        }
        Vector2 delta = desired - body.position;
        if (bodyCollider != null && delta.sqrMagnitude > .000001f && obstacleLayers.value != 0)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
            int count = bodyCollider.Cast(delta.normalized, filter, movementHits, delta.magnitude);
            float distance = delta.magnitude;
            for (int i = 0; i < count; i++)
                if(Vector2.Dot(delta.normalized,movementHits[i].normal)<-.001f)
                    distance = Mathf.Min(distance, Mathf.Max(0f, movementHits[i].distance - .005f));
            if(overheadHeelDrop&&frame<=blinkTravelFrames&&distance<delta.magnitude-.001f)
            { Finish("ApproachBlocked");return; }
            delta = delta.normalized * distance;
        }
        body.linearVelocity = delta / CombatTimeController.StepSeconds;
    }
    public void CombatTickTimers()
    {
        if (!active) return;
        elapsed++;
        if (airborneKick && !charged && elapsed >= chargeFrame)
        { charged = true; ChargeStarted?.Invoke(); }
        if(airborneKick&&charged&&!chargeReleased&&elapsed>=chargeReleaseFrame)
        { chargeReleased=true;CombatTimeController.ReleaseSlow(this);ChargeReleased?.Invoke(); }
        if (!hitApplied && elapsed >= Mathf.Max(1, hitFrame))
        {
            hitApplied = true;
            Vector2 offset=airborneKick?(overheadHeelDrop?heelContactOffset:airborneContactOffset):new Vector2(.85f,-.25f);
            Vector2 foot = (Vector2)transform.position + new Vector2(facing*offset.x,offset.y);
            bool contactsTarget = false;
            Collider2D[] contacts = definition != null && !airborneKick
                ? Physics2D.OverlapBoxAll((Vector2)transform.position + Vector2.right * facing, definition.hitSize, 0)
                : Physics2D.OverlapCircleAll(foot, radius);
            foreach (Collider2D c in contacts)
                if (c.GetComponentInParent<Damageable2D>() == SelectedTarget) { contactsTarget = true; break; }
            bool clearPath=SelectedTarget!=null&&Physics2D.Linecast(transform.position,SelectedTarget.transform.position,obstacleLayers).collider==null;
            if (SelectedTarget != null && SelectedTarget.IsAlive && contactsTarget && clearPath && SelectedTarget.TryTakeCombatHit(selectedDamage, Vector2.right * facing, EnemyHitStrength.Strong))
            {
                confirmedHit=true;HitCount++;
                if (definition != null && !airborneKick) GetComponent<PlayerSpecialGauge>()?.AddOnAttackHit(definition.gaugeGain);
                HitConfirmed?.Invoke(SelectedTarget, foot);
            }
            else { CombatTimeController.ReleaseSlow(this);HitMissed?.Invoke(); }
        }
        if(airborneKick&&!landed&&elapsed>=landingFrame) { landed=true;Landed?.Invoke(); }
        if (elapsed >= Mathf.Max(endFrame, hitFrame)) Finish("Complete");
    }
    private void Finish(string reason)
    {
        bool chain = reason == "Complete" && queuedCounter;
        LastEndReason = reason;
        CancelAction(); state?.CompleteAction(ActionState, "ParryCounter" + reason);
        if (chain && FindEligible() != null) state?.RequestAction(PlayerActionRequest.Attack);
    }
    public void CancelAction()
    {
        bool wasActive = active; bool didHit = confirmedHit;
        active = false; elapsed = 0; queuedCounter = false; hitApplied = charged = confirmedHit = chargeReleased = landed = false;
        if (wasActive && body != null) { body.gravityScale = savedGravity; body.linearVelocity = Vector2.zero; }
        if(SelectedTarget!=null)
        {
            SelectedTarget.ReleaseStun(this);
            if (wasActive && !didHit && SelectedTarget.IsAlive && definition != null && !airborneKick) SelectedTarget.ExtendStun(12f / 60f);
        }
        SelectedTarget = null; CombatTimeController.ReleaseOwner(this);
        if (wasActive) Finished?.Invoke(didHit);
    }
    private void OnDisable()
    {
        CancelAction(); ClearStocks(); CombatTimeController.Unregister(this);
        state?.CompleteAction(ActionState, "ParryCounterDisabled"); state?.UnregisterActionHandler(this);
    }
}
