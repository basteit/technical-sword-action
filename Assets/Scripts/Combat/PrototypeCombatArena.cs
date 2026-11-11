using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Repeatable acceptance arena, separate from filming and user scenes.</summary>
public sealed class PrototypeCombatArena : MonoBehaviour
{
    private enum Encounter { ActionsOnly, OneNormal, TwoNormals, Training, MixedTraining }

    [SerializeField] private PlayerStateMachine player;
    [SerializeField] private EnemyMeleeChaser2D[] enemies;
    [SerializeField, Tooltip("Inactive strong-enemy fixture retained for regression tests; not a selectable encounter.")]
    private EnemyMeleeChaser2D strongEnemy;
    [SerializeField] private EnemyMeleeChaser2D trainingDummy;
    [SerializeField] private EnemyAttackCoordinator2D coordinator;
    [SerializeField] private CombatFeedback2D feedback;
    private Vector3 trainingPosition;
    private InputActionMap controls;
    private Vector3 playerPosition;
    private Vector3[] enemyPositions;
    private int[] enemyHp;
    private InputAction reset;
    private Encounter encounterPreset;
    private void Start()
    {
        if (GetComponent<PrototypeCombatTrial>() != null) ResetActionsOnly();
    }
    private void Awake()
    {
        if (trainingDummy != null) trainingPosition = trainingDummy.transform.position;
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
        for (int i = 1; i <= 9; i++)
        {
            var action = controls.AddAction("F" + i, InputActionType.Button, "<Keyboard>/f" + i);
            action.AddBinding("<Keyboard>/" + i);
            action.AddBinding("<Keyboard>/numpad" + i);
        }
        controls.Enable(); reset = controls.FindAction("F2");
    }
    private void Update()
    {
        if (reset.WasPressedThisFrame()) ResetActionsOnly();
        if (controls.FindAction("F1").WasPressedThisFrame()) ResetArena(false);
        if (controls.FindAction("F3").WasPressedThisFrame()) ResetArena(true);
        if (controls.FindAction("F4").WasPressedThisFrame()) ResetTrainingEncounter(false);
        if (controls.FindAction("F5").WasPressedThisFrame()) ResetTrainingEncounter(true);
        if (feedback == null || feedback.Settings == null) return;
        var settings = feedback.Settings;
        if (controls.FindAction("F6").WasPressedThisFrame()) settings.shake = Cycle(settings.shake);
        if (controls.FindAction("F7").WasPressedThisFrame()) settings.flash = Cycle(settings.flash);
        if (controls.FindAction("F8").WasPressedThisFrame()) settings.vfx = Cycle(settings.vfx);
        if (controls.FindAction("F9").WasPressedThisFrame()) settings.masterVolume = settings.masterVolume > 0 ? 0 : 1;
    }
    private static float Cycle(float value) => value >= .99f ? 0 : value < .49f ? .5f : 1;
    public void ResetActionsOnly() => SelectEncounter(Encounter.ActionsOnly);
    public void ResetArena(bool twoTargets) => SelectEncounter(twoTargets ? Encounter.TwoNormals : Encounter.OneNormal);
    public void ResetTrainingEncounter(bool includeNormal) => SelectEncounter(includeNormal ? Encounter.MixedTraining : Encounter.Training);
    public void ResetCurrentEncounter() => SelectEncounter(encounterPreset);

    private void SelectEncounter(Encounter preset)
    {
        encounterPreset = preset;
        int normalCount = preset switch
        {
            Encounter.TwoNormals => 2,
            Encounter.OneNormal or Encounter.MixedTraining => 1,
            _ => 0
        };
        coordinator?.ResetEncounter(); feedback?.ResetPresentation();
        if (strongEnemy != null) strongEnemy.gameObject.SetActive(false);
        if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
        player.ResetToSafeState("PrototypeArenaReset", true);
        GetComponent<PrototypeCombatTrial>()?.ApplyAfterPlayerReset();
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
        if (trainingDummy != null && (preset is Encounter.Training or Encounter.MixedTraining))
        {
            var health = trainingDummy.GetComponent<Damageable2D>();
            health.ResetForCapture(trainingPosition, health.ConfiguredMaxHp);
            trainingDummy.ResetForCapture(player.transform, false);
        }
        player.ClearBufferedGameplayInput();
        Physics2D.SyncTransforms();
        player.GetComponent<PlayerInteractor2D>()?.RefreshAfterTeleport();
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 310, 12, 298, 280), GUI.skin.box);
        GUILayout.Label("Prototype action / combat acceptance");
        if (GUILayout.Button("1: Parry practice (one normal)")) ResetArena(false);
        if (GUILayout.Button("2: Reset actions only (no enemies)")) ResetActionsOnly();
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("3: Two normals")) ResetArena(true);
            if (GUILayout.Button("4: Infinite HP dummy")) ResetTrainingEncounter(false);
        }
        if (GUILayout.Button("5: Normal + infinite HP dummy")) ResetTrainingEncounter(true);
        GUILayout.Label("K / RMB: Parry - J / LMB: Counter");
        if (coordinator != null) GUILayout.Label($"Attack roles: {coordinator.ActiveAttackers}/{coordinator.MaximumAttackers}");
        if (feedback != null && feedback.Settings != null)
        {
            var s = feedback.Settings;
            GUILayout.Label($"6 Camera {s.shake:P0} / 7 Flash {s.flash:P0}");
            GUILayout.Label($"8 VFX {s.vfx:P0} / 9 Sound {s.masterVolume:P0}");
            GUILayout.Label($"Feedback: {feedback.LastKind} @{feedback.LastEmissionTick}");
        }
        GUILayout.Label("B: Interact in range / Dash otherwise");
        GUILayout.EndArea();
    }
    private void OnDisable() { controls?.Dispose(); controls = null; reset = null; }
}
