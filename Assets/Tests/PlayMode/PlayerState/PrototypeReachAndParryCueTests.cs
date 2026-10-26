using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeReachAndParryCueTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<GameObject> probes = new();
        private Component clock, arena, player, attack, ai, health, parry, counter;
        private Rigidbody2D body;
        private bool manual;
        private float gravity;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name)
        {
            var property = obj.GetType().GetProperty(name, Flags);
            return (V)(property != null ? property.GetValue(obj) : obj.GetType().GetField(name, Flags).GetValue(obj));
        }
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        private void Frames(int count, int fps = 60) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / fps); }
        private void Until(Func<bool> done, int fps = 60)
        {
            int limit = 900;
            while (!done() && --limit > 0) Frames(1, fps);
            Assert.That(done(), Is.True, "Timed out waiting for combat state.");
        }
        private static void Pause(bool value) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { value });
        private Component Enemy(string name) => player.gameObject.scene.GetRootGameObjects().Single(go => go.name == name).GetComponent(T("EnemyMeleeChaser2D"));
        private void Position(int direction, bool airborne = false)
        {
            body.position = new Vector2(0, airborne ? 5 : .81f);
            player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            if (airborne) body.gravityScale = 0;
            Call(player.GetComponent(T("PlayerMotor2D")), "SetFacing", direction);
            Physics2D.SyncTransforms(); Frames(1);
        }
        private void Request(PlayerActionRequest request) => Assert.That((bool)Call(player, "RequestAction", request), Is.True);

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            attack = player.GetComponent(T("PlayerAttack2D")); parry = player.GetComponent(T("PlayerParry2D")); counter = player.GetComponent(T("PlayerParryCounter2D"));
            body = player.GetComponent<Rigidbody2D>(); gravity = body.gravityScale;
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            ai = Enemy("Melee_1"); health = ai.GetComponent(T("Damageable2D"));
            Call(health, "ResetForCapture", new Vector3(10, .8f), 1000);
            Call(ai, "ResetForCapture", player.transform, true);
            Frames(1); yield return null; // Respect the render-frame input guard after disabling the router.
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            Pause(false);
            foreach (var probe in probes) if (probe != null) Object.DestroyImmediate(probe);
            probes.Clear(); body.gravityScale = gravity;
            Call(arena, "ResetActionsOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual);
            yield return null;
        }

        private static IEnumerable<TestCaseData> ReachCases()
        {
            foreach (int fps in new[] { 30, 60, 120 })
                foreach (int direction in new[] { -1, 1 })
                    foreach (bool airborne in new[] { false, true })
                        foreach (int step in new[] { 1, 2 }) yield return new TestCaseData(fps, direction, airborne, step);
        }

        [TestCaseSource(nameof(ReachCases))]
        public void SlashTipHitsOnceButOutsideHeightAndRearDoNotGrow(int fps, int direction, bool airborne, int step)
        {
            Position(direction, airborne);
            Assert.That(Get<bool>(player.GetComponent(T("PlayerMotor2D")), "IsGrounded"), Is.EqualTo(!airborne));
            Request(PlayerActionRequest.Attack); Until(() => Get<int>(attack, "ComboStep") == 1, fps);
            if (step == 2)
            {
                Until(() => Get<object>(attack, "Phase").ToString() == "Cancel", fps);
                Request(PlayerActionRequest.Attack); Until(() => Get<int>(attack, "ComboStep") == 2, fps);
            }
            var configured = Get<Array>(Get<object>(attack, "Definition"), "steps").GetValue(step - 1);
            int active = Get<int>(configured, "activeStart");
            Until(() => Get<int>(attack, "ElapsedFrames") >= active - 2, fps);
            // Place probes immediately before contact so ground motion is not mistaken for extra reach.
            Until(() => Get<int>(attack, "ElapsedFrames") == active);
            float front = step == 1 ? 2.3125f : 2.65625f;
            float center = .9f + Get<float>(configured, "forwardExtension") * .5f;
            Vector2 root = player.transform.position;
            var targets = new[]
            {
                Probe("tip-inside", root + new Vector2(direction * (front - .05f), 0)),
                Probe("tip-outside", root + new Vector2(direction * (front + .05f), 0)),
                Probe("above", root + new Vector2(direction * center, .85f)),
                Probe("below", root + new Vector2(direction * center, -.85f)),
                Probe("rear", root + new Vector2(-direction * .1f, 0)),
                Probe("original-circle", root + new Vector2(direction * 1.5f, 0))
            };
            Physics2D.SyncTransforms();
            Until(() => Get<object>(attack, "Phase").ToString() == "Cancel", fps);
            for (int i = 0; i < targets.Length; i++)
            {
                int expected = i == 0 || i == 5 ? 1 : 0;
                Assert.That(Get<int>(targets[i], "HitCount"), Is.EqualTo(expected), targets[i].name);
                Assert.That(Get<int>(targets[i], "CurrentHp"), Is.EqualTo(1000 - expected * 10), targets[i].name);
            }
            Assert.That(Get<int>(health, "HitCount"), Is.Zero, "The distant scene enemy remains outside the slash.");
            var effects = Get<IEnumerable>(player.GetComponent(T("CombatFeedback2D")), "pool").Cast<object>();
            Assert.That(effects.Any(effect => Get<GameObject>(effect, "root").activeSelf &&
                Vector2.Distance(Get<Vector2>(effect, "position"), targets[0].transform.position) <= .022f), Is.True,
                "The far-tip hit spark belongs on the struck collider, not at the old HitPoint next to the hand.");
        }

        private Component Probe(string name, Vector2 position)
        {
            var go = new GameObject(name); probes.Add(go); go.layer = LayerMask.NameToLayer("Enemy"); go.transform.position = position;
            go.AddComponent<SpriteRenderer>(); var circle = go.AddComponent<CircleCollider2D>(); circle.radius = .02f; circle.isTrigger = true;
            var extra = new GameObject("duplicate collider"); extra.layer = go.layer; extra.transform.SetParent(go.transform, false);
            var box = extra.AddComponent<BoxCollider2D>(); box.size = Vector2.one * .02f; box.isTrigger = true;
            var value = go.AddComponent(T("Damageable2D")); Call(value, "ResetForCapture", (Vector3)position, 1000);
            Call(value, "AcquireMotionControl", this); return value;
        }

        private static IEnumerable<TestCaseData> CueCases()
        {
            foreach (int fps in new[] { 30, 60, 120 })
                foreach (int direction in new[] { -1, 1 })
                    foreach (string enemy in new[] { "ReferenceNormal", "OriginalNormal", "Strong" })
                        foreach (int delay in new[] { 0, 6, 10 }) yield return new TestCaseData(fps, direction, enemy, delay);
        }

        [TestCaseSource(nameof(CueCases))]
        public void FirstLargeAuthoredFlashAndDelayedInputsBothParryTheActualAttack(int fps, int direction, string kind, int delay)
        {
            if (kind == "Strong") ai = Enemy("StrongMelee");
            if (kind == "OriginalNormal") Call(ai, "ConfigureDefinition", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/NormalMelee.asset"));
            health = ai.GetComponent(T("Damageable2D"));
            Position(direction);
            Call(health, "ResetForCapture", new Vector3(direction * 1.2f, .8f), 1000);
            Call(ai, "ResetForCapture", player.transform, true); Physics2D.SyncTransforms(); Frames(1);
            Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            int expectedWindup = kind == "Strong" ? 42 : kind == "OriginalNormal" ? 30 : 96;
            Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(expectedWindup));
            var cue = Get<SpriteRenderer>(ai.GetComponent(T("EnemyCombatCue2D")), "cue");
            var art = ai.GetComponent(T("PrototypeEnemyArtPresentation"));
            Func<bool> visible = art == null ? () => cue.enabled : () => Get<bool>(art, "IsAuthoredParryCueVisible");
            int limit = 900;
            while (!visible() && --limit > 0)
            {
                Assert.That(cue.enabled, Is.False, "Do not invite an input too early in the windup.");
                Frames(1, fps);
            }
            Assert.That(limit, Is.GreaterThan(0));
            if (art == null) { Assert.That(cue.enabled, Is.True); Assert.That(cue.color, Is.EqualTo(Color.yellow)); }
            else
            {
                Assert.That(Get<bool>(art, "HasAuthoredParryCue"), Is.True);
                Assert.That(cue.enabled, Is.False, "Never duplicate the flash already drawn into the enemy sprite.");
                var windup = Call(Get<object>(art, "Library"), "Find", "Enemy_Windup");
                Assert.That(Get<SpriteRenderer>(art, "Artwork").sprite, Is.SameAs(Get<Sprite[]>(windup, "frames")[7]));
                Assert.That(Get<int>(art, "WindupSourceFrame"), Is.InRange(66, 71));
            }
            int lead = kind == "Strong" ? 12 : 14;
            Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.InRange(lead - (fps == 30 ? 1 : 0), lead));
            Frames(delay); Request(PlayerActionRequest.Parry);
            Until(() => Get<int>(parry, "SuccessCount") == 1, fps);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(100));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(health, "IsParryStaggered"), Is.True);
            Assert.That(Get<object>(parry, "LastParryResult").ToString(), Is.EqualTo(delay == 10 ? "Just" : "Normal"));
            Assert.That(cue.enabled, Is.False, "Do not leave a yellow cue after contact/stagger.");
            if (art != null) Assert.That(Get<bool>(art, "IsAuthoredParryCueVisible"), Is.False);
        }

        [Test] public void PauseAndHitstopFreezeTheAuthoredLargeFlashAndResetHidesIt()
        {
            Position(1); Call(health, "ResetForCapture", new Vector3(1.2f, .8f), 1000);
            Call(ai, "ResetForCapture", player.transform, true); Physics2D.SyncTransforms();
            Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            var art = ai.GetComponent(T("PrototypeEnemyArtPresentation"));
            Until(() => Get<bool>(art, "IsAuthoredParryCueVisible"));
            int remaining = Get<int>(ai, "WindupRemainingFrames");
            int sourceFrame = Get<int>(art, "WindupSourceFrame"); var sprite = Get<SpriteRenderer>(art, "Artwork").sprite;
            var cue = Get<SpriteRenderer>(ai.GetComponent(T("EnemyCombatCue2D")), "cue");
            Pause(true); Frames(120); Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(remaining)); Assert.That(cue.enabled, Is.False);
            Assert.That(Get<int>(art, "WindupSourceFrame"), Is.EqualTo(sourceFrame)); Assert.That(Get<SpriteRenderer>(art, "Artwork").sprite, Is.SameAs(sprite));
            Pause(false); T("CombatTimeController").GetMethod("RequestHitstop").Invoke(null, new object[] { this, 8f / 60f });
            Frames(8); Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(remaining)); Assert.That(cue.enabled, Is.False);
            Assert.That(Get<int>(art, "WindupSourceFrame"), Is.EqualTo(sourceFrame)); Assert.That(Get<SpriteRenderer>(art, "Artwork").sprite, Is.SameAs(sprite));
            Frames(1); Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(remaining - 1));
            Call(arena, "ResetActionsOnly"); Assert.That(cue.enabled, Is.False); Assert.That(Get<bool>(ai, "IsParryCueActive"), Is.False);
            Assert.That(Get<bool>(art, "IsAuthoredParryCueVisible"), Is.False);
        }

        [Test] public void TheEarlierSmallSparkDoesNotLatchParryForThirtyFrames()
        {
            Position(1); Call(health, "ResetForCapture", new Vector3(1.2f, .8f), 1000);
            Call(ai, "ResetForCapture", player.transform, true); Physics2D.SyncTransforms();
            Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            var art = ai.GetComponent(T("PrototypeEnemyArtPresentation"));
            var windup = Call(Get<object>(art, "Library"), "Find", "Enemy_Windup");
            var drawing = Get<SpriteRenderer>(art, "Artwork"); var small = Get<Sprite[]>(windup, "frames")[6];
            Until(() => drawing.sprite == small);
            Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.GreaterThan(13));
            Assert.That(Get<bool>(art, "IsAuthoredParryCueVisible"), Is.False);
            Assert.That(Get<SpriteRenderer>(ai.GetComponent(T("EnemyCombatCue2D")), "cue").enabled, Is.False);
            Request(PlayerActionRequest.Parry); Frames(30);
            Assert.That(Get<int>(parry, "SuccessCount"), Is.Zero); Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(90));
        }

        [Test] public void UnparryableSweepKeepsItsFullRedWarningAndStillDamagesParry()
        {
            ai = Enemy("StrongMelee"); health = ai.GetComponent(T("Damageable2D")); Position(1);
            Call(health, "ResetForCapture", new Vector3(1.2f, .8f), 1000); Call(ai, "ResetForCapture", player.transform, true);
            Set(ai, "nextAttackIndex", 1); Physics2D.SyncTransforms(); Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            var cue = Get<SpriteRenderer>(ai.GetComponent(T("EnemyCombatCue2D")), "cue");
            Frames(1); Assert.That(cue.enabled, Is.True); Assert.That(cue.color, Is.EqualTo(Color.red));
            Assert.That(Get<bool>(ai, "IsParryCueActive"), Is.False);
            Until(() => Get<int>(ai, "WindupRemainingFrames") <= 12);
            Assert.That(cue.enabled, Is.True); Assert.That(cue.color, Is.EqualTo(Color.red)); Request(PlayerActionRequest.Parry);
            Frames(15); Assert.That(Get<int>(parry, "SuccessCount"), Is.Zero); Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(80));
        }
    }
}
