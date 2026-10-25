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
    public sealed class PrototypeCombatTrialTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component clock, arena, trial, player, attack, ai, health, art, launch;
        private Rigidbody2D body;
        private bool savedManual;
        private GameObject wall;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name, Flags)?.GetValue(obj) ?? obj.GetType().GetField(name, Flags).GetValue(obj));
        private void Frames(int count, int fps = 60) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / fps); }
        private void Until(Func<bool> predicate, int fps = 60, int maximum = 900)
        { while (!predicate() && maximum-- > 0) Frames(1, fps); Assert.That(predicate(), Is.True, "Timeout"); }
        private static void Pause(bool paused) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { paused });
        private object[] Steps => Get<Array>(Get<object>(attack, "Definition"), "steps").Cast<object>().ToArray();
        private SpriteRenderer Drawing => Get<SpriteRenderer>(art, "Artwork");
        private Sprite First(string name) => Get<Sprite[]>(Call(Get<object>(art, "Library"), "Find", "Enemy_" + name), "frames")[0];
        private void Place(int direction = 1, int hp = 1000)
        {
            body.position = new Vector2(0, .81f); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            Call(player.GetComponent(T("PlayerMotor2D")), "SetFacing", direction);
            Call(health, "ResetForCapture", new Vector3(direction * 1.2f, .8f), hp);
            Call(ai, "ResetForCapture", player.transform, true); Physics2D.SyncTransforms(); Frames(1);
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            savedManual = Get<bool>(clock, "ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena")); trial = arena.GetComponent(T("PrototypeCombatTrial"));
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine")); attack = player.GetComponent(T("PlayerAttack2D"));
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false; body = player.GetComponent<Rigidbody2D>();
            var root = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == "Melee_1");
            ai = root.GetComponent(T("EnemyMeleeChaser2D")); health = root.GetComponent(T("Damageable2D"));
            art = root.GetComponent(T("PrototypeEnemyArtPresentation")); launch = root.GetComponent(T("EnemyCounterLaunch2D"));
            Frames(2); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            Pause(false); if (wall != null) Object.DestroyImmediate(wall);
            Call(arena, "ResetActionsOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, savedManual); yield return null;
        }

        [Test] public void StartsInTrialWithoutEnemiesAndKeepsStrongBaseline()
        {
            Assert.That(Get<bool>(trial, "IsConfigured"), Is.True);
            Assert.That(Get<bool>(trial, "FastAttacks"), Is.True); Assert.That(ai.gameObject.activeSelf, Is.False);
            Assert.That(Steps.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 12, 9, 12, 30 }));
            Assert.That(Get<int>(Get<object>(ai, "Definition"), "windupFrames"), Is.EqualTo(96));
            var strong = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == "StrongMelee");
            Assert.That(Get<int>(Get<object>(strong.GetComponent(T("EnemyMeleeChaser2D")), "Definition"), "maxHp"), Is.EqualTo(120));
            Assert.That(strong.GetComponent(T("PrototypeEnemyArtPresentation")), Is.Null);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ReferenceWindupUsesNinetySixTicksAllCelsAndExactlyOneHit(int fps)
        {
            Place(); Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            var seen = new HashSet<Sprite>(); int startHp = Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp");
            Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(96));
            while (Get<bool>(ai, "IsWindingUp")) { seen.Add(Drawing.sprite); Frames(1, fps); }
            Assert.That(seen, Is.SupersetOf(Get<Sprite[]>(Call(Get<object>(art, "Library"), "Find", "Enemy_Windup"), "frames")));
            Until(() => Get<string>(ai, "CurrentStateName") == "Recovery", fps);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(startHp - 10));
            Frames(fps / 3, fps);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(startHp - 10));
        }

        [TestCase(30, 1)] [TestCase(60, 1)] [TestCase(120, -1)]
        public void DownParryIsGroundedAndExpiresAfterRealStagger(int fps, int direction)
        {
            Place(direction); Call(health, "ApplyParryStun", Enum.Parse(T("ParryResult"), "Normal"));
            Assert.That(Drawing.sprite, Is.SameAs(First("Death"))); Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Parried"));
            Assert.That(Drawing.transform.localPosition.y, Is.EqualTo(.2f - 2f / 32).Within(.0001f));
            Assert.That(Drawing.flipX, Is.EqualTo(direction > 0));
            Frames(fps * 2, fps); Assert.That(Get<int>(health, "ParryStaggerRemainingFrames"), Is.EqualTo(60));
            Pause(true); Frames(fps, fps); Assert.That(Get<int>(health, "ParryStaggerRemainingFrames"), Is.EqualTo(60)); Pause(false);
            Frames(fps, fps); Assert.That(Get<bool>(health, "IsParryStaggered"), Is.False);
            Assert.That(Get<string>(art, "MotionName"), Is.Not.EqualTo("Parried"));
            Assert.That(Drawing.transform.localPosition.y, Is.EqualTo(.2f).Within(.0001f));
        }

        [TestCase(30, 1)] [TestCase(60, -1)] [TestCase(120, 1)]
        public void CounterLowHopHasNoRotationAndReleasesMotionAndDeathHolds(int fps, int direction)
        {
            Place(direction, 30); var counter = player.GetComponent(T("PlayerParryCounter2D"));
            Call(counter, "RegisterParriedEnemy", health); Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(counter, "HitCount") == 1, fps);
            Vector3 origin = ai.transform.position; float peak = 0; int owners = 0;
            Until(() => Get<int>(launch, "ElapsedFrames") >= 1, fps);
            while (Get<bool>(launch, "IsActive"))
            {
                peak = Mathf.Max(peak, ai.transform.position.y - origin.y); owners = Mathf.Max(owners, Get<int>(health, "MotionOwnerCount"));
                Assert.That(Drawing.transform.parent.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(Drawing.color.a, Is.EqualTo(1)); Frames(1, fps);
            }
            Assert.That(peak, Is.InRange(.23f, .28f)); Assert.That(Get<float>(launch, "TravelDistance"), Is.EqualTo(1.6f).Within(.02f));
            Assert.That(owners, Is.EqualTo(1)); Assert.That(ai.transform.position.y, Is.EqualTo(.8f).Within(.03f));
            Until(() => !ai.gameObject.activeSelf, fps); Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero);
            Assert.That(Get<int>(health, "DeathPresentationOwnerCount"), Is.Zero);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void FastFourStepComboStillHitsEachTargetOnlyOnceAndKeepsDamage(int fps)
        {
            Place(); Call(health, "AcquireMotionControl", this);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(attack, "ComboStep") == 1, fps); // 120fps render frames can contain zero combat ticks
            int expected = 1000;
            for (int step = 1; step <= 4; step++)
            {
                Assert.That(Get<int>(attack, "ComboStep"), Is.EqualTo(step));
                Until(() => Get<object>(attack, "Phase").ToString() == "Cancel", fps);
                expected -= Get<int>(Steps[step - 1], "damage");
                Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(expected), "step " + step);
                if (step < 4) { Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True); Until(() => Get<int>(attack, "ComboStep") == step + 1, fps); }
            }
            Until(() => Get<bool>(attack, "IsRecovering"), fps);
            Assert.That(Get<int>(attack, "FullComboCount"), Is.EqualTo(1));
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(940));
            Assert.That(Get<int>(attack, "TimeoutFallbackCount"), Is.Zero);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(attack, "ComboStep") == 1, fps);
            Assert.That(Get<int>(attack, "ComboStep"), Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator EveryFastStepCanCancelAtItsBoundaryToAllDefenseActions()
        {
            foreach (var request in new[] { PlayerActionRequest.Dash, PlayerActionRequest.Parry, PlayerActionRequest.Special })
                for (int desired = 1; desired <= 4; desired++)
                {
                    Call(arena, "ResetActionsOnly"); yield return null;
                    var gauge = player.GetComponent(T("PlayerSpecialGauge")); gauge.GetType().GetProperty("CurrentGauge").SetValue(gauge, 100f);
                    Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
                    Frames(1);
                    for (int step = 1; step <= desired; step++)
                    {
                        int boundary = Get<int>(Steps[step - 1], "cancelStart");
                        Until(() => Get<int>(attack, "ElapsedFrames") == boundary - 1);
                        Assert.That(Get<object>(attack, "OpenCancelWindows").ToString(), Is.EqualTo("None"));
                        Assert.That((bool)Call(attack, "TryAdvanceConfiguredCombo"), Is.False);
                        Assert.That((bool)Call(attack, "TrySetDefinition", Get<object>(attack, "Definition")), Is.False);
                        Frames(1); Assert.That(Get<object>(attack, "Phase").ToString(), Is.EqualTo("Cancel"));
                        if (step < desired) { Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True); Frames(1); }
                    }
                    Assert.That(Get<int>(attack, "ElapsedFrames"), Is.EqualTo(Get<int>(Steps[desired - 1], "cancelStart")));
                    Assert.That((bool)Call(player, "RequestAction", request), Is.True, request + " from " + desired);
                    Frames(1);
                    Assert.That(Get<bool>(attack, "IsAttacking"), Is.False);
                }
        }

        [TestCase(1)] [TestCase(-1)] public void ReferenceLaunchStopsAtWallAndPauseAndHitstopFreezeIt(int direction)
        {
            Place(direction);
            wall = new GameObject("ReferenceLaunchWall"); wall.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = new Vector3(direction * 2.3f, 2);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4); Physics2D.SyncTransforms();
            var profile = Get<object>(player.GetComponent(T("PlayerParryCounter2D")), "CinematicProfile");
            Call(launch, "BeginLaunch", profile, direction); Frames(3);
            Vector3 position = ai.transform.position;
            Pause(true); Frames(120); Assert.That(ai.transform.position, Is.EqualTo(position)); Pause(false);
            T("CombatTimeController").GetMethod("RequestHitstop").Invoke(null, new object[] { this, .1f });
            Frames(5); Assert.That(ai.transform.position, Is.EqualTo(position));
            T("CombatTimeController").GetMethod("ReleaseOwner").Invoke(null, new object[] { this });
            Until(() => !Get<bool>(launch, "IsActive"));
            Assert.That(Mathf.Abs(ai.transform.position.x), Is.LessThanOrEqualTo(1.86f));
            Assert.That(ai.transform.position.y, Is.EqualTo(.8f).Within(.03f));
            Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero);
            Assert.That(Drawing.transform.parent.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test] public void SwitchingDuringChargeRestoresOriginalsAndCleansAllOwners()
        {
            Place(); var counter = player.GetComponent(T("PlayerParryCounter2D")); Call(counter, "RegisterParriedEnemy", health);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(counter, "ElapsedFrames") >= 13);
            Call(trial, "RestoreOriginals");
            Assert.That(Steps.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 15, 12, 15, 38 }));
            Assert.That(Get<int>(Get<object>(ai, "Definition"), "windupFrames"), Is.EqualTo(30));
            Assert.That(Get<bool>(art, "UsesDownParryPose"), Is.False);
            Assert.That(Get<bool>(counter, "IsActive"), Is.False); Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
            Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero); Assert.That(Get<bool>(launch, "IsActive"), Is.False);
            Assert.That((int)T("CombatTimeController").GetProperty("SlowOwnerCount").GetValue(null), Is.Zero);
            Assert.That(player.GetComponent<Animator>().speed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)] public void SwitchPreservesSelectedEncounterAndAxesStayIndependent(bool mixed)
        {
            if (mixed) Call(arena, "ResetStrongEncounter", true); else Call(arena, "ResetArena", false);
            for (int i = 0; i < 10; i++)
            {
                Call(trial, "SetModes", false, true, false);
                Assert.That(ai.gameObject.activeSelf, Is.True); Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(30));
                Assert.That(Get<int>(Get<object>(ai, "Definition"), "windupFrames"), Is.EqualTo(30));
                Assert.That(Get<bool>(art, "UsesDownParryPose"), Is.True);
                Call(trial, "SetModes", true, false, true);
                Assert.That(Get<bool>(art, "UsesDownParryPose"), Is.False);
                Assert.That(Get<int>(Steps[0], "activeStart"), Is.EqualTo(12));
                Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(health, "DeathPresentationOwnerCount"), Is.Zero);
            }
            var strong = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == "StrongMelee");
            Assert.That(strong.activeSelf, Is.EqualTo(mixed));
        }
    }
}
