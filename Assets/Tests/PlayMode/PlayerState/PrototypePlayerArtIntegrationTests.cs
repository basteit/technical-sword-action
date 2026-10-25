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
    public sealed class PrototypePlayerArtIntegrationTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, player, arena, presentation;
        private SpriteRenderer source, artwork;
        private bool manual;
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
        private static V Get<V>(object target, string name) => (V)(target.GetType().GetProperty(name, Flags)?.GetValue(target) ?? target.GetType().GetField(name, Flags).GetValue(target));
        private void Frames(int count) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }
        private void Refresh() => Call(presentation, "RefreshVisual");

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            presentation = player.GetComponent(T("PrototypePlayerArtPresentation"));
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            source = player.GetComponent<SpriteRenderer>();
            artwork = Get<SpriteRenderer>(presentation, "Artwork");
            Frames(2); yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            T("CombatTimeController").GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { false });
            if (arena != null) Call(arena, "ResetActionsOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual);
            yield return null;
        }

        [Test] public void SceneUsesNewArtAndOptInCounterWithoutChangingCollider()
        {
            Assert.That(presentation, Is.Not.Null); Refresh();
            Assert.That(player.GetComponent<Animator>().runtimeAnimatorController, Is.TypeOf<AnimatorOverrideController>());
            Assert.That(AssetDatabase.GetAssetPath(source.sprite), Does.Contain("IdleReference"));
            Assert.That(source.forceRenderingOff, Is.True); Assert.That(artwork.enabled, Is.True);
            Assert.That(artwork.sprite, Is.SameAs(source.sprite));
            var collider = player.GetComponent<BoxCollider2D>();
            Assert.That(collider.size, Is.EqualTo(new Vector2(.65f, 1.6f)));
            Assert.That(collider.offset, Is.EqualTo(Vector2.zero));
            Assert.That(artwork.transform.localPosition.y, Is.EqualTo(.2f).Within(.00001f));
            Assert.That(Get<object>(player.GetComponent(T("PlayerParryCounter2D")), "CinematicProfile"), Is.Not.Null);
            Assert.That(Get<bool>(player.GetComponent(T("PlayerParryCounter2D")), "IsAirborneKick"), Is.True);
            Assert.That(Object.FindObjectsByType(T("EnemyMeleeChaser2D"), FindObjectsSortMode.None), Is.Empty);
        }

        [Test] public void DrawingCopiesTintAndFacingAndDisablingRestoresTheOriginalRenderer()
        {
            Color original = source.color;
            source.color = new Color(.8f, .3f, .4f, .6f); source.flipX = true; Refresh();
            Assert.That(artwork.color, Is.EqualTo(source.color)); Assert.That(artwork.flipX, Is.True);
            ((Behaviour)presentation).enabled = false;
            Assert.That(source.forceRenderingOff, Is.False); Assert.That(artwork.enabled, Is.False);
            ((Behaviour)presentation).enabled = true;
            source.color = original; source.flipX = false; Refresh();
            Assert.That(source.forceRenderingOff, Is.True); Assert.That(artwork.enabled, Is.True);
        }

        [Test] public void JumpSampleNormalizesDrawingOnlyAndKeepsPixelsAndFeetAligned()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Player_jamp_up_sample.aseprite").OfType<Sprite>().Single();
            var position = player.transform.position; var size = player.GetComponent<BoxCollider2D>().size;
            source.sprite = sprite; Refresh();
            Assert.That(artwork.transform.localScale.x, Is.EqualTo(100f / 32).Within(.00001f));
            Assert.That(artwork.transform.localPosition.y, Is.EqualTo(-.8f).Within(.00001f));
            Assert.That(artwork.bounds.min.y, Is.EqualTo(player.GetComponent<Collider2D>().bounds.min.y).Within(.001f));
            Assert.That(player.transform.position, Is.EqualTo(position)); Assert.That(player.GetComponent<BoxCollider2D>().size, Is.EqualTo(size));
        }

        [Test] public void PauseAndResetDoNotLeaveASubmittedPoseOrHiddenBody()
        {
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Frames(10); Refresh();
            var sprite = source.sprite;
            var timeType = T("CombatTimeController");
            timeType.GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { true });
            Frames(20); Refresh(); Assert.That(source.sprite, Is.SameAs(sprite));
            timeType.GetMethod("SetPaused", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { false });
            Call(arena, "ResetActionsOnly"); Frames(2); Refresh();
            Assert.That(AssetDatabase.GetAssetPath(artwork.sprite), Does.Contain("IdleReference"));
            Assert.That(artwork.enabled, Is.True); Assert.That(source.forceRenderingOff, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test] public void EveryAttackShowsTheContactCelOnItsDamageAndHitstopTick()
        {
            var target = new GameObject("ArtworkHitstopTarget"); target.SetActive(false);
            target.layer = LayerMask.NameToLayer("Enemy"); target.AddComponent<BoxCollider2D>().isTrigger = true;
            var hp = target.AddComponent(T("Damageable2D"));
            hp.GetType().GetField("applyPresetOnAwake", Flags).SetValue(hp, false);
            hp.GetType().GetField("knockbackPower", Flags).SetValue(hp, 0f);
            hp.GetType().GetField("blockContactPushFromPlayer", Flags).SetValue(hp, false);
            target.SetActive(true); Call(hp, "ResetForCapture", player.transform.position + Vector3.right, 1000);
            var attack = player.GetComponent(T("PlayerAttack2D"));
            var set = Get<object>(presentation, "ArtSet");
            try
            {
                for (int step = 1; step <= 4; step++)
                {
                    Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
                    int limit = 180;
                    while (Get<int>(hp, "HitCount") < step && --limit > 0)
                    {
                        target.transform.position = player.transform.position + Vector3.right;
                        Physics2D.SyncTransforms(); Frames(1);
                    }
                    Assert.That(limit, Is.GreaterThan(0)); Refresh();
                    var entry = Call(set, "Find", "Attack_" + step);
                    var contact = (Sprite)entry.GetType().GetField("contactSprite").GetValue(entry);
                    Assert.That(artwork.sprite, Is.SameAs(contact), "Step " + step + ": do not freeze the windup pose on damage.");
                    var frozen = artwork.sprite; Frames(1); Refresh();
                    Assert.That(artwork.sprite, Is.SameAs(frozen), "Hitstop freezes the contact cel.");
                    while (Get<object>(attack, "Phase").ToString() != "Cancel" && --limit > 0) Frames(1);
                    Assert.That(limit, Is.GreaterThan(0));
                }
                Frames(90);
                Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
                Assert.That(Get<int>(hp, "HitCount"), Is.EqualTo(4));
                Assert.That(Get<int>(hp, "CurrentHp"), Is.EqualTo(940));
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test] public void CinematicCounterShowsContactWithoutVideoDirectorOrChangingDamage()
        {
            var target = new GameObject("ArtworkCounterTarget"); target.SetActive(false);
            target.layer = LayerMask.NameToLayer("Enemy"); target.AddComponent<BoxCollider2D>().isTrigger = true;
            var hp = target.AddComponent(T("Damageable2D"));
            hp.GetType().GetField("applyPresetOnAwake", Flags).SetValue(hp, false);
            hp.GetType().GetField("knockbackPower", Flags).SetValue(hp, 0f);
            hp.GetType().GetField("blockContactPushFromPlayer", Flags).SetValue(hp, false);
            target.SetActive(true); Call(hp, "ResetForCapture", player.transform.position + Vector3.right, 1000);
            var counter = player.GetComponent(T("PlayerParryCounter2D"));
            var start = player.transform.position;
            try
            {
                Call(counter, "RegisterParriedEnemy", hp);
                Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
                int limit = 120;
                while (Get<int>(hp, "HitCount") == 0 && --limit > 0) Frames(1);
                Assert.That(limit, Is.GreaterThan(0)); Refresh();
                var entry = Call(Get<object>(presentation, "ArtSet"), "Find", "ParryCounter");
                var animation = player.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
                Assert.That(artwork.sprite, Is.SameAs((Sprite)entry.GetType().GetField("contactSprite").GetValue(entry)),
                    $"Counter clock {Get<int>(counter, "ElapsedFrames")}, animation {animation.normalizedTime * animation.length * 60:0.000}F.");
                Assert.That(Get<int>(hp, "CurrentHp"), Is.EqualTo(910));
                Assert.That(Get<int>(counter, "StockCount"), Is.Zero);
                Assert.That(Get<bool>(counter, "IsAirborneKick"), Is.True);
                Assert.That(Get<bool>(counter, "UsesBlinkApproach"), Is.True);
                Assert.That(player.GetComponent(T("VideoCombatPresentation")), Is.Null);
                Assert.That(player.GetComponent(T("CombatVideoDirector")), Is.Null);
                Assert.That(Mathf.Abs(player.transform.position.y - start.y), Is.LessThan(.1f));
                Frames(90); Assert.That(Get<PlayerActionState>(player, "ActionState"), Is.EqualTo(PlayerActionState.Neutral));
            }
            finally { Object.DestroyImmediate(target); }
        }
    }
}
