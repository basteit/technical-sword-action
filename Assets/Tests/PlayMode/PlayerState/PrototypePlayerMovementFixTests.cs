using System;
using System.Collections;
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
    public sealed class PrototypePlayerMovementFixTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, player, motor, parry, arena, presentation;
        private Rigidbody2D body;
        private bool manual;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static V Get<V>(object target, string name) => (V)target.GetType().GetProperty(name, Flags).GetValue(target);
        private void Frames(int count) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            motor = player.GetComponent(T("PlayerMotor2D")); parry = player.GetComponent(T("PlayerParry2D"));
            presentation = player.GetComponent(T("PrototypePlayerArtPresentation"));
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            body = player.GetComponent<Rigidbody2D>();
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            Frames(2); yield return null;
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            T("CombatTimeController").GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { false });
            if (arena != null) Call(arena, "ResetActionsOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual);
            yield return null;
        }

        [TestCase(false, -1)] [TestCase(false, 1)]
        [TestCase(true, -1)] [TestCase(true, 1)]
        public void HeldMovementStopsThroughoutParryAndMissThenResumes(bool airborne, int direction)
        {
            Place(0, airborne ? 4.8f : .81f);
            if (airborne) body.linearVelocity = new Vector2(direction * 4, -1);
            Call(motor, "SetMoveInput", Vector2.right * direction);
            Vector2 start = body.position; float gravity = body.gravityScale;
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True);
            Frames(1);
            Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.Parry));
            Assert.That(Get<bool>(player, "BlocksStandardMovement"), Is.True, "The ready/acceptance phase must own horizontal velocity too.");
            int limit = 60; bool sawFail = false;
            while (Get<PlayerActionState>(player, "ActionState") != PlayerActionState.Neutral && --limit > 0)
            {
                sawFail |= Get<PlayerActionState>(player, "ActionState") == PlayerActionState.ParryFail;
                Assert.That(body.position.x, Is.EqualTo(start.x).Within(.001f));
                Assert.That(body.linearVelocity.x, Is.Zero.Within(.001f));
                Assert.That(body.gravityScale, Is.EqualTo(gravity));
                Frames(1);
            }
            Assert.That(limit, Is.GreaterThan(0)); Assert.That(sawFail, Is.True);
            Assert.That(Get<int>(parry, "MissCount"), Is.EqualTo(1));
            Assert.That(Get<float>(parry, "ParryCooldownRemaining") * 60, Is.EqualTo(14).Within(.001f));
            if (airborne)
            {
                Assert.That(body.position.y, Is.LessThan(start.y));
                Assert.That(body.linearVelocity.y, Is.LessThan(-1), "Parry must not freeze or reset falling.");
            }
            Frames(1);
            Assert.That((body.position.x - start.x) * direction, Is.GreaterThan(.02f));
            Assert.That(Get<bool>(player, "BlocksStandardMovement"), Is.False);
        }

        [TestCase(5, "Just")] [TestCase(9, "Normal")]
        public void SuccessfulParryKeepsHeldMovementStoppedOnlyForItsExistingLock(int contactFrame, string result)
        {
            Place(0, .81f); Call(motor, "SetMoveInput", Vector2.right);
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True);
            Frames(contactFrame);
            var args = new object[] { body.position + Vector2.right * .6f, null };
            var resolve = parry.GetType().GetMethod("TryResolveParry", new[] { typeof(Vector2), T("ParryResult").MakeByRefType() });
            Assert.That((bool)resolve.Invoke(parry, args), Is.True); Assert.That(args[1].ToString(), Is.EqualTo(result));
            float x = body.position.x; int limit = 60;
            while (Get<PlayerActionState>(player, "ActionState") != PlayerActionState.Neutral && --limit > 0)
            {
                Assert.That(Get<bool>(player, "BlocksStandardMovement"), Is.True);
                Assert.That(body.position.x, Is.EqualTo(x).Within(.001f)); Frames(1);
            }
            Assert.That(limit, Is.GreaterThan(0));
            Assert.That(Get<float>(parry, "ParryCooldownRemaining") * 60, Is.EqualTo(14).Within(.001f));
            Frames(1); Assert.That(body.position.x, Is.GreaterThan(x + .02f));
        }

        [TestCase(false)] [TestCase(true)]
        public void AllWalkCelsHideGuideWithoutEditingSharedTextureOrShiftingFeet(bool mirrored)
        {
            Place(0, .81f);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Submitted/PlayerPrototype_Move.anim");
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var sprites = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(k => (Sprite)k.value).Distinct().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(8));
            var source = player.GetComponent<SpriteRenderer>(); var artwork = Get<SpriteRenderer>(presentation, "Artwork");
            var camera = Camera.main; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                foreach (var sprite in sprites)
                {
                    source.sprite = sprite; source.flipX = mirrored; Call(presentation, "RefreshVisual");
                    var displayed = artwork.sprite;
                    Assert.That(displayed, Is.Not.SameAs(sprite), sprite.name + " needs a prototype-only display crop.");
                    Assert.That(displayed.texture, Is.SameAs(sprite.texture), "Do not edit/copy the artist's pixels.");
                    Assert.That(displayed.rect, Is.EqualTo(new Rect(sprite.rect.x, sprite.rect.y + 6, sprite.rect.width, sprite.rect.height - 6)));
                    Assert.That(displayed.pivot + Vector2.up * 6, Is.EqualTo(sprite.pivot));
                    Assert.That(artwork.bounds.min.y, Is.EqualTo(player.GetComponent<Collider2D>().bounds.min.y).Within(.001f));
                    camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 guideWorld = artwork.transform.TransformPoint(new Vector3(side * .9f, (5.5f - sprite.pivot.y) / sprite.pixelsPerUnit));
                        Vector3 probe = camera.WorldToScreenPoint(guideWorld);
                        Color pixel = pixels.GetPixel(Mathf.FloorToInt(probe.x), Mathf.FloorToInt(probe.y));
                        Assert.That(pixel.r + pixel.g + pixel.b, Is.GreaterThan(.5f), sprite.name + " still draws a black floor guide.");
                    }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(pixels);
            }
        }

        [UnityTest] public IEnumerator OneEnemyPracticeSupportsRealParryCounterAndNoEnemyReset()
        {
            Call(arena, "ResetArena", false); yield return null;
            var enemies = Object.FindObjectsByType(T("EnemyMeleeChaser2D"), FindObjectsSortMode.None);
            Assert.That(enemies.Length, Is.EqualTo(1)); var enemy = (Component)enemies[0];
            body.position = new Vector2(enemy.transform.position.x - 1.15f, .81f);
            player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            Call(motor, "SetFacing", 1); Physics2D.SyncTransforms();
            Assert.That((bool)Call(enemy, "BeginCaptureAttack"), Is.True);
            // Both the old 30F and the reference 96F telegraph must be tested against actual contact.
            int windupFrames = Mathf.RoundToInt(Get<float>(enemy, "WindupDuration") * 60);
            Frames(Mathf.Max(0, windupFrames - 10));
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True); Frames(14);
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")), "CurrentHp"), Is.EqualTo(100));
            var counter = player.GetComponent(T("PlayerParryCounter2D"));
            Assert.That(Get<int>(counter, "StockCount"), Is.EqualTo(1));
            Frames(10); Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            int limit = 180;
            while (Get<int>(counter, "HitCount") == 0 && --limit > 0) Frames(1);
            Assert.That(limit, Is.GreaterThan(0));
            Assert.That(Get<int>(counter, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(enemy.GetComponent(T("Damageable2D")), "IsAlive"), Is.False);
            Call(arena, "ResetActionsOnly");
            Assert.That(Object.FindObjectsByType(T("EnemyMeleeChaser2D"), FindObjectsSortMode.None), Is.Empty);
            Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
        }

        private void Place(float x, float y)
        {
            body.position = new Vector2(x, y); player.transform.position = body.position; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); Frames(2);
        }
    }
}
