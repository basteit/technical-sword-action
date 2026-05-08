using UnityEngine;

public class PlayerDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlayerMotor2D motor;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerDamageReceiver2D damageReceiver;
    [SerializeField] private bool visible = true;

    private void Awake()
    {
        if (motor == null)
        {
            motor = GetComponent<PlayerMotor2D>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (damageReceiver == null)
        {
            damageReceiver = GetComponent<PlayerDamageReceiver2D>();
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        GUI.Box(new Rect(12, 12, 320, 140), "Player Debug");

        string state = stateMachine != null ? stateMachine.CurrentState.ToString() : "N/A";
        string grounded = motor != null && motor.IsGrounded ? "Yes" : "No";
        string dashing = motor != null && motor.IsDashing ? "Yes" : "No";
        string cd = motor != null ? motor.DashCooldownRemaining.ToString("0.00") : "N/A";
        string hp = damageReceiver != null ? damageReceiver.CurrentHp.ToString() : "N/A";

        GUI.Label(new Rect(24, 38, 300, 20), $"State: {state}");
        GUI.Label(new Rect(24, 58, 300, 20), $"Grounded: {grounded} / Dashing: {dashing}");
        GUI.Label(new Rect(24, 78, 300, 20), $"Dash CD: {cd}");
        GUI.Label(new Rect(24, 98, 300, 20), $"HP: {hp}");
        GUI.Label(new Rect(24, 118, 300, 20), $"FPS: {(1f / Time.unscaledDeltaTime):0}");
    }
}
