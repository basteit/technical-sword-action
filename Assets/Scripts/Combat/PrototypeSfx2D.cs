using System;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in SFX bus: gameplay remains authoritative; no audio event drives combat or animation.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10990)]
public sealed class PrototypeSfx2D : MonoBehaviour, ICombatTickListener
{
    private sealed class Voice
    {
        public AudioSource source;
        public float gain;
        public int priority;
        public bool paused;
        public float remaining;
    }
    private const int ActionVoices = 4, ImpactVoice = 4, ChargeVoice = 5;
    [SerializeField] private PrototypeSfxProfile profile;
    [SerializeField] private EnemyMeleeChaser2D[] enemies = Array.Empty<EnemyMeleeChaser2D>();
    private readonly Voice[] voices = new Voice[6];
    private readonly long[] lastCueTicks = new long[Enum.GetValues(typeof(PrototypeSfxCue)).Length];
    private readonly int[] cueCounts = new int[Enum.GetValues(typeof(PrototypeSfxCue)).Length];
    private long impactTick = -1, decisionTick = -1;
    private int impactPriority = -1, currentStep;
    private bool initialized, wasGrounded, swingPending, sheathePending, healApplied, deathHandled, suppressCounterLanding;
    private float walkDistance, lastX;
    private long[] dangerSequences;
    private PlayerStateMachine state;
    private PlayerMotor2D motor;
    private PlayerAttack2D attack;
    private PlayerParryCounter2D counter;
    private PlayerHeal2D heal;
    private PlayerDamageReceiver2D health;
    private CombatFeedback2D feedback;
    private PrototypeSfxProfile validated;
    private bool valid;
    public int CombatTickOrder => 650;
    public PrototypeSfxProfile Profile => profile;
    public int PlayedCount { get; private set; }
    public PrototypeSfxCue LastCue { get; private set; }
    public long LastCueTick { get; private set; } = -1;
    public int ActiveVoiceCount
    {
        get { int n = 0; foreach (var v in voices) if (v != null && (v.source.isPlaying || v.paused)) n++; return n; }
    }
    public bool ChargeLoopActive => voices[ChargeVoice] != null &&
        (voices[ChargeVoice].source.isPlaying || voices[ChargeVoice].paused);
    public bool IsReady
    {
        get
        {
            if (validated != profile) { validated = profile; valid = profile != null && profile.IsValid; }
            return isActiveAndEnabled && valid && feedback != null && feedback.Settings != null;
        }
    }
    public static bool IsManaging(GameObject root) => root != null && root.TryGetComponent<PrototypeSfx2D>(out var sfx) && sfx.IsReady;
    public int Count(PrototypeSfxCue cue) => cueCounts[(int)cue];
    public bool OwnsAudioSource(AudioSource source)
    { foreach (var voice in voices) if (voice != null && voice.source == source) return true; return false; }

