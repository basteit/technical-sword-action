using TechnicalSwordAction.PlayerState;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRespawn2D : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField, Min(1)] private int delayFrames = 90;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float deathHeight = -20f;
    private Vector3 initialPosition;
    private PlayerStateMachine state;
    private PlayerDamageReceiver2D health;
    private Rigidbody2D body;
    private int deadFrames;
    public int CombatTickOrder => -200;
    private void Awake() => initialPosition = transform.position;
    private void OnEnable()
    {
        state = GetComponent<PlayerStateMachine>();
        health = GetComponent<PlayerDamageReceiver2D>();
        body = GetComponent<Rigidbody2D>();
        CombatTimeController.Register(this);
    }
    public void CombatTick()
    {
        if (state.LifeState == PlayerLifeState.Alive && transform.position.y < deathHeight)
            state.SetDead("FellOutOfWorld");
    }
    public void CombatTickTimers()
    {
        if (state.LifeState != PlayerLifeState.Dead) { deadFrames = 0; return; }
        if (++deadFrames >= Mathf.Max(1, delayFrames)) Respawn();
    }
    public void Respawn()
    {
        state.Revive("Respawn");
        transform.position = spawnPoint != null ? spawnPoint.position : initialPosition;
        if (body != null) { body.position = transform.position; body.linearVelocity = Vector2.zero; }
        health?.ResetHealth();
        GetComponent<PlayerHeal2D>()?.Refill();
        GetComponent<PlayerSpecialGauge>()?.ResetGauge();
        state.ClearBufferedGameplayInput();
        deadFrames = 0;
        Physics2D.SyncTransforms();
    }
    private void OnDisable() { CombatTimeController.Unregister(this); deadFrames = 0; }
}
