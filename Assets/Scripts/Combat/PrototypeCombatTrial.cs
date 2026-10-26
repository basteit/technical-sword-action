using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PrototypeAttackTempo { Original, FastTrial, SnappyFirstTwo }

/// <summary>Scene-local, reversible comparisons. Switching always resets the current encounter first.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PrototypeCombatArena))]
public sealed class PrototypeCombatTrial : MonoBehaviour
{
    [SerializeField] private PlayerAttack2D attack;
    [SerializeField] private PlayerAttackDefinition originalAttack;
    [SerializeField] private PlayerAttackDefinition fastAttack;
    [SerializeField] private PlayerAttackDefinition snappyAttack;
    [SerializeField] private RuntimeAnimatorController originalController;
    [SerializeField] private RuntimeAnimatorController fastController;
    [SerializeField] private RuntimeAnimatorController snappyController;
    [SerializeField] private EnemyMeleeChaser2D[] normalEnemies;
    [SerializeField] private EnemyCombatDefinition originalEnemy;
    [SerializeField] private EnemyCombatDefinition referenceEnemy;
    [SerializeField] private CounterCinematicProfile referenceLaunch;
    [Header("Trial defaults; originals remain separate assets")]
    [SerializeField] private bool referenceTiming = true;
    [SerializeField] private bool referenceReaction = true;
    [SerializeField] private bool fastAttacks = true;
    [SerializeField] private bool snappyFirstTwo = true;
    private PrototypeCombatArena arena;
    private InputActionMap controls;
    public bool ReferenceTiming => referenceTiming;
    public bool ReferenceReaction => referenceReaction;
    public bool FastAttacks => fastAttacks;
    public PrototypeAttackTempo AttackTempo => !fastAttacks ? PrototypeAttackTempo.Original :
        snappyFirstTwo ? PrototypeAttackTempo.SnappyFirstTwo : PrototypeAttackTempo.FastTrial;
    public PlayerAttackDefinition ExpectedAttack => AttackTempo == PrototypeAttackTempo.Original ? originalAttack :
        AttackTempo == PrototypeAttackTempo.FastTrial ? fastAttack : snappyAttack;
    public RuntimeAnimatorController ExpectedController => AttackTempo == PrototypeAttackTempo.Original ? originalController :
        AttackTempo == PrototypeAttackTempo.FastTrial ? fastController : snappyController;
    public bool IsConfigured => attack != null && originalAttack != null && originalAttack.IsValid &&
        fastAttack != null && fastAttack.IsValid && snappyAttack != null && snappyAttack.IsValid &&
        originalController != null && fastController != null && snappyController != null &&
        originalEnemy != null && originalEnemy.IsValid &&
        referenceEnemy != null && referenceEnemy.IsValid && referenceLaunch != null && referenceLaunch.IsValid &&
        normalEnemies != null && normalEnemies.Length > 0 && normalEnemies.All(enemy => enemy != null);

