using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Repeatable acceptance arena, separate from filming and user scenes.</summary>
public sealed class PrototypeCombatArena : MonoBehaviour
{
    [SerializeField] private PlayerStateMachine player;
    [SerializeField] private EnemyMeleeChaser2D[] enemies;
    private Vector3 playerPosition;
    private Vector3[] enemyPositions;
    private int[] enemyHp;
    private InputAction reset;
    private void Awake()
    {
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
        reset = new InputAction("ResetPrototype", InputActionType.Button, "<Keyboard>/f2");
        reset.Enable();
    }
    private void Update() { if (reset.WasPressedThisFrame()) ResetArena(false); }
    public void ResetArena(bool twoTargets)
    {
        player.ResetToSafeState("PrototypeArenaReset", true);
        var body = player.GetComponent<Rigidbody2D>();
        player.transform.position = playerPosition; body.position = playerPosition; body.linearVelocity = Vector2.zero;
        player.GetComponent<PlayerDamageReceiver2D>().ResetHealth();
        player.GetComponent<PlayerHeal2D>().Refill();
        player.GetComponent<PlayerSpecialGauge>().ResetGauge();
        player.GetComponent<PlayerCombatTelemetry>()?.ResetMeasurements();
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].GetComponent<Damageable2D>().ResetForCapture(enemyPositions[i], enemyHp[i]);
            enemies[i].ResetForCapture(player.transform, false);
            enemies[i].gameObject.SetActive(i == 0 || twoTargets);
        }
        player.ClearBufferedGameplayInput();
        Physics2D.SyncTransforms();
        player.GetComponent<PlayerInteractor2D>()?.RefreshAfterTeleport();
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 280, 12, 268, 130), GUI.skin.box);
        GUILayout.Label("Prototype #30 / #42 / #43 / #44 / #49");
        if (GUILayout.Button("Reset: 1 enemy (F2)")) ResetArena(false);
        if (GUILayout.Button("Reset: 2 enemies / stock test")) ResetArena(true);
        GUILayout.Label("B: Interact in range / Dash otherwise");
        GUILayout.EndArea();
    }
    private void OnDisable() { reset?.Dispose(); reset = null; }
}
