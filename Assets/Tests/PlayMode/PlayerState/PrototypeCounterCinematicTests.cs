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
    public sealed class PrototypeCounterCinematicTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, player, motor, counter, art, presentation, arena, feedback;
        private Rigidbody2D body;
        private bool savedManual;
        private int realFrames;
        private readonly List<GameObject> owned = new();
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name, Flags)?.GetValue(obj) ?? obj.GetType().GetField(name, Flags).GetValue(obj));
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        private static void Static(string name, params object[] args) => T("CombatTimeController").GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        private static V TimeValue<V>(string name) => (V)T("CombatTimeController").GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        private void Step()
        {
            Call(clock, "AdvanceFrame", 1d / 60d); realFrames++;
            if (presentation != null && ((Behaviour)presentation).isActiveAndEnabled) Call(presentation, "AdvanceVisuals", 1f / 60);
            if (feedback != null && ((Behaviour)feedback).isActiveAndEnabled) Call(feedback, "AdvanceVisuals", 1f / 60);
        }
        private void Frames(int n) { for (int i = 0; i < n; i++) Step(); }
        private void Until(Func<bool> predicate, int maximum = 360)
        {
            int remaining = maximum;
            while (!predicate() && --remaining > 0) Step();
            Assert.That(remaining, Is.GreaterThan(0), "Timed out: " + Get<string>(counter, "CinematicPhase") + " " + Get<int>(counter, "ElapsedFrames") + "F");
        }
        private void ToFrame(int frame) => Until(() => Get<int>(counter, "ElapsedFrames") >= frame || !Get<bool>(counter, "IsActive"));
        private Component Enemy(string name, Vector2 position, int hp)
        {
            var root = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == name);
            var health = root.GetComponent(T("Damageable2D"));
            Call(health, "ResetForCapture", (Vector3)position, hp);
            Call(root.GetComponent(T("EnemyMeleeChaser2D")), "ResetForCapture", player.transform, true);
            Physics2D.SyncTransforms(); return health;
        }
        private Component Begin(int direction = 1, bool airborne = false, int hp = 120)
        {
            body.position = new Vector2(0, airborne ? 4.8f : .81f); player.transform.position = body.position;
            body.linearVelocity = Vector2.zero; Call(motor, "SetFacing", direction); Call(motor, "SetMoveInput", Vector2.zero);
            var target = Enemy("StrongMelee", body.position + Vector2.right * direction * 1.15f, hp);
            Call(counter, "RegisterParriedEnemy", target);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Frames(1); Assert.That(Get<bool>(counter, "IsActive"), Is.True);
            return target;
        }
        private GameObject Wall(float x)
        {
            var wall = new GameObject("CounterTestWall"); owned.Add(wall);
            wall.layer = LayerMask.NameToLayer("Ground"); wall.transform.position = new Vector3(x, 3);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 8);
            Physics2D.SyncTransforms(); return wall;
        }
        private void AssertClean()
        {
            Assert.That(Get<bool>(counter, "IsActive"), Is.False);
            Assert.That(Get<bool>(counter, "IsCounterInvincible"), Is.False);
            Assert.That(Get<bool>(presentation, "IsCharged"), Is.False);
            Assert.That(Get<bool>(presentation, "IsBodyHidden"), Is.False);
            Assert.That(Get<float>(presentation, "DimAlpha"), Is.Zero.Within(.00001f));
            Assert.That(Get<bool>(presentation, "IsSpotlightVisible"), Is.False);
            Assert.That(TimeValue<int>("SlowOwnerCount"), Is.Zero);
            Assert.That(TimeValue<float>("SlowScale"), Is.EqualTo(1));
            Assert.That(body.gravityScale, Is.GreaterThan(0));
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(5.625f));
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            savedManual = Get<bool>(clock, "ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            motor = player.GetComponent(T("PlayerMotor2D")); counter = player.GetComponent(T("PlayerParryCounter2D"));
            art = player.GetComponent(T("PrototypePlayerArtPresentation")); presentation = player.GetComponent(T("PrototypeCounterPresentation"));
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena")); feedback = player.GetComponent(T("CombatFeedback2D"));
            body = player.GetComponent<Rigidbody2D>(); ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            Set(Get<object>(feedback, "Settings"), "masterVolume", 0f); Set(Get<object>(feedback, "Settings"), "shake", 0f);
            Frames(2); yield return null; realFrames = 0;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            Static("SetPaused", false);
            if (player != null && !player.gameObject.activeSelf) player.gameObject.SetActive(true);
            if (arena != null) Call(arena, "ResetActionsOnly");
            foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear();
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, savedManual); yield return null;
        }

        [TestCase(1, false)] [TestCase(-1, false)] [TestCase(1, true)] [TestCase(-1, true)]
        public void CounterUsesAuthoredPhasesSingleHitAndCollisionAwareLaunch(int direction, bool airborne)
        {
            var target = Begin(direction, airborne); var launch = target.GetComponent(T("EnemyCounterLaunch2D"));
            var profile = Get<object>(counter, "CinematicProfile"); float gravity = Get<float>(counter, "savedGravity");
            ToFrame(11); Assert.That(Get<bool>(presentation, "IsBodyHidden"), Is.True);
            ToFrame(12); Assert.That(Get<bool>(presentation, "IsBodyHidden"), Is.False);
            Assert.That((body.position.x - target.transform.position.x) * direction, Is.GreaterThan(1.7f));
            Assert.That(Get<int>(counter, "KickFacingSign"), Is.EqualTo(-direction));
            int chargeStart = realFrames; ToFrame(14);
            Assert.That(Get<float>(presentation, "DimAlpha"), Is.GreaterThan(.9f));
            Assert.That(Get<bool>(presentation, "IsSpotlightVisible"), Is.True);
            Assert.That(TimeValue<float>("SlowScale"), Is.EqualTo(.15f).Within(.00001f));
            var receiver = player.GetComponent(T("PlayerDamageReceiver2D"));
            Assert.That((bool)Call(receiver, "TryReceiveHit", 20, (Vector2)target.transform.position, 0f), Is.False);
            foreach (var action in new[] { PlayerActionRequest.Dash, PlayerActionRequest.Parry, PlayerActionRequest.Special })
                Call(player, "RequestAction", action);
            Frames(1); Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.ParryCounter));
            ToFrame(20); Assert.That((realFrames - chargeStart) / 60f, Is.EqualTo(.9f).Within(.035f));
            var gauge = player.GetComponent(T("PlayerSpecialGauge"));
            Assert.That((bool)Call(gauge, "Consume", 100f), Is.True, "Special had enough gauge but could not cancel the cinematic; empty it to measure the counter award.");
            ToFrame(21);
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(30));
            Assert.That(Get<int>(target, "HitCount"), Is.EqualTo(1)); Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
            Assert.That(Get<int>(counter, "SelectedDamage"), Is.EqualTo(90));
            Assert.That(Get<float>(gauge, "CurrentGauge"), Is.EqualTo(3));
            Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Get<Sprite>(profile, "ContactSprite")));
            Assert.That(Get<bool>(launch, "IsActive"), Is.True);
            long tick = TimeValue<long>("TickCount"); Frames(1); Assert.That(TimeValue<long>("TickCount"), Is.EqualTo(tick), "The 8F contact stop must freeze motion and the impact cel.");
            Until(() => !Get<bool>(counter, "IsActive")); Until(() => !Get<bool>(launch, "IsActive")); Frames(6);
            Assert.That(Get<float>(launch, "TravelDistance"), Is.EqualTo(3.2f).Within(.02f));
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(30)); Assert.That(Get<int>(target, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.Zero);
            Assert.That(body.gravityScale, Is.EqualTo(gravity)); AssertClean();
        }

        [TestCase(1)] [TestCase(-1)]
        public void DefeatedEnemyRemainsVisibleForLaunchThenLeavesCombat(int direction)
        {
            var target = Begin(direction, false, 30); ToFrame(21);
            Assert.That(Get<bool>(target, "IsAlive"), Is.False); Assert.That(target.gameObject.activeSelf, Is.True);
            Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.GreaterThan(0));
            var launch = target.GetComponent(T("EnemyCounterLaunch2D")); Frames(12);
            Assert.That(Get<float>(launch, "TravelDistance"), Is.GreaterThan(.5f));
            Until(() => !target.gameObject.activeSelf); Until(() => !Get<bool>(counter, "IsActive")); Frames(6);
            Assert.That(Get<int>(target, "DeathCount"), Is.EqualTo(1)); Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); AssertClean();
        }

        [TestCase(1)] [TestCase(-1)]
        public void LaunchStopsAtSolidWallWithoutRotatingCollider(int direction)
        {
            var target = Begin(direction); ToFrame(21);
            var wall = Wall(target.transform.position.x - direction * 1.5f).GetComponent<BoxCollider2D>();
            var launch = target.GetComponent(T("EnemyCounterLaunch2D")); Until(() => !Get<bool>(launch, "IsActive"));
            Physics2D.SyncTransforms(); var collider = target.GetComponent<BoxCollider2D>();
            if (direction > 0) Assert.That(collider.bounds.min.x, Is.GreaterThanOrEqualTo(wall.bounds.max.x - .01f));
            else Assert.That(collider.bounds.max.x, Is.LessThanOrEqualTo(wall.bounds.min.x + .01f));
            Assert.That(Get<float>(launch, "TravelDistance"), Is.LessThan(1.5f));
            Assert.That(target.transform.rotation, Is.EqualTo(Quaternion.identity));
            Until(() => !Get<bool>(counter, "IsActive")); Frames(6); AssertClean();
        }

        [TestCase(1)] [TestCase(-1)]
        public void UnsafeRearPositionKeepsStockAndFallsBackToOrdinaryAttack(int direction)
        {
            body.position = new Vector2(0, .81f); player.transform.position = body.position; Call(motor, "SetFacing", direction);
            var target = Enemy("StrongMelee", new Vector2(direction * 1.15f, .81f), 120); Wall(direction * 2.5f);
            Call(counter, "RegisterParriedEnemy", target);
            Assert.That((string)Call(counter, "EligibilityReason", target), Is.EqualTo("RearPositionBlocked"));
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True); Frames(1);
            Assert.That(Get<bool>(counter, "IsActive"), Is.False); Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Assert.That(Get<int>(counter, "ConsumedCount"), Is.Zero);
            Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.Attack));
            Assert.That(Mathf.Abs(body.position.x), Is.LessThan(.2f));
        }

        [TestCase(1)] [TestCase(-1)]
        public void NewObstacleDuringTransferCancelsBeforeCrossingWall(int direction)
        {
            var target = Begin(direction); ToFrame(10); Wall(direction * 2.5f);
            Until(() => !Get<bool>(counter, "IsActive")); Frames(6);
            Assert.That(Get<string>(counter, "LastEndReason"), Is.EqualTo("ApproachBlocked"));
            Assert.That(Get<int>(target, "HitCount"), Is.Zero); Assert.That(body.position.x * direction, Is.LessThan(2.1f)); AssertClean();
        }

        [TestCase(11)] [TestCase(14)] [TestCase(22)]
        public void PauseFreezesEachPhaseAndResumesWithoutLeavingOwners(int phase)
        {
            var target = Begin(); ToFrame(phase); var position = body.position; var enemyPosition = target.transform.position;
            int frame = Get<int>(counter, "ElapsedFrames"); float alpha = Get<float>(presentation, "DimAlpha");
            var sprite = player.GetComponent<SpriteRenderer>().sprite;
            Static("SetPaused", true); Frames(60);
            Assert.That(body.position, Is.EqualTo(position)); Assert.That(target.transform.position, Is.EqualTo(enemyPosition));
            Assert.That(Get<int>(counter, "ElapsedFrames"), Is.EqualTo(frame)); Assert.That(Get<float>(presentation, "DimAlpha"), Is.EqualTo(alpha));
            Assert.That(player.GetComponent<SpriteRenderer>().sprite, Is.SameAs(sprite));
            Static("SetPaused", false); Until(() => !Get<bool>(counter, "IsActive")); Frames(8); AssertClean();
        }

        [TestCase("Reset", 11)] [TestCase("Reset", 14)] [TestCase("Reset", 22)]
        [TestCase("Disable", 11)] [TestCase("Disable", 14)] [TestCase("Disable", 22)]
        [TestCase("Death", 11)] [TestCase("Death", 14)] [TestCase("Death", 22)]
        public void InterruptionRestoresGravityDrawingTimeAndTargetLeases(string operation, int phase)
        {
            var target = Begin(); ToFrame(phase);
            if (operation == "Reset") Call(arena, "ResetActionsOnly");
            else if (operation == "Disable") { player.gameObject.SetActive(false); player.gameObject.SetActive(true); }
            else { Call(player, "SetDead", "CounterCinematicTest"); Call(player, "Revive", "CounterCinematicTest"); }
            Frames(6); AssertClean();
            Assert.That(Get<bool>(target.GetComponent(T("EnemyCounterLaunch2D")), "IsActive"), Is.False);
            Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.Zero);
            Assert.That(Get<SpriteRenderer>(art, "Artwork").enabled, Is.True);
            Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void LosingTargetBeforeOrAfterHitNeverRedirectsToAnotherStock(bool afterHit)
        {
            var target = Begin(); var other = Enemy("Melee_2", new Vector2(1.5f, .81f), 1000);
            Call(counter, "RegisterParriedEnemy", other); ToFrame(afterHit ? 21 : 14); target.gameObject.SetActive(false);
            Until(() => !Get<bool>(counter, "IsActive")); Frames(8);
            Assert.That(Get<string>(counter, "LastEndReason"), Is.EqualTo(afterHit ? "Complete" : "TargetLost"));
            Assert.That(Get<int>(other, "HitCount"), Is.Zero); Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1)); AssertClean();
        }

        [TestCase(false)] [TestCase(true)]
        public void MultipleStocksOnlyChainOnNewAttackInsideLastSixFrames(bool reserve)
        {
            Begin(); var other = Enemy("Melee_2", new Vector2(1.5f, .81f), 1000); Call(counter, "RegisterParriedEnemy", other);
            ToFrame(38); var attack = player.GetComponent(T("PlayerAttack2D"));
            Assert.That((bool)Call(attack, "RequestAttack"), Is.False, "Earlier input is not a counter reservation.");
            ToFrame(39); if (reserve) Assert.That((bool)Call(attack, "RequestAttack"), Is.True);
            Assert.That(Get<int>(other, "HitCount"), Is.Zero);
            if (reserve)
            {
                Until(() => Get<int>(counter, "StartCount") == 2);
                Assert.That(Get<object>(counter, "SelectedTarget"), Is.SameAs(other));
                Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
                Until(() => Get<int>(other, "HitCount") == 1); Assert.That(Get<int>(other, "CurrentHp"), Is.EqualTo(910));
            }
            else
            {
                Until(() => !Get<bool>(counter, "IsActive")); Frames(20);
                Assert.That(Get<int>(counter, "StartCount"), Is.EqualTo(1)); Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator ZeroPresentationSettingsPreserveMotionDamageAndTiming()
        {
            var target = Begin(); Until(() => !Get<bool>(counter, "IsActive"));
            int duration = realFrames; Vector2 finalPlayer = body.position, finalEnemy = target.transform.position;
            int hp = Get<int>(target, "CurrentHp"); Call(arena, "ResetActionsOnly"); yield return null;
            Set(Get<object>(feedback, "Settings"), "flash", 0f); Set(Get<object>(feedback, "Settings"), "vfx", 0f);
            realFrames = 0; target = Begin(); ToFrame(14);
            Assert.That(Get<float>(presentation, "DimAlpha"), Is.Zero); Assert.That(Get<bool>(presentation, "IsSpotlightVisible"), Is.False);
            Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.Zero); Assert.That(TimeValue<float>("SlowScale"), Is.EqualTo(.15f).Within(.00001f));
            Until(() => !Get<bool>(counter, "IsActive"));
            Assert.That(realFrames, Is.EqualTo(duration)); Assert.That(Vector2.Distance(body.position, finalPlayer), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(target.transform.position, finalEnemy), Is.LessThan(.001f)); Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(hp));
            Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.Zero); Frames(6); AssertClean();
        }

        [Test] public void SwitchingEffectsOffClearsExistingDecorationWithoutCancelingCharge()
        {
            Begin(); ToFrame(14); Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.GreaterThan(0));
            Set(Get<object>(feedback, "Settings"), "flash", 0f); Set(Get<object>(feedback, "Settings"), "vfx", 0f); Frames(1);
            Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.Zero); Assert.That(Get<float>(presentation, "DimAlpha"), Is.Zero);
            Assert.That(Get<bool>(counter, "IsActive"), Is.True); Assert.That(Get<bool>(presentation, "IsCharged"), Is.True);
        }

        [UnityTest] public IEnumerator ActiveSceneChangeRestoresCounterAndLaunchOwnership()
        {
            var target = Begin(); ToFrame(22); var previous = SceneManager.GetActiveScene();
            var temporary = SceneManager.CreateScene("CounterCinematicSceneChangeTest"); SceneManager.SetActiveScene(temporary);
            AssertClean(); Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.Zero);
            SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(temporary);
        }
    }
}
