using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeQuickHeavyTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component clock, player, counter, parry, presentation, arena, motor, feedback;
        private Rigidbody2D body;
        private bool manual;
        private readonly List<GameObject> owned = new();
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        private static V Get<V>(object target, string name) => (V)(target.GetType().GetProperty(name, Flags)?.GetValue(target) ?? target.GetType().GetField(name, Flags).GetValue(target));
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Pause(bool paused) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { paused });
        private static V Time<V>(string property) => (V)T("CombatTimeController").GetProperty(property).GetValue(null);
        private void Frames(int count, int fps = 60)
        {
            for (int i = 0; i < count; i++)
            {
                Call(clock, "AdvanceFrame", 1d / fps);
                Call(presentation, "AdvanceVisuals", 1f / fps); Call(feedback, "AdvanceVisuals", 1f / fps);
            }
        }
        private void Until(Func<bool> condition, int maximum = 300, int fps = 60)
        {
            while (!condition() && maximum-- > 0) Frames(1, fps);
            Assert.That(condition(), Is.True, "Timed out: " + Get<string>(counter, "CinematicPhase"));
        }
        private Component Enemy(string name, Vector2 position, int hp = 1000)
        {
            var root = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == name);
            var health = root.GetComponent(T("Damageable2D"));
            Call(health, "ResetForCapture", (Vector3)position, hp);
            Call(root.GetComponent(T("EnemyMeleeChaser2D")), "ResetForCapture", player.transform, true);
            Physics2D.SyncTransforms(); return health;
        }
        private void Place(int direction = 1, bool airborne = false)
        {
            body.position = new Vector2(0, airborne ? 4.8f : .81f); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            Call(motor, "SetMoveInput", Vector2.zero); Call(motor, "SetFacing", direction); Physics2D.SyncTransforms();
        }
        private Component Begin(int direction = 1, bool airborne = false)
        {
            Place(direction, airborne);
            var enemy = Enemy("StrongMelee", body.position + Vector2.right * direction * 1.2f);
            Call(counter, "RegisterParriedEnemy", enemy);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Frames(1); Assert.That(Get<bool>(counter, "IsActive"), Is.True); return enemy;
        }
        private void HeavyParry(Component target, int inputFrame = 1)
        {
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True); Frames(1);
            Until(() => Get<int>(parry, "ElapsedFrames") >= inputFrame);
            object context = Activator.CreateInstance(T("CombatHitContext2D"), target, 1917L, true, Enum.Parse(T("ParryReactionWeight"), "Heavy"));
            Assert.That((bool)Call(player.GetComponent(T("PlayerDamageReceiver2D")), "TryReceiveContextHit", 10, (Vector2)target.transform.position, 0f, context), Is.False);
            Assert.That(Get<object>(parry, "LastReactionWeight").ToString(), Is.EqualTo("Heavy"));
            Assert.That(Get<bool>(presentation, "IsParrySequenceActive"), Is.True);
            Until(() => !Get<bool>(presentation, "IsParryImpactPending"));
        }
        private GameObject Wall(float x)
        {
            var wall = new GameObject("QuickHeavyTestWall"); owned.Add(wall); wall.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = new Vector3(x, 3); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 8);
            Physics2D.SyncTransforms(); return wall;
        }
        private void Clean()
        {
            Assert.That(Get<bool>(counter, "IsActive"), Is.False);
            Assert.That(Get<bool>(counter, "IsCounterInvincible"), Is.False);
            Assert.That(Get<bool>(presentation, "IsParrySequenceActive"), Is.False);
            Assert.That(Get<bool>(presentation, "IsBodyHidden"), Is.False);
            Assert.That(Get<float>(presentation, "DimAlpha"), Is.Zero);
            Assert.That(Get<bool>(feedback, "HasCameraFocus"), Is.False);
            Assert.That(Time<int>("SlowOwnerCount"), Is.Zero);
            Assert.That(body.gravityScale, Is.GreaterThan(0));
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            counter = player.GetComponent(T("PlayerParryCounter2D")); parry = player.GetComponent(T("PlayerParry2D"));
            motor = player.GetComponent(T("PlayerMotor2D")); presentation = player.GetComponent(T("PrototypeCounterPresentation"));
            feedback = player.GetComponent(T("CombatFeedback2D")); arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            body = player.GetComponent<Rigidbody2D>(); ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            Set(Get<object>(feedback, "Settings"), "masterVolume", 0f); Set(Get<object>(feedback, "Settings"), "shake", 0f);
            Set(Get<object>(feedback, "Settings"), "actorJitter", 0f);
            Frames(2); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            Pause(false);
            if (player != null && !player.gameObject.activeSelf) player.gameObject.SetActive(true);
            if (arena != null) Call(arena, "ResetActionsOnly");
            foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear();
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual); yield return null;
        }

        [TestCase(1, false)] [TestCase(-1, false)] [TestCase(1, true)] [TestCase(-1, true)]
        public void QuickPassThroughHasExactlyOneSheathingHitAndReturnsControl(int direction, bool airborne)
        {
            Assert.That(Get<bool>(counter, "UsesQuickCounter"), Is.True);
            var target = Begin(direction, airborne); int hp = Get<int>(target, "CurrentHp");
            Until(() => Get<int>(counter, "ElapsedFrames") == Get<int>(counter, "HitFrame") - 1);
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp), "Crossing the enemy must not cause damage.");
            Assert.That((body.position.x - target.transform.position.x) * direction, Is.GreaterThan(.9f));
            Frames(1);
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp - Get<int>(counter, "SelectedDamage")));
            Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Get<Sprite>(Get<object>(counter, "QuickProfile"), "ContactSprite")));
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Assert.That(Time<float>("SlowScale"), Is.EqualTo(1));
            Assert.That(Get<float>(presentation, "DimAlpha"), Is.Zero);
            Until(() => !Get<bool>(counter, "IsActive")); Frames(20);
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Clean();
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True); Frames(1);
            Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.Attack));
        }
        [Test] public void SeparateInputsConsumeSeparateTargetsAfterCrossingWithoutCollateralHits()
        {
            Place(); var a = Enemy("StrongMelee", new Vector2(1.1f, .81f)); var b = Enemy("Melee_1", new Vector2(1.4f, .81f));
            Call(counter, "RegisterParriedEnemy", a); Call(counter, "RegisterParriedEnemy", b);
            int bHp = Get<int>(b, "CurrentHp");
            Call(player, "RequestAction", PlayerActionRequest.Attack); Frames(1);
            Until(() => Get<int>(counter, "ElapsedFrames") >= Get<int>(Get<object>(counter, "QuickProfile"), "EndFrame") - 5);
            Assert.That(Get<int>(b, "CurrentHp"), Is.EqualTo(bHp));
            Call(player, "RequestAction", PlayerActionRequest.Attack);
            Until(() => Get<int>(counter, "ConsumedCount") == 2);
            Assert.That(Get<object>(counter, "SelectedTarget"), Is.SameAs(b));
            Until(() => !Get<bool>(counter, "IsActive")); Frames(10);
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(2)); Assert.That(Get<int>(counter, "StockCount"), Is.Zero); Clean();
        }
        [TestCase(1)] [TestCase(-1)] public void BlockedFarSideUsesSafeNearSide(int direction)
        {
            Place(direction); var target = Enemy("StrongMelee", new Vector2(direction * 1.2f, .81f)); Wall(direction * 2);
            Call(counter, "RegisterParriedEnemy", target); Call(player, "RequestAction", PlayerActionRequest.Attack); Frames(1);
            Assert.That(Get<bool>(counter, "UsesFrontApproach"), Is.True);
            Until(() => !Get<bool>(counter, "IsActive"));
            Assert.That(Mathf.Abs(body.position.x), Is.LessThan(.1f)); Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Clean();
        }
        [TestCase("Expired")] [TestCase("Range")] [TestCase("Wall")]
        public void InvalidTargetDoesNotConsumeCounter(string failure)
        {
            Place(); var target = Enemy("StrongMelee", new Vector2(1.2f, .81f)); Call(counter, "RegisterParriedEnemy", target);
            if (failure == "Expired") Frames(181);
            if (failure == "Range") { target.transform.position += Vector3.right * 8; Physics2D.SyncTransforms(); }
            if (failure == "Wall") Wall(.6f);
            Call(player, "RequestAction", PlayerActionRequest.Attack); Frames(1);
            Assert.That(Get<bool>(counter, "IsActive"), Is.False); Assert.That(Get<int>(counter, "ConsumedCount"), Is.Zero);
        }
        [TestCase("Reset")] [TestCase("TargetLost")] [TestCase("Disable")]
        public void QuickInterruptionReleasesGravityTargetLeaseAndVisibility(string interruption)
        {
            var target = Begin(); Until(() => Get<int>(counter, "ElapsedFrames") >= 5);
            if (interruption == "Reset") Call(arena, "ResetActionsOnly");
            else if (interruption == "TargetLost") { target.gameObject.SetActive(false); Frames(1); }
            else { player.gameObject.SetActive(false); player.gameObject.SetActive(true); }
            Frames(20); Clean(); Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero);
        }
        [Test] public void QuickPauseFreezesCrossingAndHitUntilResume()
        {
            var target = Begin(); Until(() => Get<int>(counter, "ElapsedFrames") == 6);
            Vector2 position = body.position; int hp = Get<int>(target, "CurrentHp");
            Pause(true); Frames(90);
            Assert.That(body.position, Is.EqualTo(position)); Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
            Pause(false); Until(() => !Get<bool>(counter, "IsActive")); Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Clean();
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)] public void QuickOutcomeIsStableAcrossRenderRates(int fps)
        {
            var target = Begin(); Until(() => !Get<bool>(counter, "IsActive"), fps: fps);
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(910));
            Assert.That(body.position.x, Is.EqualTo(2.4f).Within(.03f)); Clean();
        }
        [TestCase(1, false)] [TestCase(-1, false)] [TestCase(1, true)] [TestCase(-1, true)]
        public void HeavyParryHoldsContactDuringStopThenRecoilsAndUsesSameQuickCounter(int direction, bool airborne)
        {
            Place(direction, airborne); var target = Enemy("StrongMelee", body.position + Vector2.right * direction * 2.3f);
            HeavyParry(target); Vector2 origin = body.position;
            Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Get<Sprite>(Get<object>(presentation, "HeavyProfile"), "ContactSprite")));
            Frames(20); Assert.That(body.position.x, Is.EqualTo(origin.x).Within(.001f), "The recoil must wait until hitstop ends.");
            Until(() => !Get<bool>(presentation, "IsParrySequenceActive"));
            Assert.That((origin.x - body.position.x) * direction, Is.InRange(1.2f, 1.3f));
            Assert.That((string)Call(counter, "EligibilityReason", target), Is.EqualTo("Eligible"), "Recoil alone cannot invalidate the reward.");
            Call(player, "RequestAction", PlayerActionRequest.Attack); Frames(1);
            Assert.That(Get<bool>(counter, "UsesQuickCounter"), Is.True); Assert.That(Get<object>(counter, "SelectedTarget"), Is.SameAs(target));
            Until(() => !Get<bool>(counter, "IsActive")); Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Clean();
        }
        [TestCase(1)] [TestCase(-1)] public void HeavyRecoilStopsAtWallWithoutFloorPenetration(int direction)
        {
            Place(direction); var target = Enemy("StrongMelee", new Vector2(direction * 1.2f, .81f)); Wall(-direction * .5f);
            HeavyParry(target); Until(() => !Get<bool>(presentation, "IsParrySequenceActive"));
            Assert.That(-body.position.x * direction, Is.InRange(0, .09f)); Assert.That(body.position.y, Is.GreaterThan(.77f));
            Call(arena, "ResetActionsOnly"); Clean();
        }
        [Test] public void HeavyPauseAndResetDoNotLeaveQueuedAttackOrRecoil()
        {
            Place(); var target = Enemy("StrongMelee", new Vector2(1.2f, .81f)); HeavyParry(target);
            Call(player, "RequestAction", PlayerActionRequest.Attack); Pause(true); Vector2 position = body.position; Frames(90);
            Assert.That(body.position, Is.EqualTo(position));
            Call(arena, "ResetActionsOnly"); Pause(false); Frames(40); Clean();
            Assert.That(Get<int>(counter, "StockCount"), Is.Zero); Assert.That(Get<bool>(presentation, "HasQueuedParryCounter"), Is.False);
        }
        [TestCase("TargetLost")] [TestCase("Disable")] [TestCase("Hit")]
        public void AirborneHeavyInterruptionAlwaysRestoresGravity(string interruption)
        {
            Place(1, true); float gravity = body.gravityScale;
            var target = Enemy("StrongMelee", body.position + Vector2.right * 1.2f); HeavyParry(target);
            Assert.That(body.gravityScale, Is.Zero);
            if (interruption == "TargetLost") target.gameObject.SetActive(false);
            else if (interruption == "Disable") { player.gameObject.SetActive(false); player.gameObject.SetActive(true); }
            else Assert.That((bool)Call(player, "ForceHit", "HeavyInterruptionTest"), Is.True);
            Frames(1);
            Assert.That(body.gravityScale, Is.EqualTo(gravity));
            Assert.That(Get<bool>(presentation, "IsParrySequenceActive"), Is.False);
            Assert.That(Get<bool>(presentation, "HasQueuedParryCounter"), Is.False);
            Call(arena, "ResetActionsOnly"); Clean();
        }
        [Test] public void PresetFiveUsesHeavyOnlyInfiniteEnemyAndFourRestoresLight()
        {
            Call(arena, "ResetTrainingEncounter", true);
            var dummy = Get<Component>(arena, "trainingDummy"); var health = dummy.GetComponent(T("Damageable2D"));
            Assert.That(Get<bool>(health, "HasInfiniteHealth"), Is.True);
            Assert.That((bool)Call(dummy, "BeginCaptureAttack"), Is.True);
            Assert.That(Get<object>(dummy, "AttackParryReaction").ToString(), Is.EqualTo("Heavy"));
            Assert.That(Get<string>(dummy, "AttackId"), Is.EqualTo("training-heavy-parry"));
            Assert.That(Object.FindObjectsByType(T("EnemyMeleeChaser2D"), FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Call(arena, "ResetTrainingEncounter", false);
            Assert.That((bool)Call(dummy, "BeginCaptureAttack"), Is.True);
            Assert.That(Get<object>(dummy, "AttackParryReaction").ToString(), Is.EqualTo("Light"));
        }
        [Test] public void LegacyKickRemainsAComparisonAndSwitchingRequiresNeutral()
        {
            Assert.That((bool)Call(counter, "TrySetQuickCounter", false), Is.True);
            Assert.That(Get<int>(counter, "HitFrame"), Is.EqualTo(21)); Assert.That(Get<bool>(counter, "IsAirborneKick"), Is.True);
            Assert.That((bool)Call(counter, "TrySetQuickCounter", true), Is.True);
            Begin(); Assert.That((bool)Call(counter, "TrySetQuickCounter", false), Is.False);
            Call(arena, "ResetActionsOnly"); Assert.That((bool)Call(counter, "TrySetQuickCounter", false), Is.True);
        }
        [TestCase(1, "Just")] [TestCase(10, "Normal")]
        public void HeavyWeightIsIndependentOfInputPrecision(int inputFrame, string expected)
        {
            Place(); var target = Enemy("StrongMelee", new Vector2(1.2f, .81f)); HeavyParry(target, inputFrame);
            Assert.That(Get<object>(parry, "LastParryResult").ToString(), Is.EqualTo(expected));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Call(player, "RequestAction", PlayerActionRequest.Attack);
            Until(() => Get<bool>(counter, "IsActive")); Until(() => !Get<bool>(counter, "IsActive"));
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1)); Clean();
        }
        [Test] public void QuickAudioUsesOneSheatheAndSlashWithoutKickChargeOrLanding()
        {
            var sfx = player.GetComponent(T("PrototypeSfx2D"));
            Set(Get<object>(feedback, "Settings"), "masterVolume", 1f);
            Set(Get<object>(feedback, "Settings"), "sfxVolume", 1f);
            object Cue(string name) => Enum.Parse(T("PrototypeSfxCue"), name);
            int Count(string name) => (int)Call(sfx, "Count", Cue(name));
            float volume = AudioListener.volume; AudioListener.volume = 0;
            try
            {
                var target = Begin(); int hp = Get<int>(target, "CurrentHp");
                int sheathFrame = Get<int>(Get<object>(counter, "QuickProfile"), "sheatheFrame");
                Until(() => Get<int>(counter, "ElapsedFrames") == sheathFrame - 1);
                Assert.That(Count("Sheathe"), Is.Zero); Assert.That(Count("HeavyHit"), Is.Zero);
                Frames(1);
                Assert.That(Count("Sheathe"), Is.EqualTo(1)); Assert.That(Count("HeavyHit"), Is.Zero);
                Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
                Until(() => Get<int>(counter, "ElapsedFrames") == Get<int>(counter, "HitFrame") - 1);
                Assert.That(Count("HeavyHit"), Is.Zero); Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
                Frames(1); Assert.That(Count("HeavyHit"), Is.EqualTo(1));
                Until(() => !Get<bool>(counter, "IsActive"));
                Assert.That(Count("Sheathe"), Is.EqualTo(1)); Assert.That(Count("HeavyHit"), Is.EqualTo(1));
                Assert.That(Count("CounterCharge"), Is.Zero); Assert.That(Count("CounterHit"), Is.Zero);
                Assert.That(Count("CounterRelease"), Is.Zero); Assert.That(Count("Land"), Is.Zero);
            }
            finally { AudioListener.volume = volume; }
        }
        [TestCase(1)] [TestCase(-1)] public void QuickHoldsFirstCelAfterCrossingThenWaitsAfterSheathing(int direction)
        {
            var target = Begin(direction); int hp = Get<int>(target, "CurrentHp");
            var profile = Get<object>(counter, "QuickProfile");
            Until(() => Get<int>(counter, "ElapsedFrames") == Get<int>(counter, "BlinkArrivalFrame"));
            var first = player.GetComponent<SpriteRenderer>().sprite; Vector2 position = body.position;
            for (int i = 0; i < 9; i++)
            {
                Frames(1); Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.SameAs(first));
                Assert.That(Vector2.Distance(body.position, position), Is.LessThan(.001f)); Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
            }
            Frames(1); Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.Not.SameAs(first));
            Until(() => Get<int>(counter, "ElapsedFrames") == Get<int>(profile, "sheatheFrame"));
            Assert.That(Get<int>(counter, "HitFrame") - Get<int>(counter, "ElapsedFrames"), Is.GreaterThanOrEqualTo(8));
            Until(() => Get<int>(counter, "ElapsedFrames") == Get<int>(counter, "HitFrame") - 1);
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp)); Frames(1);
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1));
            Until(() => !Get<bool>(counter, "IsActive")); Frames(20); Clean();
        }
        [TestCase(1)] [TestCase(-1)] public void HeavyReturnsDirectlyToIdleWithoutLightParryRecovery(int direction)
        {
            Place(direction); var target = Enemy("StrongMelee", new Vector2(direction * 1.2f, .81f)); HeavyParry(target);
            var animator = player.GetComponent<Animator>();
            Until(() => !Get<bool>(presentation, "IsParrySequenceActive"));
            for (int i = 0; i < 32; i++)
            {
                Frames(1); var pose = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(pose.IsName("PlayerPrototype_Recovery_Parry") || pose.IsName("PlayerPrototype_ParrySuccessLight"), Is.False);
            }
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("PlayerPrototype_Idle"), Is.True); Clean();
        }
        [TestCase(1, 30)] [TestCase(-1, 60)] [TestCase(1, 120)]
        public void HeavyHitstopJittersBothDrawingsWithCameraOffThenReleases(int direction, int fps)
        {
            Place(direction); Set(Get<object>(feedback, "Settings"), "actorJitter", 1f);
            var target = Enemy("Melee_1", new Vector2(direction * 1.2f, .81f)); HeavyParry(target);
            var art = player.GetComponent(T("PrototypePlayerArtPresentation")); var enemyArt = target.GetComponent(T("PrototypeEnemyArtPresentation"));
            Vector2 playerPosition = body.position, enemyPosition = target.transform.position;
            bool positive = false, negative = false; int remaining = fps;
            while (Time<bool>("IsHitStopped") && remaining-- > 0)
            {
                float offset = Get<float>(art, "ImpactOffsetX"); positive |= offset > 0; negative |= offset < 0;
                Assert.That(offset + Get<float>(enemyArt, "ImpactOffsetX"), Is.Zero.Within(.00001f));
                Assert.That(body.position, Is.EqualTo(playerPosition)); Assert.That((Vector2)target.transform.position, Is.EqualTo(enemyPosition));
                Frames(1, fps);
            }
            Assert.That(positive && negative, Is.True);
            Assert.That(Get<float>(art, "ImpactOffsetX"), Is.Zero); Assert.That(Get<float>(enemyArt, "ImpactOffsetX"), Is.Zero);
            Assert.That(Get<bool>(presentation, "IsParryCameraActive"), Is.False);
            Until(() => !Get<bool>(presentation, "IsParrySequenceActive")); Frames(24); Clean();
        }
    }
}
