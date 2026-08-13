using UnityEngine;

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
    [SerializeField] private int fontSize = 20;

    private GUIStyle boxStyle;
    private GUIStyle labelStyle;

    private void Awake()
    {
        if (motor == null) motor = GetComponent<PlayerMotor2D>();
        if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
        if (attack == null) attack = GetComponent<PlayerAttack2D>();
        if (damageReceiver == null) damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        if (parry == null) parry = GetComponent<PlayerParry2D>();
        if (specialGauge == null) specialGauge = GetComponent<PlayerSpecialGauge>();
        if (specialSkill == null) specialSkill = GetComponent<PlayerSpecialSkill2D>();
    }

    private void EnsureGuiStyles()
    {
        if (boxStyle == null) boxStyle = new GUIStyle(GUI.skin.box);
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label);
        boxStyle.fontSize = fontSize;
        labelStyle.fontSize = fontSize;
    }

    private void OnGUI()
    {
        if (!visible) return;

        EnsureGuiStyles();
        GUI.Box(new Rect(12, 12, 920, 574), "Player Debug", boxStyle);

        string state = stateMachine != null ? stateMachine.CurrentState.ToString() : "N/A";
        string grounded = motor != null && motor.IsGrounded ? "Yes" : "No";
        string dashing = motor != null && motor.IsDashing ? "Yes" : "No";
        string cd = motor != null ? motor.DashCooldownRemaining.ToString("0.00") : "N/A";
        string attackState = attack != null
            ? $"{(attack.IsAttacking ? "Active" : "Idle")} / Step {attack.ComboStep}/{attack.MaxComboStep} / Hit {(attack.HitAppliedForCurrentStep ? "Yes" : "No")}"
            : "N/A";
        string attackTiming = attack != null
            ? $"Window {(attack.IsComboWindowOpen ? "Open" : "Closed")} / Queued {(attack.HasQueuedAttack ? "Yes" : "No")} / Buffer {attack.InputBufferRemaining:0.000}s / Fallback {attack.StepTimeoutRemaining:0.000}s"
            : "N/A";
        string attackStats = attack != null
            ? $"Attempt {attack.ComboAttemptCount} / Full {attack.FullComboCount} / Rate {attack.FullComboRate * 100f:0.0}% / Timeout {attack.TimeoutFallbackCount} / End {attack.LastEndReason}"
            : "N/A";
        string hp = damageReceiver != null ? damageReceiver.CurrentHp.ToString() : "N/A";
        string parryActive = parry != null && parry.IsParryActive ? "Yes" : "No";
        string parryRemain = parry != null ? parry.ParryRemaining.ToString("0.00") : "N/A";
        string parryLast = damageReceiver != null ? damageReceiver.LastParryResult.ToString() : "N/A";
        string failLock = parry != null ? parry.FailLockRemaining.ToString("0.00") : "N/A";
        string parryAttempts = parry != null ? parry.AttemptCount.ToString() : "N/A";
        string parrySuccess = parry != null ? parry.SuccessCount.ToString() : "N/A";
        string parryJust = parry != null ? parry.JustSuccessCount.ToString() : "N/A";
        string parryMiss = parry != null ? parry.MissCount.ToString() : "N/A";
        string parryRate = parry != null ? $"{parry.SuccessRate * 100f:0.0}%" : "N/A";
        string gauge = specialGauge != null ? $"{specialGauge.CurrentGauge:0}/{specialGauge.MaxGauge:0} ({specialGauge.GaugeRate * 100f:0}%)" : "N/A";
        string special = specialSkill != null ? (specialSkill.IsUsingSkill ? $"Yes ({specialSkill.LockRemaining:0.00}s)" : "No") : "N/A";
        string hitsTaken = damageReceiver != null ? damageReceiver.TotalHitsTaken.ToString() : "N/A";
        string blockedParry = damageReceiver != null ? damageReceiver.BlockedByParry.ToString() : "N/A";
        string blockedIFrame = damageReceiver != null ? damageReceiver.BlockedByInvincible.ToString() : "N/A";
        string blockedDash = damageReceiver != null ? damageReceiver.BlockedByDash.ToString() : "N/A";

        GUI.Label(new Rect(28, 56, 600, 30), $"State: {state}", labelStyle);
        GUI.Label(new Rect(28, 90, 600, 30), $"Grounded: {grounded} / Dashing: {dashing}", labelStyle);
        GUI.Label(new Rect(28, 124, 660, 30), $"Attack: {attackState}", labelStyle);
        GUI.Label(new Rect(28, 158, 860, 30), $"Attack Timing: {attackTiming}", labelStyle);
        GUI.Label(new Rect(28, 192, 860, 30), $"Attack Stats: {attackStats}", labelStyle);
        GUI.Label(new Rect(28, 226, 600, 30), $"Dash CD: {cd}", labelStyle);
        GUI.Label(new Rect(28, 260, 600, 30), $"HP: {hp}", labelStyle);
        GUI.Label(new Rect(28, 294, 600, 30), $"Parry Active: {parryActive} ({parryRemain}s)", labelStyle);
        GUI.Label(new Rect(28, 328, 600, 30), $"Parry Result: {parryLast}", labelStyle);
        GUI.Label(new Rect(28, 362, 600, 30), $"Parry FailLock: {failLock}s", labelStyle);
        GUI.Label(new Rect(28, 396, 600, 30), $"Special Gauge: {gauge}", labelStyle);
        GUI.Label(new Rect(28, 430, 600, 30), $"Special Active: {special}", labelStyle);
        GUI.Label(new Rect(28, 464, 860, 30), $"Parry Stats: Attempt {parryAttempts} / Success {parrySuccess} / Just {parryJust} / Miss {parryMiss}", labelStyle);
        GUI.Label(new Rect(28, 498, 860, 30), $"Parry Success Rate: {parryRate} (diagnostic)", labelStyle);
        GUI.Label(new Rect(28, 532, 860, 30), $"Damage Stats: Taken {hitsTaken} / Blocked(Parry {blockedParry}, IFrame {blockedIFrame}, Dash {blockedDash})", labelStyle);
        GUI.Label(new Rect(730, 56, 180, 30), $"FPS: {(1f / Time.unscaledDeltaTime):0}", labelStyle);
    }
}
