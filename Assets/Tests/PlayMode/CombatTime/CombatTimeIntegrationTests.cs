using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.CombatTime.Tests
{
    // Runtime components remain in Assembly-CSharp. Reflection keeps this test
    // assembly independent without moving unrelated gameplay into a new assembly.
    public sealed class CombatTimeIntegrationTests
    {
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        private readonly List<GameObject> objects = new();
        private readonly List<Scene> scenes = new();
        private Type timeType;
        private Behaviour controller;
        private bool previousManualAdvance;
        private Scene originalScene;
        private Action<long> tickCallback;

        private sealed class Rig
        {
            public GameObject Player;
            public Component State, Attack, Motor, Damage, Gauge, Enemy;
            public Rigidbody2D Body;
            public Animator Animator;
        }

        private sealed class Snapshot
        {
            public PlayerActionState Action;
            public PlayerLifeState Life;
            public int PlayerHp, EnemyHp, Hits, Attacks;
            public float Gauge, Lock, Invincible, Break, AnimationTime;
            public int AnimationState;
            public Vector2 Position, EnemyPosition;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeType = RuntimeType("CombatTimeController");
            Static("SetPaused", false);
            controller = (Behaviour)Object.FindFirstObjectByType(timeType);
            Assert.That(controller, Is.Not.Null);
            previousManualAdvance = Get<bool>(controller, "ManualAdvanceOnly");
            Property(controller, "ManualAdvanceOnly", true);
            Static("ResetSession");
            originalScene = SceneManager.GetActiveScene();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RemoveTickCallback();
            if (controller != null)
            {
                controller.enabled = true;
                Static("ResetSession");
            }
            foreach (GameObject item in objects)
                if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            if (originalScene.IsValid() && originalScene.isLoaded)
                SceneManager.SetActiveScene(originalScene);
            foreach (Scene scene in scenes)
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
            if (controller != null) Property(controller, "ManualAdvanceOnly", previousManualAdvance);
        }

        [UnityTest]
        public IEnumerator SameInputsAt30_60_120RenderFpsMatchEveryCombatTickIncludingRealHitsAndPhysics()
        {
            List<Snapshot> reference = null;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Static("ResetSession");
                Rig rig = CreateRig(true);
                yield return null;
                Assert.That(rig.Animator.enabled, Is.False, "Combat time must own Animator evaluation.");
                var history = new List<Snapshot>();
                long initialTick = GetStatic<long>("TickCount");
                object hitstopOwner = new object();
                tickCallback = absoluteTick =>
                {
                    long tick = absoluteTick - initialTick;
                    history.Add(Capture(rig));
                    if (tick == 1 || tick == 110)
                        Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Attack), Is.True);
                    if (tick == 20) Static("RequestHitstop", hitstopOwner, 6f / 60f);
                    if (tick == 70 || tick == 140)
                        Assert.That(Call<bool>(rig.Damage, "TryReceiveHit", 1, Vector2.left, 0f), Is.True);
                    if (tick == 71)
                        Assert.That(Call<bool>(rig.Damage, "TryReceiveHit", 1, Vector2.left, 0f), Is.False,
                            "Invincibility must prevent the second hit.");
                    Field(rig.Motor, "moveInput", tick >= 80 && tick < 95 ? 0.4f : 0f);
                };
                AddTickCallback();
                for (int frame = 0; frame < fps; frame++) Advance(1d / fps);
                Snapshot beforePause = Capture(rig);
                Static("SetPaused", true);
                Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Attack), Is.False);
                for (int frame = 0; frame < fps; frame++) Advance(1d / fps);
                AssertSnapshot(beforePause, Capture(rig), $"{fps} fps pause");
                Assert.That(Time.timeScale, Is.Zero);
                Static("SetPaused", false);
                // A deliberate new input frame follows the pause edge.
                yield return null;
                for (int frame = 0; frame < fps * 2 + fps / 10; frame++) Advance(1d / fps);
                RemoveTickCallback();
                history.Add(Capture(rig));
                Snapshot final = history[history.Count - 1];
                Assert.That(GetStatic<long>("TickCount") - initialTick, Is.EqualTo(180));
                Assert.That(final.EnemyHp, Is.EqualTo(48), "Real animation events must hit twice.");
                Assert.That(final.PlayerHp, Is.EqualTo(3));
                Assert.That(final.Hits, Is.EqualTo(2));
                Assert.That(final.Attacks, Is.EqualTo(2));
                Assert.That(final.Gauge, Is.EqualTo(24f).Within(0.0001f));
                Assert.That(final.Position.x, Is.GreaterThan(0.05f), "Physics must move the real Rigidbody2D.");
                Assert.That(Get<int>(rig.Attack, "TimeoutFallbackCount"), Is.Zero,
                    "Attacks must finish through actual Animator events.");
                Assert.That(final.Action, Is.EqualTo(PlayerActionState.Neutral));
                if (reference == null) reference = history;
                else
                {
                    Assert.That(history.Count, Is.EqualTo(reference.Count));
                    for (int index = 0; index < history.Count; index++)
                        AssertSnapshot(reference[index], history[index], $"{fps} fps tick {index}");
                }
                DestroyRig(rig);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ActualRenderLoopWith30_60_120ConfiguredCapsProducesIdenticalCombatTickHistory()
        {
            int savedTargetFrameRate = Application.targetFrameRate;
            int savedVSyncCount = QualitySettings.vSyncCount;
            bool savedManualAdvance = Get<bool>(controller, "ManualAdvanceOnly");
            List<Snapshot> reference = null;
            try
            {
                QualitySettings.vSyncCount = 0;
                foreach (int configuredFps in new[] { 30, 60, 120 })
                {
                    Property(controller, "ManualAdvanceOnly", true);
                    Application.targetFrameRate = configuredFps;
                    Static("ResetSession");
                    Rig rig = CreateRig(true);
                    // Let the actual render loop adopt the requested cap before sampling.
                    yield return null;
                    yield return null;
                    var history = new List<Snapshot>();
                    var framesWithTicks = new HashSet<int>();
                    long initialTick = GetStatic<long>("TickCount");
                    int tickCallbacks = 0;
                    int coroutineFrames = 0;
                    int initialFrame = Time.frameCount;
                    double startTime = Time.realtimeSinceStartupAsDouble;
                    object hitstopOwner = new object();
                    tickCallback = absoluteTick =>
                    {
                        tickCallbacks++;
                        framesWithTicks.Add(Time.frameCount);
                        long tick = absoluteTick - initialTick;
                        // Capture the state before tick 181, i.e. after exactly 180 ticks.
                        // A low render rate may execute more ticks in the final frame.
                        if (tick > 181) return;
                        history.Add(Capture(rig));
                        if (tick == 1 || tick == 110)
                            Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Attack), Is.True);
                        if (tick == 20) Static("RequestHitstop", hitstopOwner, 6f / 60f);
                        if (tick == 70 || tick == 140)
                            Assert.That(Call<bool>(rig.Damage, "TryReceiveHit", 1, Vector2.left, 0f), Is.True);
                        if (tick == 71)
                            Assert.That(Call<bool>(rig.Damage, "TryReceiveHit", 1, Vector2.left, 0f), Is.False);
                        Field(rig.Motor, "moveInput", tick >= 80 && tick < 95 ? 0.4f : 0f);
                    };
                    AddTickCallback();
                    Property(controller, "ManualAdvanceOnly", false);
                    while (history.Count < 181 && Time.realtimeSinceStartupAsDouble - startTime < 15d)
                    {
                        coroutineFrames++;
                        yield return null;
                    }
                    Property(controller, "ManualAdvanceOnly", true);
                    RemoveTickCallback();
                    double elapsed = Time.realtimeSinceStartupAsDouble - startTime;
                    int renderedFrames = Time.frameCount - initialFrame;
                    Debug.Log($"[CombatClock RenderLoop] configuredCap={configuredFps}, " +
                        $"measuredFps={renderedFrames / elapsed:F2}, elapsedSeconds={elapsed:F3}, " +
                        $"renderedFrames={renderedFrames}, coroutineFrames={coroutineFrames}, " +
                        $"framesWithTicks={framesWithTicks.Count}, tickCallbacks={tickCallbacks}, " +
                        $"comparedSnapshots={history.Count}, vSyncCount={QualitySettings.vSyncCount}");
                    Assert.That(history.Count, Is.EqualTo(181),
                        $"Configured cap {configuredFps} did not reach 180 combat ticks within 15 seconds.");
                    Assert.That(renderedFrames, Is.GreaterThan(1));
                    Snapshot final = history[180];
                    Assert.That(final.EnemyHp, Is.EqualTo(48), "Actual Animator events must hit twice.");
                    Assert.That(final.PlayerHp, Is.EqualTo(3));
                    Assert.That(final.Hits, Is.EqualTo(2));
                    Assert.That(final.Attacks, Is.EqualTo(2));
                    Assert.That(final.Gauge, Is.EqualTo(24f).Within(0.0001f));
                    Assert.That(final.Position.x, Is.GreaterThan(0.05f));
                    Assert.That(final.Action, Is.EqualTo(PlayerActionState.Neutral));
                    Assert.That(Get<int>(rig.Attack, "TimeoutFallbackCount"), Is.Zero);
                    if (reference == null) reference = history;
                    else
                        for (int index = 0; index < history.Count; index++)
                            AssertSnapshot(reference[index], history[index],
                                $"actual loop configured cap {configuredFps}, tick {index}");
                    DestroyRig(rig);
                }
            }
            finally
            {
                RemoveTickCallback();
                if (controller != null) Property(controller, "ManualAdvanceOnly", savedManualAdvance);
                Application.targetFrameRate = savedTargetFrameRate;
                QualitySettings.vSyncCount = savedVSyncCount;
            }
        }

        [UnityTest]
        public IEnumerator AttackComboReservationFreezesDuringPauseAndIsDiscardedOnResume()
        {
            Rig rig = CreateRig(false);
            yield return null;
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.True);
            Advance(1d / 60d);
            Assert.That(Get<int>(rig.Attack, "ComboStep"), Is.EqualTo(1));
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.True);
            float reserved = Get<float>(rig.Attack, "InputBufferRemaining");
            float lockRemaining = Get<float>(rig.Attack, "StepTimeoutRemaining");
            Assert.That(reserved, Is.EqualTo(6f / 60f).Within(0.000001f));
            Call<bool>(rig.State, "RequestPause");
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.False);
            Advance(1d);
            Assert.That(Get<float>(rig.Attack, "InputBufferRemaining"), Is.EqualTo(reserved));
            Assert.That(Get<float>(rig.Attack, "StepTimeoutRemaining"), Is.EqualTo(lockRemaining));
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.False);
            Call<bool>(rig.State, "RequestPause");
            Assert.That(Get<float>(rig.Attack, "InputBufferRemaining"), Is.Zero);
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.False);
            yield return null;
            Call(rig.Attack, "OnComboWindowOpen", 1);
            Advance(1d / 60d);
            Call(rig.Attack, "OnAttackEnd", 1);
            Assert.That(Get<int>(rig.Attack, "ComboStep"), Is.Zero);
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(rig.State, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
        }

        [UnityTest]
        public IEnumerator AttackComboInputCollectedDuringHitstopQueuesOnlyOnceOnFirstResumedTick()
        {
            Rig rig = CreateRig(false);
            yield return null;
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.True);
            Advance(1d / 60d);
            Call(rig.Attack, "OnComboWindowOpen", 1);
            Static("RequestHitstop", rig.Attack, 6f / 60f);
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.True);
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.True);
            float reserved = Get<float>(rig.Attack, "InputBufferRemaining");
            float lockRemaining = Get<float>(rig.Attack, "StepTimeoutRemaining");
            Assert.That(reserved, Is.EqualTo(6f / 60f).Within(0.000001f));
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.False,
                "Sampling during hitstop must not consume the combo buffer.");
            Advance(5d / 60d);
            Assert.That(Get<float>(rig.Attack, "InputBufferRemaining"), Is.EqualTo(reserved));
            Assert.That(Get<float>(rig.Attack, "StepTimeoutRemaining"), Is.EqualTo(lockRemaining));
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.False);
            Advance(2d / 60d);
            Assert.That(Get<float>(rig.Attack, "InputBufferRemaining"), Is.Zero);
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.True);
            Assert.That(Get<int>(rig.Attack, "ComboStep"), Is.EqualTo(1));
            Call(rig.Attack, "OnAttackEnd", 1);
            Assert.That(Get<int>(rig.Attack, "ComboStep"), Is.EqualTo(2));
            Assert.That(Get<bool>(rig.Attack, "HasQueuedAttack"), Is.False);
            Call(rig.Attack, "OnComboWindowOpen", 2);
            Advance(1d / 60d);
            Call(rig.Attack, "OnAttackEnd", 2);
            Assert.That(Get<int>(rig.Attack, "ComboStep"), Is.Zero,
                "Repeated input during one hitstop must not leak a third combo step.");
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(rig.State, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
        }

        [UnityTest]
        public IEnumerator HitstopCollectsInputWithoutAgingItAndResolvesOnlyOneAction()
        {
            Rig rig = CreateRig(false);
            yield return null;
            object owner = new object();
            Static("RequestHitstop", owner, 8f / 60f);
            Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Attack | PlayerActionRequest.Heal), Is.True);
            int frames = Call<int>(rig.State, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack);
            long ticks = GetStatic<long>("TickCount");
            Advance(7d / 60d);
            Assert.That(GetStatic<long>("TickCount"), Is.EqualTo(ticks));
            Assert.That(Call<int>(rig.State, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack), Is.EqualTo(frames));
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.Zero);
            Advance(2d / 60d);
            Assert.That(Get<PlayerActionState>(rig.State, "ActionState"), Is.EqualTo(PlayerActionState.Attack));
            Assert.That(Get<PlayerActionRequest>(rig.State, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.EqualTo(1));
            Advance(1d);
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(rig.State, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
        }

        [UnityTest]
        public IEnumerator PauseFreezesExistingReservationRejectsNewInputAndDiscardsReservationOnResume()
        {
            Rig rig = CreateRig(false);
            yield return null;
            Static("RequestHitstop", rig.Attack, 4f / 60f);
            Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Attack), Is.True);
            int remaining = Call<int>(rig.State, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack);
            Assert.That(Call<bool>(rig.State, "RequestPause"), Is.True);
            Assert.That(Get<PlayerActionRequest>(rig.State, "PendingRequests"), Is.EqualTo(PlayerActionRequest.Attack));
            Assert.That(Call<bool>(rig.State, "RequestAction", PlayerActionRequest.Gameplay), Is.False);
            Assert.That(Call<bool>(rig.Attack, "RequestAttack"), Is.False);
            Static("ReleaseOwner", rig.Attack);
            Advance(10d);
            Assert.That(GetStatic<bool>("IsPaused"), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.Zero);
            Assert.That(Call<int>(rig.State, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack), Is.EqualTo(remaining));
            Call<bool>(rig.State, "RequestPause");
            Assert.That(Call<int>(rig.State, "GetInputBufferFramesRemaining", PlayerActionRequest.Attack), Is.Zero);
            yield return null;
            Advance(1d / 60d);
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.Zero, "Resume must discard the pre-pause reservation.");
            Assert.That(Get<PlayerActionRequest>(rig.State, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None));
            Advance(1d);
            Assert.That(Get<int>(rig.Attack, "ComboAttemptCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableAndDeathEachReleaseTimeAndActionOwnershipTenTimes()
        {
            Rig rig = CreateRig(false);
            yield return null;
            for (int iteration = 0; iteration < 10; iteration++)
            {
                SeedLocks(rig);
                rig.Player.SetActive(false);
                AssertClean(rig, $"disable {iteration}");
                rig.Player.SetActive(true);
                SeedLocks(rig);
                Call(rig.State, "SetDead", "CombatClockDeathStress");
                Assert.That(Get<PlayerLifeState>(rig.State, "LifeState"), Is.EqualTo(PlayerLifeState.Dead));
                AssertClean(rig, $"death {iteration}");
                Call(rig.State, "Revive", "CombatClockDeathStress");
            }
        }

        [UnityTest]
        public IEnumerator SceneTransitionsReleaseTimeAndPlayerLocksTenTimes()
        {
            for (int iteration = 0; iteration < 10; iteration++)
            {
                Scene scene = SceneManager.CreateScene($"CombatClockStress_{iteration}");
                scenes.Add(scene);
                Rig rig = CreateRig(false);
                SceneManager.MoveGameObjectToScene(rig.Player, scene);
                SceneManager.MoveGameObjectToScene(rig.Enemy.gameObject, scene);
                SceneManager.SetActiveScene(scene);
                SeedLocks(rig);
                SceneManager.SetActiveScene(originalScene);
                AssertClean(rig, $"scene transition {iteration}");
                yield return SceneManager.UnloadSceneAsync(scene);
                Assert.That(GetStatic<int>("HitstopOwnerCount"), Is.Zero);
                Assert.That(GetStatic<bool>("IsPaused"), Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f));
            }
        }

        [UnityTest]
        public IEnumerator FrameQuantizedTimersExpireOnTheirExactThreeAndThirtySecondBoundaries()
        {
            foreach (int seconds in new[] { 3, 30 })
            {
                float remaining = seconds;
                for (int tick = 1; tick < seconds * 60; tick++)
                {
                    remaining = (float)Static("AdvanceTimer", remaining);
                    Assert.That(remaining, Is.GreaterThan(0f), $"{seconds}s timer ended at tick {tick}");
                }
                remaining = (float)Static("AdvanceTimer", remaining);
                Assert.That(remaining, Is.Zero, $"{seconds}s timer must end at tick {seconds * 60}");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingClockRestoresPreviousGlobalTimeAndPhysicsSettingsTenTimes()
        {
            controller.enabled = false;
            float savedScale = Time.timeScale, savedStep = Time.fixedDeltaTime;
            SimulationMode2D savedMode = Physics2D.simulationMode;
            try
            {
                for (int iteration = 0; iteration < 10; iteration++)
                {
                    Time.timeScale = 0.75f;
                    Time.fixedDeltaTime = 0.025f;
                    Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                    controller.enabled = true;
                    Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
                    Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f));
                    Static("RequestHitstop", this, 1f);
                    Static("SetPaused", true);
                    controller.enabled = false;
                    Assert.That(Time.timeScale, Is.EqualTo(0.75f), $"disable {iteration}");
                    Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.025f));
                    Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.FixedUpdate));
                    Assert.That(GetStatic<int>("HitstopOwnerCount"), Is.Zero);
                    Assert.That(GetStatic<bool>("IsPaused"), Is.False);
                }
            }
            finally
            {
                Time.timeScale = savedScale;
                Time.fixedDeltaTime = savedStep;
                Physics2D.simulationMode = savedMode;
                controller.enabled = true;
            }
            yield return null;
        }

        private Rig CreateRig(bool withAnimator)
        {
            var rig = new Rig { Player = NewObject("CombatClockTestPlayer") };
            rig.Player.SetActive(false);
            rig.Body = rig.Player.AddComponent<Rigidbody2D>();
            rig.Body.gravityScale = 0f;
            rig.Player.AddComponent<BoxCollider2D>().isTrigger = true;
            rig.State = rig.Player.AddComponent(RuntimeType("PlayerStateMachine"));
            rig.Gauge = rig.Player.AddComponent(RuntimeType("PlayerSpecialGauge"));
            rig.Motor = rig.Player.AddComponent(RuntimeType("PlayerMotor2D"));
            rig.Damage = rig.Player.AddComponent(RuntimeType("PlayerDamageReceiver2D"));
            rig.Attack = rig.Player.AddComponent(RuntimeType("PlayerAttack2D"));
            Field(rig.Motor, "moveSpeed", 1f);
            Field(rig.Motor, "applyRecommendedPhysicsSettings", false);
            Field(rig.Damage, "minKnockbackForce", 0f);
            Field(rig.Attack, "hitPoint", rig.Player.transform);
            Field(rig.Attack, "targetLayers", (LayerMask)(1 << 30));
            if (withAnimator)
            {
#if UNITY_EDITOR
                rig.Player.AddComponent<SpriteRenderer>();
                rig.Animator = rig.Player.AddComponent<Animator>();
                rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.Animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Animations/Player/PlayerAnimator.controller");
                Assert.That(rig.Animator.runtimeAnimatorController, Is.Not.Null);
#else
                Assert.Ignore("The existing project Animator fixture is loaded through AssetDatabase in Editor PlayMode.");
#endif
            }
            GameObject enemy = NewObject("CombatClockTestEnemy");
            enemy.SetActive(false);
            enemy.layer = 30;
            enemy.transform.position = new Vector3(0.5f, 0f, 0f);
            enemy.AddComponent<BoxCollider2D>().isTrigger = true;
            rig.Enemy = enemy.AddComponent(RuntimeType("Damageable2D"));
            Field(rig.Enemy, "maxHp", 50);
            Field(rig.Enemy, "knockbackPower", 0f);
            Field(rig.Enemy, "applyPresetOnAwake", false);
            enemy.SetActive(true);
            rig.Player.SetActive(true);
            return rig;
        }

        private void SeedLocks(Rig rig)
        {
            Static("SetPaused", false);
            Call(rig.State, "ForceHit", "CombatClockStress");
            Collider2D own = rig.Player.GetComponent<Collider2D>();
            Collider2D other = rig.Enemy.GetComponent<Collider2D>();
            Call(rig.State, "AcquireCollisionIgnore", PlayerActionState.Hit, own, other);
            Static("RequestHitstop", rig.State, 1f);
            Static("SetPaused", true);
            Assert.That(Get<int>(rig.State, "ActiveActionOwnerCount"), Is.EqualTo(1));
            Assert.That(Get<int>(rig.State, "ActiveCollisionIgnoreCount"), Is.EqualTo(1));
        }

        private void AssertClean(Rig rig, string context)
        {
            Assert.That(GetStatic<bool>("IsPaused"), Is.False, context);
            Assert.That(GetStatic<int>("HitstopOwnerCount"), Is.Zero, context);
            Assert.That(Time.timeScale, Is.EqualTo(1f), context);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 60f), context);
            Assert.That(Get<int>(rig.State, "ActiveActionOwnerCount"), Is.Zero, context);
            Assert.That(Get<int>(rig.State, "ActiveCollisionIgnoreCount"), Is.Zero, context);
            Assert.That(Get<PlayerActionRequest>(rig.State, "PendingRequests"), Is.EqualTo(PlayerActionRequest.None), context);
            Assert.That(Get<float>(rig.State, "RemainingLock"), Is.Zero, context);
            Assert.That(Physics2D.GetIgnoreCollision(rig.Player.GetComponent<Collider2D>(), rig.Enemy.GetComponent<Collider2D>()), Is.False, context);
        }

        private static Snapshot Capture(Rig rig)
        {
            AnimatorStateInfo animation = rig.Animator != null ? rig.Animator.GetCurrentAnimatorStateInfo(0) : default;
            return new Snapshot
            {
                Action = Get<PlayerActionState>(rig.State, "ActionState"),
                Life = Get<PlayerLifeState>(rig.State, "LifeState"),
                PlayerHp = Get<int>(rig.Damage, "CurrentHp"), EnemyHp = Get<int>(rig.Enemy, "CurrentHp"),
                Hits = Get<int>(rig.Damage, "TotalHitsTaken"), Attacks = Get<int>(rig.Attack, "ComboAttemptCount"),
                Gauge = Get<float>(rig.Gauge, "CurrentGauge"), Lock = Get<float>(rig.State, "RemainingLock"),
                Invincible = (float)rig.Damage.GetType().GetField("invincibleTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig.Damage),
                Break = Get<float>(rig.Enemy, "CurrentBreak"), Position = rig.Body.position,
                EnemyPosition = rig.Enemy.transform.position, AnimationTime = animation.normalizedTime,
                AnimationState = animation.fullPathHash
            };
        }

        private static void AssertSnapshot(Snapshot expected, Snapshot actual, string context)
        {
            Assert.That(actual.Action, Is.EqualTo(expected.Action), context);
            Assert.That(actual.Life, Is.EqualTo(expected.Life), context);
            Assert.That(actual.PlayerHp, Is.EqualTo(expected.PlayerHp), context);
            Assert.That(actual.EnemyHp, Is.EqualTo(expected.EnemyHp), context);
            Assert.That(actual.Hits, Is.EqualTo(expected.Hits), context);
            Assert.That(actual.Attacks, Is.EqualTo(expected.Attacks), context);
            Assert.That(actual.Gauge, Is.EqualTo(expected.Gauge).Within(0.0001f), context);
            Assert.That(actual.Lock, Is.EqualTo(expected.Lock).Within(0.0001f), context);
            Assert.That(actual.Invincible, Is.EqualTo(expected.Invincible).Within(0.0001f), context);
            Assert.That(actual.Break, Is.EqualTo(expected.Break).Within(0.0001f), context);
            Assert.That(Vector2.Distance(actual.Position, expected.Position), Is.LessThan(0.0001f), context);
            Assert.That(Vector2.Distance(actual.EnemyPosition, expected.EnemyPosition), Is.LessThan(0.0001f), context);
            Assert.That(actual.AnimationState, Is.EqualTo(expected.AnimationState), context);
            Assert.That(actual.AnimationTime, Is.EqualTo(expected.AnimationTime).Within(0.0001f), context);
        }

        private GameObject NewObject(string name)
        {
            var item = new GameObject(name);
            objects.Add(item);
            return item;
        }
        private void DestroyRig(Rig rig)
        {
            Object.DestroyImmediate(rig.Player);
            Object.DestroyImmediate(rig.Enemy.gameObject);
        }
        private void Advance(double seconds) => Call(controller, "AdvanceFrame", seconds);
        private void AddTickCallback() => timeType.GetEvent("BeforeCombatTick").AddEventHandler(null, tickCallback);
        private void RemoveTickCallback()
        {
            if (tickCallback == null) return;
            timeType.GetEvent("BeforeCombatTick").RemoveEventHandler(null, tickCallback);
            tickCallback = null;
        }
        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private object Static(string name, params object[] args) => timeType.GetMethod(name, Public).Invoke(null, args);
        private T GetStatic<T>(string name) => (T)timeType.GetProperty(name, Public).GetValue(null);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name, Public).GetValue(target);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name, Public).SetValue(target, value);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Public).Invoke(target, args);
        private static T Call<T>(object target, string name, params object[] args) => (T)Call(target, name, args);
    }
}
