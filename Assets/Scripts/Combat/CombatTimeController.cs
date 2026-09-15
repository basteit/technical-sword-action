using System;
using System.Collections.Generic;
using TechnicalSwordAction.CombatTime;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface ICombatTickListener
{
    int CombatTickOrder { get; }
    void CombatTick();
}

public interface ICombatHitListener
{
    void ResolveCombatHits();
}

public interface ICombatTimerListener
{
    void CombatTickTimers();
}

/// <summary>The sole owner of combat time, 2D simulation, pause and hitstop.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CombatTimeController : MonoBehaviour
{
    private sealed class AnimatorRegistration
    {
        public bool WasEnabled;
        public readonly HashSet<object> Owners = new();
    }
    public const float StepSeconds = 1f / CombatClock.TickRate;
    private static CombatTimeController instance;
    private static readonly List<ICombatTickListener> listeners = new();
    private readonly List<ICombatTickListener> tickListeners = new();
    private readonly Dictionary<Animator, AnimatorRegistration> animators = new();
    private readonly List<Animator> animatorSnapshot = new();
    private readonly CombatClock clock = new();
    private SimulationMode2D previousSimulationMode;
    private float previousTimeScale;
    private float previousFixedDeltaTime;
    private bool ownsTime;
    public bool ManualAdvanceOnly { get; set; }
    public static event Action<long> BeforeCombatTick;

    public static bool IsPaused => instance != null && instance.clock.IsPaused;
    public static bool IsHitStopped => instance != null && instance.clock.IsHitStopped;
    public static bool IsSuspended => IsPaused || IsHitStopped;
    public static bool IsExecutingTick { get; private set; }
    public static bool AcceptsGameplayInput => !IsPaused;
    public static long TickCount => instance != null ? instance.clock.TickCount : 0L;
    public static double ElapsedSeconds => instance != null ? instance.clock.ElapsedSeconds : 0d;
    public static int HitstopRemainingTicks => instance != null ? instance.clock.HitstopRemainingTicks : 0;
    public static int HitstopOwnerCount => instance != null ? instance.clock.HitstopOwnerCount : 0;
    public static float AdvanceTimer(float remaining)
    {
        // Re-quantize after each subtraction so long timers do not accumulate float drift.
        int frames = Mathf.CeilToInt(remaining * CombatClock.TickRate - 0.0001f);
        return Mathf.Max(0, frames - 1) * StepSeconds;
    }
    public static event Action<bool> PauseChanged;
    // Extension point only: no multiplier, duration or defeat effect is selected here.
    public static event Action<object> DefeatSlowRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStatics()
    {
        instance = null;
        listeners.Clear();
        IsExecutingTick = false;
        PauseChanged = null;
        DefeatSlowRequested = null;
        BeforeCombatTick = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null || !Application.isPlaying) return;
        var host = new GameObject(nameof(CombatTimeController));
        DontDestroyOnLoad(host);
        host.AddComponent<CombatTimeController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        if (instance != this) return;
        previousSimulationMode = Physics2D.simulationMode;
        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        ownsTime = true;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Time.fixedDeltaTime = StepSeconds;
        clock.ResetStops();
        foreach (var pair in animators)
            if (pair.Key != null) pair.Key.enabled = false;
        ApplyTimeScale();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    public static void Register(ICombatTickListener listener)
    {
        if (listener == null || listeners.Contains(listener)) return;
        EnsureInstance();
        listeners.Add(listener);
    }

    public static void Unregister(ICombatTickListener listener) => listeners.Remove(listener);

    public static void RegisterAnimator(Animator animator, object owner)
    {
        EnsureInstance();
        if (instance == null || animator == null || owner == null) return;
        if (!instance.animators.TryGetValue(animator, out AnimatorRegistration registration))
        {
            registration = new AnimatorRegistration { WasEnabled = animator.enabled };
            instance.animators.Add(animator, registration);
        }
        registration.Owners.Add(owner);
        // Animator.Update is evaluated explicitly after physical movement, including events.
        if (instance.ownsTime) animator.enabled = false;
    }

    public static void UnregisterAnimator(Animator animator, object owner)
    {
        if (instance == null || animator == null || !instance.animators.TryGetValue(animator, out AnimatorRegistration registration)) return;
        registration.Owners.Remove(owner);
        if (registration.Owners.Count != 0) return;
        instance.animators.Remove(animator);
        animator.enabled = registration.WasEnabled;
    }

    public static void SetPaused(bool paused)
    {
        EnsureInstance();
        if (instance == null || instance.clock.IsPaused == paused) return;
        instance.clock.SetPaused(paused);
        instance.ApplyTimeScale();
        PauseChanged?.Invoke(paused);
    }

    public static void RequestHitstop(object owner, float duration)
    {
        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f) return;
        EnsureInstance();
        if (instance == null) return;
        int ticks = Mathf.CeilToInt(duration / StepSeconds - 0.00001f);
        instance.clock.RequestHitstop(owner, ticks);
        if (!IsExecutingTick) instance.ApplyTimeScale();
    }

    public static void ReleaseOwner(object owner)
    {
        if (instance == null) return;
        instance.clock.ReleaseOwner(owner);
        if (!IsExecutingTick) instance.ApplyTimeScale();
    }

    public static void ResetSession()
    {
        if (instance == null) return;
        bool wasPaused = IsPaused;
        instance.clock.ResetStops(!IsExecutingTick);
        instance.ApplyTimeScale();
        if (wasPaused) PauseChanged?.Invoke(false);
    }

    public static void RequestDefeatSlow(object source) => DefeatSlowRequested?.Invoke(source);

    private void LateUpdate()
    {
        if (!ManualAdvanceOnly) AdvanceFrame(Time.unscaledDeltaTime);
    }

    /// <summary>Also used by deterministic replay tests; callers disable automatic updates first.</summary>
    public void AdvanceFrame(double unscaledSeconds)
    {
        if (!ownsTime) return;
        try { clock.Advance(unscaledSeconds, ExecuteTick); }
        finally { ApplyTimeScale(); }
    }

    private static bool IsLive(ICombatTickListener listener) =>
        listener is MonoBehaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;

    private void ExecuteTick()
    {
        tickListeners.Clear();
        foreach (ICombatTickListener listener in listeners)
            if (IsLive(listener)) tickListeners.Add(listener);
        // List.Sort is not stable: use instance ID to give equal-order participants an explicit order.
        tickListeners.Sort((a, b) =>
        {
            int order = a.CombatTickOrder.CompareTo(b.CombatTickOrder);
            return order != 0 ? order : ((MonoBehaviour)a).GetInstanceID().CompareTo(((MonoBehaviour)b).GetInstanceID());
        });
        IsExecutingTick = true;
        try
        {
            BeforeCombatTick?.Invoke(clock.TickCount);
            foreach (ICombatTickListener listener in tickListeners)
                if (IsLive(listener)) listener.CombatTick();
            Physics2D.SyncTransforms();
            Physics2D.Simulate(StepSeconds);
            foreach (ICombatTickListener listener in tickListeners)
                if (IsLive(listener) && listener is ICombatHitListener hitListener) hitListener.ResolveCombatHits();
            animatorSnapshot.Clear();
            animatorSnapshot.AddRange(animators.Keys);
            foreach (Animator animator in animatorSnapshot)
                if (animator != null && animator.gameObject.activeInHierarchy && animator.runtimeAnimatorController != null)
                    animator.Update(StepSeconds);
            foreach (ICombatTickListener listener in tickListeners)
                if (IsLive(listener) && listener is ICombatTimerListener timerListener) timerListener.CombatTickTimers();
        }
        catch
        {
            ResetSession();
            foreach (ICombatTickListener listener in tickListeners)
                if (listener is PlayerStateMachine player && player != null)
                    player.ResetToSafeState("CombatTickException", player.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Dead);
            throw;
        }
        finally { IsExecutingTick = false; }
    }

    private void ApplyTimeScale()
    {
        if (!ownsTime) return;
        Time.timeScale = clock.IsPaused || clock.IsHitStopped ? 0f : 1f;
        Time.fixedDeltaTime = StepSeconds;
    }

    private void ResetActors(string reason)
    {
        ResetSession();
        foreach (ICombatTickListener listener in listeners.ToArray())
            if (listener is PlayerStateMachine player && player != null)
                player.ResetToSafeState(reason, player.LifeState != TechnicalSwordAction.PlayerState.PlayerLifeState.Dead);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ResetActors("SceneLoaded");
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => ResetActors("ActiveSceneChanged");

    private void OnDisable()
    {
        if (!ownsTime) return;
        ResetActors("TimeControllerDisabled");
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        foreach (var pair in animators)
            if (pair.Key != null) pair.Key.enabled = pair.Value.WasEnabled;
        Physics2D.simulationMode = previousSimulationMode;
        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        ownsTime = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