    private void Awake() => arena = GetComponent<PrototypeCombatArena>();
    private void OnEnable()
    {
        controls = new InputActionMap("PrototypeCombatTrial");
        for (int key = 10; key <= 12; key++) controls.AddAction("F" + key, InputActionType.Button, "<Keyboard>/f" + key);
        controls.Enable();
    }
    private void Update()
    {
        if (controls.FindAction("F10").WasPressedThisFrame()) CycleAttackTempo();
        if (controls.FindAction("F11").WasPressedThisFrame()) SetEnemyTiming(!referenceTiming);
        if (controls.FindAction("F12").WasPressedThisFrame()) SetEnemyReaction(!referenceReaction);
    }
    // Preserve the previous bool API: true selects the unchanged 10/25 FAST baseline, not the new trial.
    public void SetModes(bool timing, bool reaction, bool speed)
        => SetComparisonModes(timing, reaction, speed ? PrototypeAttackTempo.FastTrial : PrototypeAttackTempo.Original);
    public void SetAttackTempo(PrototypeAttackTempo tempo) => SetComparisonModes(referenceTiming, referenceReaction, tempo);
    public void SetEnemyTiming(bool timing) => SetComparisonModes(timing, referenceReaction, AttackTempo);
    public void SetEnemyReaction(bool reaction) => SetComparisonModes(referenceTiming, reaction, AttackTempo);
    public void CycleAttackTempo() => SetAttackTempo(AttackTempo == PrototypeAttackTempo.SnappyFirstTwo ? PrototypeAttackTempo.FastTrial :
        AttackTempo == PrototypeAttackTempo.FastTrial ? PrototypeAttackTempo.Original : PrototypeAttackTempo.SnappyFirstTwo);
    private void SetComparisonModes(bool timing, bool reaction, PrototypeAttackTempo tempo)
    {
        if (!IsConfigured || !System.Enum.IsDefined(typeof(PrototypeAttackTempo), tempo)) return;
        referenceTiming = timing; referenceReaction = reaction;
        fastAttacks = tempo != PrototypeAttackTempo.Original; snappyFirstTwo = tempo == PrototypeAttackTempo.SnappyFirstTwo;
        arena.ResetCurrentEncounter();
    }
    public void RestoreOriginals() => SetModes(false, false, false);

    /// <summary>Called by the arena after player cancellation, before restoring enemy health/AI.</summary>
    public void ApplyAfterPlayerReset()
    {
        if (!IsConfigured) { Debug.LogError("[PrototypeTrial] Missing/invalid comparison assets.", this); return; }
        if (!attack.TrySetDefinition(ExpectedAttack))
        { Debug.LogError("[PrototypeTrial] Attack was not reset before switching.", this); return; }
        var animator = attack.GetComponent<Animator>();
        var controller = ExpectedController;
        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            animator.Rebind(); animator.speed = 1; animator.Update(0);
        }
        foreach (var enemy in normalEnemies)
        {
            if (enemy == null) continue;
            var launch = enemy.GetComponent<EnemyCounterLaunch2D>();
            // Inactive scene enemies may not have run Awake yet; do not touch an uninitialized rest rotation.
            if (launch != null && launch.IsActive) launch.CancelLaunch();
            enemy.ConfigureDefinition(referenceTiming ? referenceEnemy : originalEnemy);
            enemy.GetComponent<Damageable2D>().ConfigureDefinition(referenceTiming ? referenceEnemy : originalEnemy);
            launch?.SetLaunchOverride(referenceReaction ? referenceLaunch : null);
            enemy.GetComponent<PrototypeEnemyArtPresentation>()?.SetDownParryPose(referenceReaction);
        }
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 310, 302, 298, 212), GUI.skin.box);
        GUILayout.Label("#34 Combat trial (switch = safe reset)");
        string tempo = AttackTempo == PrototypeAttackTempo.SnappyFirstTwo ? "SNAPPY 9/7/12/30F" :
            AttackTempo == PrototypeAttackTempo.FastTrial ? "FAST 12/9/12/30F" : "ORIGINAL 15/12/15/38F";
        if (GUILayout.Button("F10 Attack: " + tempo)) CycleAttackTempo();
        GUILayout.Label("Cycle: SNAPPY -> previous FAST -> ORIGINAL");
        if (GUILayout.Button("F11 Enemy: " + (referenceTiming ? "VIDEO 96/24F" : "ORIGINAL 30/30F")))
            SetEnemyTiming(!referenceTiming);
        if (GUILayout.Button("F12 Reaction: " + (referenceReaction ? "DOWN / LOW HOP" : "CHANCE / ROTATING")))
            SetEnemyReaction(!referenceReaction);
        if (GUILayout.Button("Restore ALL original settings")) RestoreOriginals();
        GUILayout.Label("No parry / counter / hitstop changes");
        GUILayout.EndArea();
    }
    private void OnDisable() { controls?.Dispose(); controls = null; }
}