    private void Awake()
    {
        state = GetComponent<PlayerStateMachine>(); motor = GetComponent<PlayerMotor2D>();
        attack = GetComponent<PlayerAttack2D>(); counter = GetComponent<PlayerParryCounter2D>();
        heal = GetComponent<PlayerHeal2D>(); health = GetComponent<PlayerDamageReceiver2D>();
        feedback = GetComponent<CombatFeedback2D>();
        var root = new GameObject("PrototypeFreeSfx"); root.transform.SetParent(transform, false);
        for (int i = 0; i < voices.Length; i++)
        {
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.volume = 0;
            voices[i] = new Voice { source = source };
        }
        Array.Fill(lastCueTicks, -1);
    }
    private void OnEnable()
    {
        if (attack != null) attack.StepStarted += OnStep;
        if (counter != null)
        {
            counter.BlinkStarted += OnTransfer; counter.ChargeStarted += OnCharge;
            counter.ChargeReleased += OnRelease; counter.Finished += OnCounterFinished; counter.Landed += OnCounterLanded;
        }
        if (health != null) health.DamageReceived += OnDamage;
        dangerSequences = new long[enemies.Length]; Array.Fill(dangerSequences, -1);
        foreach (var enemy in enemies) if (enemy != null) { enemy.WindupStarted += OnEnemyWindup; enemy.AttackStarted += OnEnemyAttack; }
        CombatTimeController.Register(this);
        CombatTimeController.SessionReset += ResetAudio;
        CombatTimeController.PauseChanged += OnPause;
        SceneManager.activeSceneChanged += OnSceneChanged;
        initialized = false;
    }
    private void OnStep(int step) { currentStep = step; swingPending = sheathePending = true; }
    private void OnTransfer() => Play(PrototypeSfxCue.CounterTransfer);
    private void OnCharge() => Play(PrototypeSfxCue.CounterCharge, false, true);
    private void OnRelease() { StopCharge(); Play(PrototypeSfxCue.CounterRelease); }
    private void OnCounterFinished(bool completed) { StopCharge(); if (!completed) StopActions(); swingPending = sheathePending = false; }
    private void OnCounterLanded() { Play(PrototypeSfxCue.Land); suppressCounterLanding = true; }
    private void OnEnemyWindup() => Play(PrototypeSfxCue.EnemyWindup);
    private void OnEnemyAttack() => Play(PrototypeSfxCue.EnemyAttack);
    private void OnDamage(int amount, string source, long sequence)
    {
        StopCharge(); StopActions(); swingPending = sheathePending = false;
        // Fatal damage resets the combat session immediately after this callback.
        // Start its fall sound from the resulting Dead state, not before that reset.
    }

    public bool TryPlayFeedback(CombatFeedbackKind kind)
    {
        if (!IsReady) return false;
        var cue = kind switch {
            CombatFeedbackKind.LightHit => PrototypeSfxCue.LightHit,
            CombatFeedbackKind.HeavyHit => state != null && state.ActionState == PlayerActionState.Special ? PrototypeSfxCue.SpecialHit : PrototypeSfxCue.HeavyHit,
            CombatFeedbackKind.Counter => PrototypeSfxCue.CounterHit,
            CombatFeedbackKind.Parry => PrototypeSfxCue.Parry,
            CombatFeedbackKind.JustParry => PrototypeSfxCue.JustParry,
            CombatFeedbackKind.PlayerHit => PrototypeSfxCue.PlayerHit,
            CombatFeedbackKind.Defeat => PrototypeSfxCue.EnemyDefeat,
            _ => PrototypeSfxCue.ParryMiss
        };
        if (kind == CombatFeedbackKind.Counter) StopCharge();
        Play(cue, true); return true;
    }

