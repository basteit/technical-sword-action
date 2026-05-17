using UnityEngine;

public class PlayerDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerStateMachine stateMachine;
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
        GUI.Box(new Rect(12, 12, 640, 394), "Player Debug", boxStyle);

        string state = stateMachine != null ? stateMachine.CurrentState.ToString() : "N/A";
        string grounded = motor != null && motor.IsGrounded ? "Yes" : "No";
        string dashing = motor != null && motor.IsDashing ? "Yes" : "No";
        string cd = motor != null ? motor.DashCooldownRemaining.ToString("0.00") : "N/A";
        string hp = damageReceiver != null ? damageReceiver.CurrentHp.ToString() : "N/A";
        string parryActive = parry != null && parry.IsParryActive ? "Yes" : "No";
        string parryRemain = parry != null ? parry.ParryRemaining.ToString("0.00") : "N/A";
        string parryLast = damageReceiver != null ? damageReceiver.LastParryResult.ToString() : "N/A";
        string failLock = parry != null ? parry.FailLockRemaining.ToString("0.00") : "N/A";
        string gauge = specialGauge != null ? $"{specialGauge.CurrentGauge:0}/{specialGauge.MaxGauge:0} ({specialGauge.GaugeRate * 100f:0}%)" : "N/A";
        string special = specialSkill != null ? (specialSkill.IsUsingSkill ? $"Yes ({specialSkill.LockRemaining:0.00}s)" : "No") : "N/A";

        GUI.Label(new Rect(28, 56, 600, 30), $"State: {state}", labelStyle);
        GUI.Label(new Rect(28, 90, 600, 30), $"Grounded: {grounded} / Dashing: {dashing}", labelStyle);
        GUI.Label(new Rect(28, 124, 600, 30), $"Dash CD: {cd}", labelStyle);
        GUI.Label(new Rect(28, 158, 600, 30), $"HP: {hp}", labelStyle);
        GUI.Label(new Rect(28, 192, 600, 30), $"Parry Active: {parryActive} ({parryRemain}s)", labelStyle);
        GUI.Label(new Rect(28, 226, 600, 30), $"Parry Result: {parryLast}", labelStyle);
        GUI.Label(new Rect(28, 260, 600, 30), $"Parry FailLock: {failLock}s", labelStyle);
        GUI.Label(new Rect(28, 294, 600, 30), $"Special Gauge: {gauge}", labelStyle);
        GUI.Label(new Rect(28, 328, 600, 30), $"Special Active: {special}", labelStyle);
        GUI.Label(new Rect(28, 362, 600, 30), $"FPS: {(1f / Time.unscaledDeltaTime):0}", labelStyle);
    }
}
