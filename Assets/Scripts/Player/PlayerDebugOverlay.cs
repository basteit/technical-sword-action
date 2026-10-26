using UnityEngine;
using System.Text;

public class PlayerDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private PlayerParry2D parry;
    [SerializeField] private PlayerSpecialGauge specialGauge;
    [SerializeField] private PlayerSpecialSkill2D specialSkill;
    [SerializeField] private bool visible = true;
    [SerializeField] private int fontSize = 17;
    private PlayerParryCounter2D counter;
    private PlayerInteractor2D interactor;
    private PlayerCombatTelemetry telemetry;
    private readonly StringBuilder text = new(2048);
    private string snapshot;
    private float nextRefresh;
    private int group;
    private Vector2 scroll;
    private GUIStyle labelStyle;
    private static readonly string[] Groups = { "State / Input", "Attack / Counter", "Enemy / Damage" };

    private void Start()
    {
        if (motor == null) motor = GetComponent<PlayerMotor2D>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (damageReceiver == null) damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        if (parry == null) parry = GetComponent<PlayerParry2D>();
        if (specialGauge == null) specialGauge = GetComponent<PlayerSpecialGauge>();
        if (specialSkill == null) specialSkill = GetComponent<PlayerSpecialSkill2D>();
        counter = GetComponent<PlayerParryCounter2D>(); interactor = GetComponent<PlayerInteractor2D>();
        telemetry = GetComponent<PlayerCombatTelemetry>();
        if (telemetry == null) telemetry = gameObject.AddComponent<PlayerCombatTelemetry>();
    }

    public string BuildSnapshot(int selectedGroup)
    {
        text.Clear();
        text.AppendLine($"Tick {CombatTimeController.TickCount} / FPS {(Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0):0}");
        text.AppendLine($"Stop: {(CombatTimeController.IsPaused ? "Pause" : CombatTimeController.IsHitStopped ? "Hitstop" : "None")} / Hitstop {CombatTimeController.HitstopRemainingTicks}F / Slow {CombatTimeController.SlowScale:0.00}");
        if (selectedGroup == 0)
        {
            if (stateMachine == null) text.AppendLine("State: N/A (PlayerStateMachine missing)");
            else
            {
                text.AppendLine($"{stateMachine.LifeState} / {stateMachine.ActionState} / {stateMachine.LocomotionState} / Lock {stateMachine.RemainingLock * 60:0}F");
                text.AppendLine($"Transition: {stateMachine.PreviousActionState} -> {stateMachine.ActionState} ({stateMachine.LastTransitionReason})");
                text.AppendLine($"Decision @{stateMachine.LastDecisionTick}: {stateMachine.LastRequestedActions} -> {stateMachine.LastAcceptedRequest} / Priority {stateMachine.LastAcceptedPriority}");
                text.AppendLine($"Rejected: {stateMachine.LastRejectionReason}\n{stateMachine.LastDecision}");
                text.AppendLine($"Shared B: {stateMachine.LastSharedInputResolution} ({stateMachine.LastSharedInputReason}), target ID {stateMachine.LastSharedInputTargetInstanceId}");
                text.AppendLine($"Pending: {stateMachine.PendingRequests} / Attack {stateMachine.GetInputBufferFramesRemaining(TechnicalSwordAction.PlayerState.PlayerActionRequest.Attack)}F / Dash {stateMachine.GetInputBufferFramesRemaining(TechnicalSwordAction.PlayerState.PlayerActionRequest.Dash)}F");
                text.AppendLine($"Collision ignores: {stateMachine.ActiveCollisionIgnoreCount}");
            }
            text.AppendLine(interactor != null ? $"Interact candidate: {interactor.CurrentInteractable?.InteractionPrompt ?? "None"}" : "Interact: N/A (PlayerInteractor2D missing)");
            text.AppendLine(motor != null ? $"Velocity: {motor.Velocity} / Owner {motor.VelocityOwner}\nGrounded {motor.IsGrounded} / AirDashUsed {motor.AirDashUsed} / FloorDrop {motor.IsDroppingThrough} / Coyote {motor.CoyoteRemainingFrames}F" : "Motor: N/A (PlayerMotor2D missing)");
            text.AppendLine(damageReceiver != null ? $"HP {damageReceiver.CurrentHp}/{damageReceiver.MaxHp} / Invincible {damageReceiver.InvincibleRemaining * 60:0}F" : "HP: N/A (PlayerDamageReceiver2D missing)");
        }
        else if (selectedGroup == 1)
        {
            if (attack == null) text.AppendLine("Attack: N/A (PlayerAttack2D missing)");
            else
            {
                text.AppendLine($"Step {attack.ComboStep}/4 / Phase {attack.Phase} / Frame {attack.ElapsedFrames} / Next {attack.HasQueuedAttack}");
                text.AppendLine(attack.UsesFrameDefinition ? $"Common cancel: [{attack.CancelStartFrame}..{attack.CancelEndFrame}] / Open {attack.OpenCancelWindows}" : $"Legacy event profile / Windows {attack.OpenCancelWindows}");
                text.AppendLine($"Buffer {attack.InputBufferRemaining * 60:0}F / End {attack.LastEndReason} / Full {attack.FullComboCount}/{attack.ComboAttemptCount} / Fallback {attack.TimeoutFallbackCount}");
                if (attack.Definition != null)
                {
                    var step = attack.Definition.steps[Mathf.Clamp(attack.ComboStep - 1, 0, 3)];
                    text.AppendLine($"Hitbox height {step.radius * 2:0.000}u / forward extension {step.forwardExtension:0.00000}u (Gizmos)");
                }
            }
            text.AppendLine(parry != null ? $"Parry {parry.ElapsedFrames}F / Accept [{parry.AcceptanceStartFrame}..{parry.AcceptanceEndFrame}] / CD {parry.ParryCooldownRemaining * 60:0}F\nSuccess {parry.SuccessCount}/{parry.AttemptCount} / Just {parry.JustSuccessCount} / Miss {parry.MissCount}" : "Parry: N/A (PlayerParry2D missing)");
            if (counter == null) text.AppendLine("Counter: N/A (PlayerParryCounter2D missing)");
            else
            {
                text.AppendLine($"Stocks {counter.StockCount} / Selected {counter.SelectedTarget?.IndividualId ?? "None"} / Chain {counter.HasQueuedCounter}");
                text.AppendLine($"Consume {counter.ConsumedCount} / {counter.LastStockReason}\n{counter.LastSelectionReason} / {counter.SelectedDamage} damage x{counter.SelectedMultiplier:0.0}");
                var cinematic = GetComponent<PrototypeCounterPresentation>();
                text.AppendLine($"Counter {counter.CinematicPhase} {counter.ElapsedFrames}F / Hit {counter.HitFrame}F / End {counter.LandingFrame}F / Invincible {counter.IsCounterInvincible} / {counter.LastEndReason}");
                if (cinematic != null) text.AppendLine($"Dim {cinematic.DimAlpha:0.00} / Hidden {cinematic.IsBodyHidden} / VFX {cinematic.ActiveEffectCount}");
                for (int i = 0; i < counter.Stocks.Count; i++)
                    text.AppendLine($"  #{i + 1} {counter.Stocks[i].IndividualId}: {counter.Stocks[i].ParryStaggerRemainingFrames}F / {counter.EligibilityReason(counter.Stocks[i])}");
                text.AppendLine("Opportunity: separate timed contract (not a parry stock)");
            }
            text.AppendLine(specialGauge != null ? $"Gauge {specialGauge.CurrentGauge:0}/{specialGauge.MaxGauge:0}" : "Gauge: N/A (PlayerSpecialGauge missing)");
        }
        else
        {
            if (telemetry != null)
            {
                text.AppendLine($"Damage dealt {telemetry.TotalDamage} / DPS 5s {telemetry.RecentDps:0.0} / Cumulative {telemetry.CumulativeDps:0.0}");
                text.AppendLine($"Damage received {telemetry.TotalReceivedDamage}");
                foreach (var pair in telemetry.ReceivedBySource) text.AppendLine($"  {pair.Key}: {pair.Value}");
            }
            else text.AppendLine("DPS: N/A (telemetry not connected)");
            if (damageReceiver != null) text.AppendLine($"Last hit @{damageReceiver.LastDamageTick}: {damageReceiver.LastDamageCause} / source {damageReceiver.LastDamageSourceId} / attack {damageReceiver.LastDamageAttackId}");
            EnemyMeleeChaser2D[] enemies = FindObjectsByType<EnemyMeleeChaser2D>(FindObjectsSortMode.None);
            if (enemies.Length == 0) text.AppendLine("Enemy: N/A (no active melee enemy in scene)");
            for (int i = 0; i < Mathf.Min(8, enemies.Length); i++)
            {
                var enemy = enemies[i]; var health = enemy.GetComponent<Damageable2D>();
                text.AppendLine($"{health.IndividualId}: {enemy.CurrentStateName}, {health.CurrentHp}/{health.MaxHp} HP / {enemy.AttackKind}\n  Windup {enemy.WindupRemainingFrames}F / Active {enemy.ActiveRemainingFrames}F / Attack {enemy.AttackId} #{enemy.AttackSequence}\n  Role {enemy.AttackRoleReason} / Counter x{health.CounterMultiplier:0.00} / Reaction {health.LastHitReaction} / Armor {enemy.ResistsHitStun}");
                var launch = enemy.GetComponent<EnemyCounterLaunch2D>();
                text.AppendLine($"  Parry stagger {health.ParryStaggerRemainingFrames}F / configured {health.ConfiguredParryStaggerFrames}F");
                var enemyArt = enemy.GetComponent<PrototypeEnemyArtPresentation>();
                text.AppendLine(enemyArt != null && enemyArt.HasAuthoredParryCue
                    ? $"  Authored flash {enemyArt.IsAuthoredParryCueVisible} / source {enemyArt.WindupSourceFrame}F / lead {enemy.ParryCueLeadFrames}F (no extra marker)"
                    : $"  Placeholder parry cue {enemy.IsParryCueActive} / last {enemy.ParryCueLeadFrames}F of windup");
                if (launch != null) text.AppendLine($"  Launch {launch.IsActive} {launch.ElapsedFrames}F / {launch.TravelDistance:0.00}u / {launch.LastEndReason} / Motion owners {health.MotionOwnerCount}");
            }
            var feedback = GetComponent<CombatFeedback2D>();
            if (feedback != null) text.AppendLine($"Feedback {feedback.LastKind} @{feedback.LastEmissionTick} / emissions {feedback.EmissionCount} / active {feedback.ActiveEffectCount} / flash {feedback.ActiveFlashCount}");
        }
        return text.ToString();
    }

    private void OnGUI()
    {
        if (!visible)
        {
            if (GUI.Button(new Rect(12, 12, 120, 28), "Show Debug")) visible = true;
            return;
        }
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
        labelStyle.fontSize = fontSize;
        float width = Mathf.Min(640, Screen.width * .42f);
        float height = Mathf.Min(700, Screen.height - 24);
        GUILayout.BeginArea(new Rect(12, 12, width, height), "Combat Debug", GUI.skin.window);
        if (GUILayout.Button("Hide Debug")) visible = false;
        int selected = GUILayout.Toolbar(group, Groups);
        if (selected != group) { group = selected; nextRefresh = 0; }
        if (GUILayout.Button("Reset damage measurements")) { telemetry?.ResetMeasurements(); nextRefresh = 0; }
        if (snapshot == null || Time.unscaledTime >= nextRefresh)
        { snapshot = BuildSnapshot(group); nextRefresh = Time.unscaledTime + .1f; }
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(snapshot, labelStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
