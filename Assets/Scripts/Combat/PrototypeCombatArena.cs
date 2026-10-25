using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Repeatable acceptance arena, separate from filming and user scenes.</summary>
public sealed class PrototypeCombatArena : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine player;
    [SerializeField] private EnemyMeleeChaser2D[] enemies;
    [SerializeField] private EnemyMeleeChaser2D strongEnemy;
    [SerializeField] private EnemyAttackCoordinator2D coordinator;
    [SerializeField] private CombatFeedback2D feedback;
    private Vector3 strongPosition;
    private InputActionMap controls;
    private Vector3 playerPosition;
    private Vector3[] enemyPositions;
    private int[] enemyHp;
    private InputAction reset;
    private void Awake()
    {
        if (strongEnemy != null) strongPosition = strongEnemy.transform.position;
        playerPosition = player.transform.position;
        enemyPositions = new Vector3[enemies.Length]; enemyHp = new int[enemies.Length];
        for (int i = 0; i < enemies.Length; i++)
        {
            enemyPositions[i] = enemies[i].transform.position;
            enemyHp[i] = enemies[i].Definition != null ? enemies[i].Definition.maxHp : enemies[i].GetComponent<Damageable2D>().MaxHp;
        }
    }
    private void OnEnable()
    {
        controls = new InputActionMap("PrototypeValidation");
        for (int i = 1; i <= 9; i++) controls.AddAction("F" + i, InputActionType.Button, "<Keyboard>/f" + i);
        controls.Enable(); reset = controls.FindAction("F2");
    }
    private void Update()
    {
        if (reset.WasPressedThisFrame()) ResetActionsOnly();
        if (controls.FindAction("F1").WasPressedThisFrame()) ResetArena(false);
        if (controls.FindAction("F3").WasPressedThisFrame()) ResetArena(true);
        if (controls.FindAction("F4").WasPressedThisFrame()) ResetStrongEncounter(false);
        if (controls.FindAction("F5").WasPressedThisFrame()) ResetStrongEncounter(true);
        if (feedback == null || feedback.Settings == null) return;
        var settings = feedback.Settings;
        if (controls.FindAction("F6").WasPressedThisFrame()) settings.shake = Cycle(settings.shake);
        if (controls.FindAction("F7").WasPressedThisFrame()) settings.flash = Cycle(settings.flash);
        if (controls.FindAction("F8").WasPressedThisFrame()) settings.vfx = Cycle(settings.vfx);
        if (controls.FindAction("F9").WasPressedThisFrame()) settings.masterVolume = settings.masterVolume > 0 ? 0 : 1;
    }
    private static float Cycle(float value) => value >= .99f ? 0 : value < .49f ? .5f : 1;
    public void ResetActionsOnly() => ResetCore(0);
    public void ResetArena(bool twoTargets) => ResetCore(twoTargets ? 2 : 1);
    private void ResetCore(int normalCount)
    {
        coordinator?.ResetEncounter(); feedback?.ResetPresentation();
        if (strongEnemy != null) strongEnemy.gameObject.SetActive(false);
        player.ResetToSafeState("PrototypeArenaReset", true);
        var body = player.GetComponent<Rigidbody2D>();
        player.transform.position = playerPosition; body.position = playerPosition; body.linearVelocity = Vector2.zero;
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        player.GetComponent<PlayerHeal2D>().Refill();
        player.GetComponent<PlayerSpecialGauge>().ResetGauge();
        player.GetComponent<PlayerCombatTelemetry>()?.ResetMeasurements();
        for (int i = 0; i < enemies.Length; i++)
        {
            var health = enemies[i].GetComponent<Damageable2D>();
            int hp = health.Definition != null ? health.ConfiguredMaxHp : enemyHp[i];
            health.ResetForCapture(enemyPositions[i], hp);
            enemies[i].ResetForCapture(player.transform, false);
            enemies[i].gameObject.SetActive(i < normalCount);
        }
        player.ClearBufferedGameplayInput();
        Physics2D.SyncTransforms();
        player.GetComponent<PlayerInteractor2D>()?.RefreshAfterTeleport();
    }
    public void ResetStrongEncounter(bool includeNormal)
    {
        ResetCore(includeNormal ? 1 : 0);
        if (strongEnemy == null) return;
        var health = strongEnemy.GetComponent<Damageable2D>();
        int hp = health.Definition != null ? health.ConfiguredMaxHp : strongEnemy.Definition.maxHp;
        health.ResetForCapture(strongPosition, hp);
        strongEnemy.ResetForCapture(player.transform, false);
        coordinator?.ResetEncounter(); Physics2D.SyncTransforms();
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 310, 12, 298, 280), GUI.skin.box);
        GUILayout.Label("Prototype action / combat acceptance");
        if (GUILayout.Button("F1: Parry practice (one normal)")) ResetArena(false);
        if (GUILayout.Button("F2: Reset actions only (no enemies)")) ResetActionsOnly();
        GUILayout.Label("F3: Two / F4: Strong / F5: Mixed");
        GUILayout.Label("K / RMB: Parry - J / LMB: Counter");
        if (coordinator != null) GUILayout.Label($"Attack roles: {coordinator.ActiveAttackers}/{coordinator.MaximumAttackers}");
        if (feedback != null && feedback.Settings != null)
        {
            var s = feedback.Settings;
            GUILayout.Label($"F6 Camera (shake / zoom) {s.shake:P0}");
            GUILayout.Label($"F7 Flash {s.flash:P0}");
            GUILayout.Label($"F8 VFX {s.vfx:P0} / F9 Sound {s.masterVolume:P0}");
            GUILayout.Label("0% keeps essential hit/telegraph shapes");
            GUILayout.Label($"Feedback: {feedback.LastKind} @{feedback.LastEmissionTick}");
        }
        GUILayout.Label("B: Interact in range / Dash otherwise");
        GUILayout.EndArea();
    }
    private void OnDisable() { controls?.Dispose(); controls = null; reset = null; }
}
