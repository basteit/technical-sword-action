using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeEnemyArtTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Component clock, arena, player, ai, health, art;
        private Rigidbody2D body;
        private bool savedManual;
        private SpriteRenderer drawing;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name, Flags)?.GetValue(obj) ?? obj.GetType().GetField(name, Flags).GetValue(obj));
        private void Frames(int count, int fps = 60) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / fps); }
        private void Until(Func<bool> ready, int maximum = 480)
        { while (!ready() && maximum-- > 0) Frames(1); Assert.That(ready(), Is.True, "Timed out in " + Get<string>(art, "MotionName")); }
        private static void Pause(bool paused) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { paused });
        private object Motion(string name) => Call(Get<object>(art, "Library"), "Find", "Enemy_" + name);
        private Sprite First(string name) => Get<Sprite[]>(Motion(name), "frames")[0];

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            savedManual = Get<bool>(clock, "ManualAdvanceOnly"); clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            body = player.GetComponent<Rigidbody2D>();
            var root = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == "Melee_1");
            ai = root.GetComponent(T("EnemyMeleeChaser2D")); health = root.GetComponent(T("Damageable2D"));
            art = root.GetComponent(T("PrototypeEnemyArtPresentation")); drawing = Get<SpriteRenderer>(art, "Artwork");
            Frames(2); yield return null;
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            Pause(false); Call(arena, "ResetActionsOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, savedManual);
            yield return null;
        }

        private void Place(int direction = 1, int hp = 120)
        {
            body.position = new Vector2(0, .81f); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            Call(player.GetComponent(T("PlayerMotor2D")), "SetFacing", direction);
            Call(health, "ResetForCapture", new Vector3(direction * 1.2f, .8f), hp);
            Call(ai, "ResetForCapture", player.transform, true);
            Physics2D.SyncTransforms(); Frames(1);
        }

        [Test] public void OptionalPresetsUseSubmittedNormalsWithoutChangingStrongEnemyOrHealth()
        {
            Assert.That(ai.gameObject.activeSelf, Is.False);
            Call(arena, "ResetArena", false);
            Assert.That(ai.gameObject.activeSelf, Is.True); Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(30));
            Assert.That(drawing.sprite.name, Does.StartWith("EnemySample_"));
            Assert.That(ai.GetComponentInChildren<SpriteRenderer>(), Is.SameAs(drawing), "Common feedback must find actual artwork, not the hidden cube.");
            var strong = player.gameObject.scene.GetRootGameObjects().Single(go => go.name == "StrongMelee");
            Assert.That(strong.GetComponent(T("PrototypeEnemyArtPresentation")), Is.Null);
            Call(arena, "ResetArena", true);
            Assert.That(Object.FindObjectsByType(T("PrototypeEnemyArtPresentation"), FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Call(arena, "ResetActionsOnly"); Assert.That(ai.gameObject.activeSelf, Is.False);
        }

        [TestCase(1)] [TestCase(-1)]
        public void FacingAndFeetUseTheSamePixelScaleAsReceivedVideoArtwork(int direction)
        {
            Place(direction); Assert.That(drawing.flipX, Is.EqualTo(direction > 0));
            Assert.That(drawing.sprite.pixelsPerUnit, Is.EqualTo(32));
            Assert.That(drawing.transform.localPosition.y, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(drawing.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(drawing.transform.parent.name, Is.EqualTo("CounterLaunchVisual"));
        }

        [Test]
        public void RealThirtyTickWindupShowsAllSubmittedCelsAndContactBeforeRecovery()
        {
            Place(); Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            var seen = new HashSet<Sprite>();
            while (Get<bool>(ai, "IsWindingUp")) { seen.Add(drawing.sprite); Frames(1); }
            Assert.That(seen, Is.SupersetOf(Get<Sprite[]>(Motion("Windup"), "frames")));
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Attack"));
            Assert.That(drawing.sprite, Is.SameAs(Get<Sprite[]>(Motion("Windup"), "frames").Last()));
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(90));
            Until(() => Get<string>(ai, "CurrentStateName") == "Recovery");
            Assert.That(drawing.sprite, Is.SameAs(First("Attack")));
        }

        [Test]
        public void LivingHitUsesChanceThenReturnsWithoutResumingAnInterruptedAttack()
        {
            Place(); Call(ai, "BeginCaptureAttack"); Frames(5);
            Assert.That((bool)Call(health, "TryTakeHit", 5, Vector2.right), Is.True);
            Assert.That(drawing.sprite, Is.SameAs(First("Hit")));
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Hit"));
            Frames(40); Assert.That(Get<string>(art, "MotionName"), Is.Not.EqualTo("Windup"));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ParryDrawingLastsOnlyForTheRealThreeSecondWindow(int fps)
        {
            Place();
            Call(health, "ApplyParryStun", Enum.Parse(T("ParryResult"), "Normal"));
            Assert.That(drawing.sprite, Is.SameAs(First("Parried")));
            Frames(fps * 2, fps);
            Assert.That(Get<int>(health, "ParryStaggerRemainingFrames"), Is.EqualTo(60));
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Parried"));
            Frames(fps, fps);
            Assert.That(Get<bool>(health, "IsParryStaggered"), Is.False);
            Assert.That(Get<string>(art, "MotionName"), Is.Not.EqualTo("Parried"));
        }

        [Test]
        public void PauseAndHitstopFreezeDrawingAndDeathFade()
        {
            Place(); Call(ai, "BeginCaptureAttack"); Frames(10);
            Sprite pose = drawing.sprite; Vector3 position = drawing.transform.position;
            Pause(true); Frames(120); Assert.That(drawing.sprite, Is.SameAs(pose)); Assert.That(drawing.transform.position, Is.EqualTo(position));
            Pause(false);
            T("CombatTimeController").GetMethod("RequestHitstop").Invoke(null, new object[] { this, .1f });
            Frames(5); Assert.That(drawing.sprite, Is.SameAs(pose));
            T("CombatTimeController").GetMethod("ReleaseOwner").Invoke(null, new object[] { this });
            Call(health, "TryTakeHit", 1000, Vector2.right); Frames(32);
            float alpha = drawing.color.a; Assert.That(alpha, Is.InRange(.001f, .999f));
            Pause(true); Frames(120); Assert.That(drawing.color.a, Is.EqualTo(alpha));
        }

        [Test]
        public void DeathPoseIsGroundedAndVisualOnlyHoldEndsWithoutMotionOwnership()
        {
            Place(); Call(health, "TryTakeHit", 1000, Vector2.right);
            Assert.That(Get<bool>(health, "IsAlive"), Is.False);
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Death"));
            Assert.That(drawing.sprite, Is.SameAs(First("Death")));
            Assert.That(drawing.transform.localPosition.y, Is.EqualTo(.2f - 2f / 32).Within(.0001f));
            Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero);
            Until(() => !ai.gameObject.activeSelf);
            Assert.That(Get<int>(health, "DeathPresentationOwnerCount"), Is.Zero);
        }

        [Test]
        public void CounterLaunchKeepsItsSoleMotionOwnerAndDoesNotFadeBeforeLanding()
        {
            Place(1, 30);
            var counter = player.GetComponent(T("PlayerParryCounter2D"));
            Call(counter, "RegisterParriedEnemy", health);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(counter, "HitCount") == 1);
            var launch = ai.GetComponent(T("EnemyCounterLaunch2D"));
            Assert.That(Get<bool>(launch, "IsActive"), Is.True);
            Until(() => Get<int>(launch, "ElapsedFrames") >= 5);
            Assert.That(Get<int>(health, "MotionOwnerCount"), Is.EqualTo(1));
            Assert.That(drawing.color.a, Is.EqualTo(1));
            Assert.That(drawing.transform.parent.localRotation, Is.Not.EqualTo(Quaternion.identity));
            Until(() => !Get<bool>(launch, "IsActive"));
            Until(() => !ai.gameObject.activeSelf);
            Assert.That(Get<int>(health, "MotionOwnerCount"), Is.Zero);
        }

        [Test]
        public void ResetDuringWindupRestoresIdleBeforeAnotherCombatTick()
        {
            Place(); Call(ai, "BeginCaptureAttack"); Frames(8);
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Windup"));
            Call(arena, "ResetArena", false);
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Idle"));
            Assert.That(drawing.sprite, Is.SameAs(First("Idle")));
        }

        [Test]
        public void TenResetCyclesRestoreFeetOpacityAndNoPresentationLease()
        {
            for (int i = 0; i < 10; i++)
            {
                Place(); Call(health, "ApplyParryStun", Enum.Parse(T("ParryResult"), "Normal"));
                Call(health, "TryTakeHit", 1000, Vector2.right); Frames(25);
                Call(arena, "ResetArena", false);
                Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Idle"));
                Assert.That(drawing.color, Is.EqualTo(Color.white));
                Assert.That(drawing.transform.localPosition.y, Is.EqualTo(.2f).Within(.0001f));
                Assert.That(Get<int>(health, "DeathPresentationOwnerCount"), Is.Zero);
            }
        }
    }
}
