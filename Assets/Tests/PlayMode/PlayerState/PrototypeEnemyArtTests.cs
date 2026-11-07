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
            // Preserve the #43 baseline contract; #34 trial behavior has its own comparisons.
            Call(arena.GetComponent(T("PrototypeCombatTrial")), "RestoreOriginals");
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
        public void AuthoredForecastLightDrawsOverPlayerWithoutBringingEnemyBodyForward()
        {
            Place();
            var heroArt = Get<SpriteRenderer>(player.GetComponent(T("PrototypePlayerArtPresentation")), "Artwork");
            Assert.That((bool)Call(ai, "BeginCaptureAttack"), Is.True);
            Until(() => Get<bool>(art, "IsAuthoredParryCueVisible"));
            var cue = drawing.transform.Find("ForegroundParryCue").GetComponent<SpriteRenderer>();
            Assert.That(cue.sprite, Is.SameAs(drawing.sprite));
            Assert.That(cue.flipX, Is.EqualTo(drawing.flipX));
            Assert.That(drawing.sortingOrder, Is.LessThan(heroArt.sortingOrder));
            Assert.That(cue.sortingOrder, Is.GreaterThan(heroArt.sortingOrder));

            // An opaque stand-in at the player's order covers the entire enemy to expose any body pixels leaking through.
            var cover = new GameObject("PlayerSortOccluder").AddComponent<SpriteRenderer>();
            var white = Texture2D.whiteTexture;
            cover.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), Vector2.one * .5f, white.width);
            cover.color = Color.magenta;
            cover.sharedMaterial = drawing.sharedMaterial;
            cover.sortingLayerID = heroArt.sortingLayerID;
            cover.sortingOrder = heroArt.sortingOrder + 1;
            cover.transform.position = drawing.bounds.center;
            cover.transform.localScale = Vector3.one * 8;
            var camera = Camera.main;
            var position = camera.transform.position; float size = camera.orthographicSize;
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            const int resolution = 128;
            var target = RenderTexture.GetTemporary(resolution, resolution, 24);
            var pixels = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            try
            {
                camera.transform.position = drawing.bounds.center + Vector3.back * 10;
                camera.orthographicSize = 2; camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                var colors = pixels.GetPixels32();
                Assert.That(colors.Any(IsYellow), Is.True, "The existing yellow cue must render in front of the cover.");
                Assert.That(colors.All(p => IsYellow(p) || (p.r > 230 && p.g < 30 && p.b > 230)), Is.True,
                    "No blue/grey body pixels may render in front of the cover.");
            }
            finally
            {
                camera.transform.position = position; camera.orthographicSize = size;
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(cover.sprite); Object.DestroyImmediate(cover.gameObject);
            }

            Call(health, "ResetForCapture", ai.transform.position, 120);
            Assert.That(cue.enabled, Is.False, "Reset removes the telegraph immediately.");
        }

        private static bool IsYellow(Color32 pixel) => pixel.r > 230 && pixel.g > 220 && pixel.b < 175;

        [Test]
        public void LivingHitOutsideAnAttackUsesChance()
        {
            Place();
            Assert.That((bool)Call(health, "TryTakeHit", 5, Vector2.right), Is.True);
            Assert.That(drawing.sprite, Is.SameAs(First("Hit")));
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Hit"));
            Frames(40); Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Idle"));
        }

        [TestCase("Light")] [TestCase("Strong")]
        public void NonlethalHitsKeepTheWindupAndActiveAttackButCanPunishRecovery(string strength)
        {
            Place(); Assert.That(Call(ai, "BeginCaptureAttack"), Is.True);
            Frames(5);
            var hitStrength = Enum.Parse(T("EnemyHitStrength"), strength);
            Vector3 start = ai.transform.position;
            Vector2 facing = Get<Vector2>(ai, "Facing");
            long sequence = Get<long>(ai, "AttackSequence");
            var coordinator = Object.FindFirstObjectByType(T("EnemyAttackCoordinator2D"));
            void HitWithoutInterrupting(string motion)
            {
                Sprite pose = drawing.sprite;
                int windup = Get<int>(ai, "WindupRemainingFrames"), active = Get<int>(ai, "ActiveRemainingFrames");
                Assert.That(Get<bool>(ai, "ResistsHitStun"), Is.True);
                Assert.That(Call(health, "TryTakeCombatHit", 5, Vector2.right, hitStrength), Is.True);
                Assert.That(Get<string>(health, "LastHitReaction"), Is.EqualTo("AttackArmor"));
                Assert.That(Get<bool>(health, "IsStunned"), Is.False);
                Assert.That(Get<int>(ai, "WindupRemainingFrames"), Is.EqualTo(windup));
                Assert.That(Get<int>(ai, "ActiveRemainingFrames"), Is.EqualTo(active));
                Assert.That(Get<string>(art, "MotionName"), Is.EqualTo(motion));
                Assert.That(drawing.sprite, Is.SameAs(pose));
                Assert.That(Get<Vector2>(ai, "Facing"), Is.EqualTo(facing));
                Assert.That(Get<long>(ai, "AttackSequence"), Is.EqualTo(sequence));
                Assert.That(Get<int>(coordinator, "ActiveAttackers"), Is.EqualTo(1));
            }
            HitWithoutInterrupting("Windup");
            Until(() => Get<int>(ai, "ActiveRemainingFrames") > 0);
            Assert.That(ai.transform.position, Is.EqualTo(start));
            var playerHealth = player.GetComponent(T("PlayerDamageReceiver2D"));
            Assert.That(Get<int>(playerHealth, "CurrentHp"), Is.EqualTo(90));
            // Contact stops both actors. Wait out the stop without advancing the remaining active frames.
            Until(() => !(bool)T("CombatTimeController").GetProperty("IsHitStopped").GetValue(null));
            HitWithoutInterrupting("Attack");
            Until(() => Get<string>(ai, "CurrentStateName") == "Recovery");
            Assert.That(ai.transform.position, Is.EqualTo(start));
            Assert.That(Get<int>(playerHealth, "CurrentHp"), Is.EqualTo(90));
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(110));
            Assert.That(Get<bool>(ai, "ResistsHitStun"), Is.False);
            Assert.That(Call(health, "TryTakeCombatHit", 5, Vector2.right, hitStrength), Is.True);
            Assert.That(Get<bool>(health, "IsStunned"), Is.True);
            Assert.That(drawing.sprite, Is.SameAs(First("Hit")));
        }

        [UnityTest]
        public IEnumerator ActualSlashDoesNotStopTheReferenceWindupButParryDoes()
        {
            Call(arena.GetComponent(T("PrototypeCombatTrial")), "SetEnemyTiming", true);
            yield return null;
            Place(); Assert.That(Call(ai, "BeginCaptureAttack"), Is.True);
            Assert.That(Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<int>(health, "HitCount") == 1);
            Assert.That(Get<int>(health, "CurrentHp"), Is.EqualTo(110));
            Assert.That(Get<string>(health, "LastHitReaction"), Is.EqualTo("AttackArmor"));
            Assert.That(Get<string>(art, "MotionName"), Is.EqualTo("Windup"));
            Until(() => Get<bool>(ai, "IsParryCueActive"));
            Assert.That(Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True);
            Until(() => Get<bool>(health, "IsParryStaggered"));
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(100));
            Assert.That(Get<bool>(ai, "ResistsHitStun"), Is.False);
            Assert.That(Get<int>(ai, "ActiveRemainingFrames"), Is.Zero);
            Assert.That(Get<int>(player.GetComponent(T("PlayerParryCounter2D")), "StockCount"), Is.EqualTo(1));
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
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(100),
                "A lethal hit must still cancel the committed windup before contact.");
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