    public void CombatTick()
    {
        if (!IsReady || state == null || motor == null || CombatTimeController.IsPaused) return;
        if (!initialized)
        { initialized = true; lastX = transform.position.x; wasGrounded = motor.IsGrounded; return; }
        if (state.LifeState == PlayerLifeState.Dead)
        {
            StopCharge();
            if (!deathHandled) { StopActions(); Play(PrototypeSfxCue.PlayerDeath); deathHandled = true; }
            wasGrounded = motor.IsGrounded; lastX = transform.position.x; walkDistance = 0; return;
        }
        if (deathHandled) { ResetAudio(); initialized = true; lastX = transform.position.x; wasGrounded = motor.IsGrounded; return; }
        if (state.LastDecisionTick == CombatTimeController.TickCount && decisionTick != state.LastDecisionTick)
        {
            decisionTick = state.LastDecisionTick;
            switch (state.LastAcceptedRequest)
            {
                case PlayerActionRequest.Jump: if (!motor.IsDroppingThrough) Play(PrototypeSfxCue.Jump); break;
                case PlayerActionRequest.Dash: Play(PrototypeSfxCue.Dash); break;
                case PlayerActionRequest.Parry: Play(PrototypeSfxCue.ParryReady); break;
                case PlayerActionRequest.Special: Play(PrototypeSfxCue.SpecialCast); break;
                case PlayerActionRequest.Heal: healApplied = false; Play(PrototypeSfxCue.HealStart); break;
                case PlayerActionRequest.Interact: Play(PrototypeSfxCue.Interact); break;
            }
        }
        bool neutral = state.ActionState == PlayerActionState.Neutral && !DialogueController.GameplayInputBlocked;
        if (!wasGrounded && motor.IsGrounded && neutral && !motor.IsDroppingThrough && !suppressCounterLanding &&
            (counter == null || !counter.IsActive)) Play(PrototypeSfxCue.Land);
        if (motor.IsGrounded) suppressCounterLanding = false;
        if (motor.IsGrounded && wasGrounded && neutral && Mathf.Abs(motor.MoveInput) > .01f &&
            Mathf.Abs(motor.Velocity.x) > .1f && (attack == null || !attack.IsRecovering))
        {
            walkDistance += Mathf.Abs(transform.position.x - lastX);
            if (walkDistance >= profile.footstepDistance) { walkDistance %= profile.footstepDistance; Play(PrototypeSfxCue.Footstep); }
        }
        else walkDistance = 0;
        lastX = transform.position.x; wasGrounded = motor.IsGrounded;
        if (attack != null && currentStep >= 1 && currentStep <= 4 && attack.Definition != null)
        {
            var step = attack.Definition.steps[currentStep - 1];
            if (swingPending && attack.IsAttacking && attack.ComboStep == currentStep &&
                attack.ElapsedFrames >= Mathf.Max(0, step.activeStart - profile.swingLeadFrames))
            { Play((PrototypeSfxCue)((int)PrototypeSfxCue.Swing1 + currentStep - 1)); swingPending = false; }
            int sheathFrame = Mathf.RoundToInt(Mathf.Lerp(step.recoveryStart, step.endFrame, profile.sheatheRecoveryProgress));
            if (sheathePending && attack.IsRecovering && attack.ElapsedFrames >= sheathFrame)
            { Play(PrototypeSfxCue.Sheathe); sheathePending = false; }
            if (!attack.IsAttacking && !attack.IsRecovering) swingPending = sheathePending = false;
        }
        if (heal != null && heal.IsHealing && heal.HasApplied && !healApplied)
        { healApplied = true; Play(PrototypeSfxCue.HealApply); }
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.AttackKind != "Unparryable" || !enemy.IsWindingUp ||
                enemy.WindupRemainingFrames > enemy.ParryCueLeadFrames || dangerSequences[i] == enemy.AttackSequence) continue;
            dangerSequences[i] = enemy.AttackSequence; Play(PrototypeSfxCue.EnemyDanger);
        }
    }

    private void Play(PrototypeSfxCue cue, bool impact = false, bool loop = false)
    {
        if (!IsReady || CombatTimeController.IsPaused) return;
        var entry = profile.Find(cue);
        if (entry == null || lastCueTicks[(int)cue] == CombatTimeController.TickCount || MasterGain <= 0) return;
        int slot = loop ? ChargeVoice : impact ? ImpactVoice : -1;
        if (impact && impactTick == CombatTimeController.TickCount && entry.priority <= impactPriority) return;
        if (slot < 0)
        {
            for (int i = 0; i < ActionVoices; i++) if (!voices[i].source.isPlaying && !voices[i].paused) { slot = i; break; }
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < ActionVoices; i++) if (voices[i].priority < voices[slot].priority) slot = i;
                if (voices[slot].priority > entry.priority) return;
            }
        }
        var voice = voices[slot]; voice.source.Stop(); voice.paused = false;
        int index = cueCounts[(int)cue];
        voice.source.clip = entry.clips[index % entry.clips.Length]; voice.source.loop = loop;
        voice.source.pitch = entry.pitch + ((index % 3) - 1) * entry.pitchVariation;
        voice.source.priority = 256 - Mathf.Clamp(entry.priority * 2, 0, 256);
        voice.gain = entry.volume; voice.priority = entry.priority;
        voice.remaining = voice.source.clip.length / Mathf.Abs(voice.source.pitch) + .04f;
        voice.source.volume = entry.volume * MasterGain;
        voice.source.Play(); RefreshVolumes();
        lastCueTicks[(int)cue] = CombatTimeController.TickCount; cueCounts[(int)cue]++;
        PlayedCount++; LastCue = cue; LastCueTick = CombatTimeController.TickCount;
        if (impact) { impactTick = CombatTimeController.TickCount; impactPriority = entry.priority; }
    }
    private float MasterGain => feedback != null && feedback.Settings != null ?
        Mathf.Clamp01(feedback.Settings.masterVolume) * Mathf.Clamp01(feedback.Settings.sfxVolume) : 0;
    public void RefreshVolumes()
    {
        if (ChargeLoopActive && (counter == null || !counter.IsActive || counter.SelectedTarget == null ||
            !counter.SelectedTarget.ParticipatingInCombat)) StopCharge();
        float sum = 0;
        foreach (var voice in voices) if (voice != null && (voice.source.isPlaying || voice.paused)) sum += voice.gain;
        float headroom = sum > 1.6f ? 1.6f / sum : 1;
        foreach (var voice in voices) if (voice != null) voice.source.volume = voice.gain * MasterGain * headroom;
    }
    public void AdvanceAudio(float realSeconds)
    {
        if (!float.IsFinite(realSeconds) || realSeconds < 0 || CombatTimeController.IsPaused) return;
        foreach (var voice in voices)
        {
            if (voice == null || voice.source.loop || voice.paused || voice.remaining <= 0) continue;
            voice.remaining -= realSeconds;
            if (voice.remaining <= 0) voice.source.Stop();
        }
        RefreshVolumes();
    }
    private void LateUpdate() { if (IsReady) AdvanceAudio(Time.unscaledDeltaTime); }
    private void StopCharge() { if (voices[ChargeVoice] != null) { voices[ChargeVoice].source.Stop(); voices[ChargeVoice].paused = false; } }
    private void StopActions()
    { for (int i = 0; i < ActionVoices; i++) if (voices[i] != null) { voices[i].source.Stop(); voices[i].paused = false; } }
    private void OnPause(bool paused)
    {
        foreach (var voice in voices)
        {
            if (voice == null) continue;
            if (paused) { voice.paused = voice.source.isPlaying; if (voice.paused) voice.source.Pause(); }
            else if (voice.paused) { voice.paused = false; voice.source.UnPause(); }
        }
    }
    public void ResetAudio()
    {
        foreach (var voice in voices) if (voice != null) { voice.source.Stop(); voice.paused = false; voice.gain = 0; }
        initialized = swingPending = sheathePending = healApplied = deathHandled = suppressCounterLanding = false;
        currentStep = 0; walkDistance = 0; impactTick = decisionTick = -1; impactPriority = -1;
        Array.Fill(lastCueTicks, -1); if (dangerSequences != null) Array.Fill(dangerSequences, -1);
    }
    private void OnSceneChanged(Scene before, Scene after) => ResetAudio();
    private void OnDisable()
    {
        if (attack != null) attack.StepStarted -= OnStep;
        if (counter != null)
        {
            counter.BlinkStarted -= OnTransfer; counter.ChargeStarted -= OnCharge;
            counter.ChargeReleased -= OnRelease; counter.Finished -= OnCounterFinished; counter.Landed -= OnCounterLanded;
        }
        if (health != null) health.DamageReceived -= OnDamage;
        foreach (var enemy in enemies) if (enemy != null) { enemy.WindupStarted -= OnEnemyWindup; enemy.AttackStarted -= OnEnemyAttack; }
        CombatTimeController.Unregister(this); CombatTimeController.SessionReset -= ResetAudio;
        CombatTimeController.PauseChanged -= OnPause; SceneManager.activeSceneChanged -= OnSceneChanged;
        ResetAudio();
    }
}
