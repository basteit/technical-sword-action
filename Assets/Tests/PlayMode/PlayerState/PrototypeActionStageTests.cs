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
    public sealed class PrototypeActionStageTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Component clock, arena, player, motor;
        private bool manual;

        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            arena = (Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            player = (Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));
            motor = player.GetComponent(T("PlayerMotor2D"));
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled = false;
            Frames(2);
            // Disabling the real router intentionally blocks gameplay for the current render frame.
            yield return null;
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            if (arena != null) Call(arena, "ResetActionsOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual);
            yield return null;
        }

        [Test] public void StartsWithoutEnemiesAndDoesNotSpawnThemWhilePracticing()
        {
            Assert.That(Enemies(true).Length, Is.EqualTo(3), "Keep existing combat targets for explicit QA presets.");
            Frames(180);
            Assert.That(Enemies(false), Is.Empty);
        }

        [Test] public void ActionsOnlyResetClearsExplicitCombatPresetWithoutDeletingTargets()
        {
            for (int i = 0; i < 3; i++)
            {
                Call(arena, "ResetStrongEncounter", true);
                Assert.That(Enemies(false).Length, Is.EqualTo(2));
                Call(arena, "ResetActionsOnly");
                Frames(1);
                Assert.That(Enemies(false), Is.Empty);
                Assert.That(Get<int>(player.GetComponent(T("PlayerParryCounter2D")), "StockCount"), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            Call(arena, "ResetArena", false);
            Assert.That(Enemies(false).Length, Is.EqualTo(1), "Retain the single-normal combat preset API.");
        }

        [Test] public void UsesExistingVideoGridAndThreeGroundLayerPlatforms()
        {
            var grid = GameObject.Find("VideoWhiteGrid");
            Assert.That(grid, Is.Not.Null);
            var renderers = grid.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(renderers.Length, Is.EqualTo(60));
            foreach (var renderer in renderers)
            {
                Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Is.EqualTo("Assets/Prefabs/VideoStudio/StudioSolid.png"));
                Assert.That(renderer.sortingOrder, Is.LessThan(0));
            }
            var platforms = GameObject.Find("ActionPlatforms").GetComponentsInChildren<BoxCollider2D>();
            Assert.That(platforms.Length, Is.EqualTo(3));
            Assert.That(platforms.Count(x => x.usedByEffector), Is.EqualTo(2));
            foreach (var platform in platforms)
            {
                Assert.That(platform.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")));
                Assert.That(platform.isTrigger, Is.False);
                Assert.That(platform.bounds.max.y, Is.GreaterThan(0));
            }
            Assert.That(GameObject.Find("Ground").GetComponent<BoxCollider2D>().bounds.max.y, Is.EqualTo(0).Within(.001f));
        }

        [TestCase("OneWayFloor", -6f, 0f)]
        [TestCase("UpperOneWayFloor", -4.75f, 2.09375f)]
        public void HeldJumpReachesEachOneWayFloorFromThePreviousLevel(string name, float x, float startFeet)
        {
            Place(x, startFeet);
            Assert.That(Get<bool>(motor, "IsGrounded"), Is.True);
            Call(motor, "SetJumpHeld", true);
            Jump(); Frames(75);
            AssertStandingOn(name);
        }

        [Test] public void SolidStepCanBeReachedByJumpingFromAdjacentGround()
        {
            Place(-6.25f, 0);
            Call(motor, "SetJumpHeld", true);
            Jump(); Frames(12);
            Call(motor, "SetMoveInput", Vector2.left);
            Frames(40);
            Call(motor, "SetMoveInput", Vector2.zero);
            Frames(60);
            AssertStandingOn("SolidStep");
        }

        [Test] public void DownJumpDropsToSafeGroundAndRestoresFloorCollision()
        {
            Place(-6, 2.09375f);
            AssertStandingOn("OneWayFloor");
            Call(motor, "SetMoveInput", Vector2.down);
            Call(motor, "SetJumpDownInput", true);
            Jump(); Frames(1);
            Assert.That(Get<bool>(motor, "IsDroppingThrough"), Is.True);
            Frames(60);
            Assert.That(Get<bool>(motor, "IsGrounded"), Is.True);
            Assert.That(Get<bool>(motor, "IsDroppingThrough"), Is.False);
            Assert.That(player.GetComponent<Collider2D>().bounds.min.y, Is.EqualTo(0).Within(.06f));
            Call(motor, "SetMoveInput", Vector2.zero);
            Call(motor, "SetJumpHeld", true);
            Jump(); Frames(75);
            AssertStandingOn("OneWayFloor");
        }

        private void Place(float x, float feet)
        {
            var body = player.GetComponent<Rigidbody2D>();
            body.position = new Vector2(x, feet + .81f);
            player.transform.position = body.position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); Frames(2);
        }
        private void AssertStandingOn(string name)
        {
            var platform = GameObject.Find(name).GetComponent<BoxCollider2D>();
            Assert.That(Get<bool>(motor, "IsGrounded"), Is.True);
            Assert.That(player.GetComponent<Collider2D>().bounds.min.y, Is.EqualTo(platform.bounds.max.y).Within(.06f));
        }
        private void Jump() => Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Jump), Is.True);
        private Object[] Enemies(bool inactive) => Object.FindObjectsByType(T("EnemyMeleeChaser2D"),
            inactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        private void Frames(int count) { for (int i = 0; i < count; i++) Call(clock, "AdvanceFrame", 1d / 60d); }
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static TValue Get<TValue>(object target, string name) => (TValue)target.GetType().GetProperty(name, Flags).GetValue(target);
    }
}
