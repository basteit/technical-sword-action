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
            Assert.That(Get<bool>(feedback, "HasCameraFocus"), Is.False);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Assert.That(Get<string>(presentation,"CounterLightPhase"),Is.EqualTo("None"));
            Assert.That(Get<float>(feedback,"ImpactZoom"),Is.Zero);
            Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsParryActorJitterActive"),Is.False);
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsParryCameraActive"),Is.False);
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
            Assert.That((body.position.x - target.transform.position.x) * direction, Is.EqualTo(2.125f).Within(.02f));
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
            long tick = TimeValue<long>("TickCount"); Frames(1); Assert.That(TimeValue<long>("TickCount"), Is.EqualTo(tick), "The contact stop must freeze motion and the impact cel.");
            Until(() => !Get<bool>(counter, "IsActive")); Until(() => !Get<bool>(launch, "IsActive")); Frames(6);
            Assert.That(Get<float>(launch, "TravelDistance"), Is.EqualTo(3.2f).Within(.02f));
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(30)); Assert.That(Get<int>(target, "HitCount"), Is.EqualTo(1));
            Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.Zero);
            Assert.That(body.gravityScale, Is.EqualTo(gravity)); AssertClean();
        }

        [TestCase(1)] [TestCase(10)]
        public void BufferedCounterConsumesParryPoseWithoutReplayingSuccessAfterward(int parryFrame)
        {
            body.position = new Vector2(0, .81f); player.transform.position = body.position;
            var target = Enemy("StrongMelee", new Vector2(1.15f, .81f), 120);
            var parry = player.GetComponent(T("PlayerParry2D"));
            var receiver = player.GetComponent(T("PlayerDamageReceiver2D"));
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Parry), Is.True);
            Until(() => Get<int>(parry, "ElapsedFrames") >= parryFrame);
            object context = Activator.CreateInstance(T("CombatHitContext2D"), target, 91L, true,
                Enum.Parse(T("ParryReactionWeight"), "Light"));
            Call(receiver, "TryReceiveContextHit", 10, (Vector2)target.transform.position, 0f, context);
            Assert.That(Get<object>(parry, "LastParryResult").ToString(), Is.EqualTo(parryFrame == 1 ? "Just" : "Normal"));
            Until(() => !TimeValue<bool>("IsHitStopped"));
            Assert.That((bool)Call(player, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Until(() => Get<bool>(counter, "IsActive"));
            Until(() => !Get<bool>(counter, "IsActive"));
            var animator = player.GetComponent<Animator>();
            for (int i = 0; i < 18; i++)
            {
                Frames(1); animator.Update(0);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(state.IsName("PlayerPrototype_ParrySuccessLight") || state.IsName("PlayerPrototype_ParrySuccessHeavy") ||
                    state.IsName("PlayerPrototype_ParrySuccess") || state.IsName("PlayerPrototype_Recovery_Parry"), Is.False);
            }
        }

        [TestCase(1)] [TestCase(-1)]
        public void ImpactKeepsItsWorldContactAndFacingWhenPlayerMovesAndTurns(int direction)
        {
            var effects = player.GetComponent(T("VideoVfxRenderer"));
            effects.GetType().GetProperty("UseAuthoredCounterEffect").SetValue(effects, false);
            Begin(direction); ToFrame(21);
            var profile = Get<object>(counter, "CinematicProfile");
            var motion = Call(Get<object>(profile, "art"), "Find", "Fx_ElectricKick");
            var sprites = Get<Sprite[]>(motion, "frames");
            var impact = effects.GetComponentsInChildren<SpriteRenderer>().Single(r => sprites.Contains(r.sprite) && r.name == "PooledVideoVfx");
            Vector3 contact = Get<Vector2>(counter, "ContactPoint");
            Assert.That(Vector3.Distance(impact.transform.position, contact), Is.LessThan(.0001f));
            Vector3 local = new Vector3(.4f, .2f);
            Vector3 expected = contact + new Vector3(.4f * -direction, .2f) * 1.15f;
            Assert.That(Vector3.Distance(impact.transform.TransformPoint(local), expected), Is.LessThan(.0001f));
            body.position += Vector2.right * 3; player.transform.position = body.position;
            Call(motor, "SetFacing", direction); Call(effects, "AdvanceVisuals", 0f);
            Assert.That(Vector3.Distance(impact.transform.position, contact), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(impact.transform.TransformPoint(local), expected), Is.LessThan(.0001f));
        }

        [TestCase(1)] [TestCase(-1)]
        public void SeparateCounterEffectTracksTheDisplayedArtworkAndClearsOnReset(int direction)
        {
            Begin(direction); ToFrame(14);
            var effects = player.GetComponent(T("VideoVfxRenderer"));
            var drawing = Get<SpriteRenderer>(art, "Artwork");
            var effect = effects.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "AuthoredCounterEffect");
            Assert.That(effect.sprite.name, Is.EqualTo("CounterKickEffect_02"));
            Assert.That(Vector3.Distance(effect.transform.TransformPoint(new Vector3(.65f, .35f)), drawing.transform.TransformPoint(new Vector3(.65f, .35f))), Is.LessThan(.0001f));
            Assert.That(effect.flipX, Is.EqualTo(drawing.flipX));
            Assert.That(effect.sortingOrder, Is.EqualTo(1009), "Keep authored electricity visible above the dimmer and below the lifted actor.");
            var sprite = effect.sprite; Static("SetPaused", true); Frames(60);
            Assert.That(effect.sprite, Is.SameAs(sprite));
            Static("SetPaused", false); ToFrame(21);
            Assert.That(effect.sprite.name, Is.EqualTo("CounterKickEffect_09"));
            sprite = effect.sprite; Frames(3);
            Assert.That(effect.sprite, Is.SameAs(sprite));
            Assert.That(effects.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith("Fx_ElectricKick_")), Is.False);
            Call(arena, "ResetActionsOnly"); Frames(6);
            Assert.That(Get<int>(presentation, "ActiveEffectCount"), Is.Zero); AssertClean();
        }

        [TestCase(1, "Afterimage")] [TestCase(-1, "Afterimage")]
        [TestCase(1, "TransitSilhouette")] [TestCase(-1, "TransitSilhouette")]
        [TestCase(1, "EyeTrail")] [TestCase(-1, "EyeTrail")]
        public void SpriteEffectsCopyTheDisplayedArtPoseOnce(int direction, string kind)
        {
            Call(motor, "SetFacing", direction);
            var profile = Get<object>(counter, "CinematicProfile");
            player.GetComponent<SpriteRenderer>().sprite = Get<Sprite[]>(Get<object>(profile, "Kick"), "frames")[0];
            Call(art, "RefreshVisual");
            var drawing = Get<SpriteRenderer>(art, "Artwork");
            var effects = player.GetComponent(T("VideoVfxRenderer")); Call(effects, "ResetEffects");
            if (kind == "TransitSilhouette") Call(effects, "EmitTransitSilhouette", drawing, direction, (Vector2)drawing.transform.position, Vector2.one, .45f);
            else Call(effects, "Emit" + kind, drawing, direction);
            var image = effects.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "PooledVideoVfx");
            var sample = new Vector3(.65f, .35f);
            Vector3 expected = drawing.transform.TransformPoint(sample);
            Assert.That(Vector3.Distance(image.transform.TransformPoint(sample), expected), Is.LessThan(.0001f));
            body.position += Vector2.right * 3; player.transform.position = body.position;
            Call(motor, "SetFacing", -direction); Call(effects, "AdvanceVisuals", 0f);
            Assert.That(Vector3.Distance(image.transform.TransformPoint(sample), expected), Is.LessThan(.0001f));
        }

        [TestCase(1)] [TestCase(-1)]
        public void CounterCloseUpFramesTheHeroFreezesOnPauseAndReturnsToTheOriginalCamera(int direction)
        {
            Set(Get<object>(feedback, "Settings"), "shake", 1f);
            var camera = Camera.main; Vector3 original = camera.transform.position;
            Begin(direction); ToFrame(14);
            var profile = Get<object>(counter, "CinematicProfile");
            Assert.That(camera.orthographicSize, Is.EqualTo(Get<float>(profile, "cameraOrthographicSize")).Within(.00001f));
            Vector2 offset = Get<Vector2>(profile, "cameraFocusOffset");
            Vector3 focus = player.transform.position + new Vector3(offset.x * -direction, offset.y);
            focus.z = original.z;
            Assert.That(Vector3.Distance(camera.transform.position - Get<Vector3>(feedback, "ShakeOffset"), focus), Is.LessThan(.0001f));
            var viewport = camera.WorldToViewportPoint(player.transform.position);
            Assert.That(viewport.x, Is.InRange(.3f, .7f)); Assert.That(viewport.y, Is.InRange(.2f, .8f));
            Vector3 paused = camera.transform.position; float size = camera.orthographicSize;
            Static("SetPaused", true); Frames(60);
            Assert.That(camera.transform.position, Is.EqualTo(paused)); Assert.That(camera.orthographicSize, Is.EqualTo(size));
            Static("SetPaused", false); Until(() => !Get<bool>(counter, "IsActive")); Frames(8);
            Assert.That(Vector3.Distance(camera.transform.position, original), Is.LessThan(.0001f)); AssertClean();
        }

        [TestCase("Reset")] [TestCase("Disable")] [TestCase("Death")] [TestCase("TargetLost")]
        public void CounterCloseUpImmediatelyRestoresOnInterruption(string operation)
        {
            Set(Get<object>(feedback, "Settings"), "shake", 1f);
            var camera = Camera.main; Vector3 original = camera.transform.position;
            var target = Begin(); ToFrame(14);
            Assert.That(Get<bool>(feedback, "HasCameraFocus"), Is.True);
            if (operation == "Reset") Call(arena, "ResetActionsOnly");
            else if (operation == "Disable") { player.gameObject.SetActive(false); player.gameObject.SetActive(true); }
            else if (operation == "Death") { Call(player, "SetDead", "CounterCameraTest"); Call(player, "Revive", "CounterCameraTest"); }
            else { target.gameObject.SetActive(false); Until(() => !Get<bool>(counter, "IsActive")); }
            Assert.That(Get<bool>(feedback, "HasCameraFocus"), Is.False);
            Assert.That(camera.orthographicSize, Is.EqualTo(5.625f));
            Assert.That(Vector3.Distance(camera.transform.position - Get<Vector3>(feedback, "ShakeOffset"), original), Is.LessThan(.0001f));
            Frames(8); AssertClean();
        }

        [Test] public void DisablingCameraMotionDuringChargeRestoresTheViewWithoutChangingTheCounter()
        {
            Set(Get<object>(feedback, "Settings"), "shake", 1f);
            var camera = Camera.main; Vector3 original = camera.transform.position;
            var target = Begin(); ToFrame(14);
            Assert.That(camera.orthographicSize, Is.EqualTo(3.75f).Within(.00001f));
            Set(Get<object>(feedback, "Settings"), "shake", 0f); Frames(1);
            Assert.That(camera.orthographicSize, Is.EqualTo(5.625f));
            Assert.That(Vector3.Distance(camera.transform.position, original), Is.LessThan(.0001f));
            Until(() => !Get<bool>(counter, "IsActive")); Frames(6);
            Assert.That(Get<int>(target, "CurrentHp"), Is.EqualTo(30)); AssertClean();
        }

        [TestCase(1)] [TestCase(-1)]
        public void DefeatedEnemyRemainsVisibleForLaunchThenLeavesCombat(int direction)
        {
            var target = Begin(direction, false, 30); ToFrame(21);
            Assert.That(Get<bool>(target, "IsAlive"), Is.False); Assert.That(target.gameObject.activeSelf, Is.True);
            Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.GreaterThan(0));
            var launch = target.GetComponent(T("EnemyCounterLaunch2D")); Until(() => !TimeValue<bool>("IsHitStopped")); Frames(12);
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

        [Test] public void ParryStaggerSwaysUntilExpiryWithoutMovingColliderOrLaunchVisual()
        {
            var target = Enemy("StrongMelee", new Vector2(1.15f, .81f), 120);
            var ai = target.GetComponent(T("EnemyMeleeChaser2D")); ((Behaviour)ai).enabled = false;
            var cue = target.GetComponent(T("EnemyCombatCue2D"));
            var silhouette = Get<Transform>(cue, "silhouette"); var weapon = Get<Transform>(cue, "weapon");
            var launchVisual = target.transform.Find("CounterLaunchVisual");
            var rootPosition = target.transform.position; var launchRotation = launchVisual.localRotation;
            Call(target, "ApplyParryStun", Enum.Parse(T("ParryResult"), "Normal"));
            var first = silhouette.localRotation; var sword = weapon.localRotation;
            Frames(6);
            Assert.That(Quaternion.Angle(silhouette.localRotation, first), Is.GreaterThan(1));
            Assert.That(Quaternion.Angle(weapon.localRotation, sword), Is.GreaterThan(1));
            Assert.That(target.transform.position, Is.EqualTo(rootPosition));
            Assert.That(launchVisual.localRotation, Is.EqualTo(launchRotation));
            Frames(173); Assert.That(Get<bool>(target, "IsParryStaggered"), Is.True);
            Frames(1); Assert.That(Get<bool>(target, "IsParryStaggered"), Is.False);
            Assert.That(Quaternion.Angle(silhouette.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Call(target, "RefreshParryStagger"); Frames(6);
            target.gameObject.SetActive(false);
            Assert.That(Quaternion.Angle(silhouette.localRotation, Quaternion.identity), Is.LessThan(.001f));
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

        private void RenderAt(int fps)
        {
            Call(clock,"AdvanceFrame",1d/fps);
            Call(presentation,"AdvanceVisuals",1f/fps);Call(feedback,"AdvanceVisuals",1f/fps);
        }
        private Component BeginActualParry(int direction,bool just)
        {
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            Call(motor,"SetFacing",direction);Call(motor,"SetMoveInput",Vector2.zero);
            var target=Enemy("StrongMelee",body.position+Vector2.right*direction*1.15f,120);
            Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);Frames(just?1:10);
            int impacts=Get<int>(presentation,"ParryImpactCount");
            var context=Activator.CreateInstance(T("CombatHitContext2D"),target,987L,true);
            Call(player.GetComponent(T("PlayerDamageReceiver2D")),"TryReceiveContextHit",1,(Vector2)target.transform.position,0f,context);
            Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts+1));
            Assert.That(Get<long>(presentation,"ParryImpactTick"),Is.EqualTo(TimeValue<long>("TickCount")));
            return target;
        }
        [TestCase(30,1,false)] [TestCase(60,1,false)] [TestCase(120,1,false)]
        [TestCase(30,-1,true)] [TestCase(60,-1,true)] [TestCase(120,-1,true)]
        public void GameplayParryUsesFiniteSoftBacklightDuringThirtyFrameImpact(int fps,int direction,bool just)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);var target=BeginActualParry(direction,just);
            RenderAt(fps);var profile=Get<object>(counter,"CinematicProfile");
            Assert.That(player.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get<Sprite>(profile,"parryContactSprite")));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.InRange(30-Mathf.CeilToInt(60f/fps),30));
            Assert.That(Get<int>(feedback,"ActiveEffectCount"),Is.Zero,"The authored spark replaces the procedural glyph.");
            Assert.That(Get<Vector3>(feedback,"ShakeOffset").magnitude,Is.InRange(.24f,.38f));
            long tick=TimeValue<long>("TickCount");var sprite=player.GetComponent<SpriteRenderer>().sprite;int lit=0;
            while(Get<bool>(presentation,"IsParryBacklit")&&lit<fps)
            {lit++;RenderAt(fps);Assert.That(TimeValue<long>("TickCount"),Is.EqualTo(tick));Assert.That(player.GetComponent<SpriteRenderer>().sprite,Is.SameAs(sprite));}
            Assert.That(lit,Is.EqualTo(Mathf.CeilToInt(fps*Get<float>(profile,"ParryLightSeconds"))));
            Frames(50);Assert.That(Get<int>(target,"HitCount"),Is.Zero);AssertClean();
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,-1)] [TestCase(60,-1)] [TestCase(120,-1)]
        public void GameplayCounterSharesRimPulseImpactLightAndStrongCameraWithoutChangingDamage(int fps,int direction)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);var target=Begin(direction);
            float low=1,high=0;int samples=0;
            for(int i=0;i<fps*5&&Get<int>(target,"HitCount")==0;i++)
            {
                RenderAt(fps);
                if(Get<string>(presentation,"CounterLightPhase")=="Charge")
                {float pulse=Get<float>(presentation,"CounterRimPulse");low=Mathf.Min(low,pulse);high=Mathf.Max(high,pulse);samples++;}
            }
            Assert.That(samples,Is.GreaterThan(fps*.7f));Assert.That(low,Is.LessThan(.18f));Assert.That(high,Is.GreaterThan(.98f));
            Assert.That(Get<string>(presentation,"CounterLightPhase"),Is.EqualTo("Flash"));
            Assert.That(Get<Vector3>(feedback,"ShakeOffset").magnitude,Is.InRange(.42f,.54f));
            Assert.That(Get<int>(target,"CurrentHp"),Is.EqualTo(30));Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));
            var position=Camera.main.transform.position;int lightFrame=Get<int>(presentation,"CounterLightFrame");
            Static("SetPaused",true);for(int i=0;i<fps;i++)RenderAt(fps);
            Assert.That(Camera.main.transform.position,Is.EqualTo(position));Assert.That(Get<int>(presentation,"CounterLightFrame"),Is.EqualTo(lightFrame));
            Static("SetPaused",false);for(int i=0;i<Mathf.CeilToInt(fps*2/60f);i++)RenderAt(fps);
            Assert.That(Get<float>(feedback,"ImpactZoom"),Is.EqualTo(1).Within(.001f));Assert.That(Camera.main.orthographicSize,Is.LessThan(3.75f));
            Until(()=>!Get<bool>(counter,"IsActive"));Frames(30);AssertClean();
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("TargetLost")]
        public void ParryImpactClearsOnResetDisableOrTargetLossWithoutResurrecting(string operation)
        {
            var target=BeginActualParry(1,true);int count=Get<int>(presentation,"ParryImpactCount");
            if(operation=="Reset")Call(arena,"ResetActionsOnly");
            else if(operation=="Disable"){((Behaviour)presentation).enabled=false;((Behaviour)presentation).enabled=true;}
            else target.gameObject.SetActive(false);
            Frames(35);Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(count));AssertClean();
        }
        [UnityTest]
        public IEnumerator ActualParryAndEnhancedCounterRepeatTenTimesWithoutPresentationOrDamageResidue()
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);
            foreach(int direction in new[]{1,-1})
            for(int run=0;run<10;run++)
            {
                Call(arena,"ResetActionsOnly");yield return null;Frames(2);var target=BeginActualParry(direction,run%2==0);
                Until(()=>Get<PlayerActionState>(player,"ActionState")==PlayerActionState.Neutral);
                Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Attack),Is.True);Frames(1);
                Assert.That(Get<bool>(counter,"IsActive"),Is.True);
                Until(()=>!Get<bool>(counter,"IsActive"));Frames(30);
                Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));Assert.That(Get<int>(target,"CurrentHp"),Is.EqualTo(30));AssertClean();
                Call(arena,"ResetActionsOnly");Frames(2);Assert.That(Get<int>(presentation,"ActiveEffectCount"),Is.Zero);AssertClean();
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void CameraLocalLightsKeepThePrototypeEnemiesSlicedBodySizeAndFlashStrength(bool kick)
        {
            Set(Get<object>(feedback,"Settings"),"flash",.5f);
            Component target;
            if(kick){target=Begin();ToFrame(21);}
            else{target=BeginActualParry(1,true);Frames(2);Call(presentation,"AdvanceVisuals",0f);}
            var light=Camera.main.GetComponent(T(kick?"VideoCounterLighting":"VideoParryBacklight"));
            Call(light,"BeforeCamera",default(UnityEngine.Rendering.ScriptableRenderContext),Camera.main);
            try
            {
                var original=target.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="HeavySilhouette");
                var lit=target.GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name==(kick?"CounterLitEnemy":"ParryLitEnemy"));
                Assert.That(original.drawMode,Is.EqualTo(SpriteDrawMode.Sliced));
                Assert.That(lit.drawMode,Is.EqualTo(original.drawMode));Assert.That(lit.size,Is.EqualTo(original.size));
                Assert.That(lit.bounds.size,Is.EqualTo(original.bounds.size));
                Assert.That(lit.color.a,Is.EqualTo(kick ? .5f : .5f*Get<float>(Get<object>(counter,"CinematicProfile"),"parryActorLight")).Within(.001f));
                Assert.That(lit.sprite.texture,Is.SameAs(original.sprite.texture));Assert.That(lit.sprite.triangles.Length,Is.EqualTo(6));
                var cached=lit.sprite;Call(light,"BeforeCamera",default(UnityEngine.Rendering.ScriptableRenderContext),Camera.main);
                Assert.That(lit.sprite,Is.SameAs(cached),"Reuse the full-rect drawing instead of allocating every render.");
            }
            finally{Call(light,"AfterCamera",default(UnityEngine.Rendering.ScriptableRenderContext),Camera.main);}
        }
        [TestCase(1)] [TestCase(-1)]
        public void ParryAcceptsAHitOnItsFirstTickAndShowsTheImpactWithoutAVisualDelay(int direction)
        {
            body.position=new Vector2(0,.81f);player.transform.position=body.position;Call(motor,"SetFacing",direction);
            var target=Enemy("StrongMelee",body.position+Vector2.right*direction*1.15f,120);
            var parry=player.GetComponent(T("PlayerParry2D"));
            Assert.That(Get<int>(parry,"AcceptanceStartFrame"),Is.Zero);
            Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);Call(player,"CombatTick");
            Assert.That(Get<int>(parry,"ElapsedFrames"),Is.Zero);
            int impacts=Get<int>(presentation,"ParryImpactCount");var context=Activator.CreateInstance(T("CombatHitContext2D"),target,876L,true);
            Call(player.GetComponent(T("PlayerDamageReceiver2D")),"TryReceiveContextHit",1,(Vector2)target.transform.position,0f,context);
            Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts+1));
            Assert.That(Get<long>(presentation,"ParryImpactTick"),Is.EqualTo(TimeValue<long>("TickCount")));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));
            var profile=Get<object>(counter,"CinematicProfile");Assert.That(player.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get<Sprite>(profile,"parryContactSprite")));
            Frames(11);Assert.That(player.GetComponent<SpriteRenderer>().sprite.name,Is.Not.EqualTo("ParrySuccessLight_01"));
        }
        [Test] public void EmptyParryHoldsTheReadyPoseAndNeverPlaysTheSuccessImpact()
        {
            int impacts=Get<int>(presentation,"ParryImpactCount"),emissions=Get<int>(feedback,"EmissionCount");
            Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);Frames(1);
            var pose=Get<Sprite>(Get<object>(counter,"CinematicProfile"),"parryWaitSprite");int held=0;
            while(Get<PlayerActionState>(player,"ActionState")!=PlayerActionState.Neutral&&held++<40)
            {
                Assert.That(player.GetComponent<SpriteRenderer>().sprite,Is.SameAs(pose));
                Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts));
                Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);Assert.That(TimeValue<bool>("IsHitStopped"),Is.False);Step();
            }
            Assert.That(held,Is.InRange(25,28));
            Assert.That(Get<int>(feedback,"EmissionCount")-emissions,Is.LessThanOrEqualTo(1),"Only the existing miss cue may occur.");
        }
        [Test] public void ParryShockwaveGpuProjectionAndResetPixelsMatch()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("GPU rendering required.");
            var utility=Type.GetType("ParryShockwaveCaptureUtility, Assembly-CSharp-Editor",true);
            string report=(string)utility.GetMethod("ValidateProjection").Invoke(null,null);
            Assert.That(report,Does.Contain("\"mirroredCenterPixels\": 0"));
            Assert.That(report,Does.Contain("\"residualPixels\": 0"));
            Assert.That(report,Does.Contain("\"otherCameraChangedPixels\": 0"));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ParryShockwaveStartsAtTheAuthoredContactAndFreezesOnPause(int direction)
        {
            BeginActualParry(direction,true);
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.True);
            var sample=T("ParryShockwaveFeature").GetMethod("GetSample").Invoke(null,new object[]{Camera.main});
            var profile=Get<object>(counter,"CinematicProfile");var drawing=Get<Component>(art,"Artwork");
            Vector2 offset=Get<Vector2>(profile,"parryContactOffset");
            var expected=drawing.transform.TransformPoint(new Vector3(direction*offset.x,offset.y));
            Assert.That(Get<Vector3>(sample,"worldCenter"),Is.EqualTo(expected));
            var pulse=Get<object>(feedback,"parryShockwave");float age=Get<float>(pulse,"Elapsed");
            Static("SetPaused",true);Frames(20);
            Assert.That(Get<float>(pulse,"Elapsed"),Is.EqualTo(age));
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.True);
            Static("SetPaused",false);Frames(30);
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.False);
        }
        [TestCase(0f,1f)] [TestCase(.65f,0f)] [TestCase(.65f,1f)]
        public void ParryShockwaveIntensityNeverChangesTheSuccessfulParry(float shockwave,float vfx)
        {
            Set(Get<object>(feedback,"Settings"),"parryShockwave",shockwave);
            Set(Get<object>(feedback,"Settings"),"vfx",vfx);
            BeginActualParry(1,true);
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.EqualTo(shockwave*vfx>0));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));
            Assert.That(Get<long>(presentation,"ParryImpactTick"),Is.EqualTo(TimeValue<long>("TickCount")));
            Assert.That(player.GetComponent<SpriteRenderer>().sprite,Is.SameAs(Get<Sprite>(Get<object>(counter,"CinematicProfile"),"parryContactSprite")));
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("IntensityZero")]
        public void ParryShockwaveClearsWithoutAStaleRenderPass(string operation)
        {
            BeginActualParry(1,true);Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.True);
            if(operation=="Reset")Call(arena,"ResetActionsOnly");
            else if(operation=="Disable"){((Behaviour)feedback).enabled=false;((Behaviour)feedback).enabled=true;}
            else{Set(Get<object>(feedback,"Settings"),"parryShockwave",0f);Call(feedback,"AdvanceVisuals",0f);}
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.False);
            Frames(20);Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.False);
        }
        private Component BeginAuthoredAdaptiveParry(int direction,bool expectAdaptive=true,bool bufferAttackBeforeSuccess=false)
        {
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            Call(motor,"SetFacing",direction);Call(motor,"SetMoveInput",Vector2.zero);
            var target=Enemy("Melee_1",body.position+Vector2.right*direction*1.15f,120);
            var ai=target.GetComponent(T("EnemyMeleeChaser2D"));
            Assert.That((bool)Call(ai,"BeginCaptureAttack"),Is.True);
            Until(()=>Get<int>(ai,"WindupRemainingFrames")<=2);
            var parry=player.GetComponent(T("PlayerParry2D"));int successes=Get<int>(parry,"SuccessCount");
            Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Parry),Is.True);
            if(bufferAttackBeforeSuccess){Frames(1);Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Attack),Is.True);}
            Until(()=>Get<int>(parry,"SuccessCount")>successes);
            Assert.That(Get<bool>(presentation,"IsParryImpactPending"),Is.EqualTo(expectAdaptive));
            Assert.That(TimeValue<bool>("IsHitStopped"),Is.EqualTo(!expectAdaptive));
            return target;
        }
        [TestCase(1)] [TestCase(-1)]
        public void AdaptiveParryCanBeDisabledToRestoreThePreviousImmediateTenFramePresentation(int direction)
        {
            var profile=Get<object>(counter,"CinematicProfile");bool original=Get<bool>(profile,"synchronizedParryImpact");
            try
            {
                Set(profile,"synchronizedParryImpact",false);var target=BeginAuthoredAdaptiveParry(direction,false);
                Assert.That(Get<int>(presentation,"ParryImpactDelayFrames"),Is.Zero);
                Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(10));
                Assert.That(Get<string>(target.GetComponent(T("PrototypeEnemyArtPresentation")),"MotionName"),Is.EqualTo("Parried"));
                Assert.That(Get<bool>(presentation,"IsParrySequenceActive"),Is.False);
            }
            finally{Set(profile,"synchronizedParryImpact",original);}
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,-1)] [TestCase(60,-1)] [TestCase(120,-1)]
        public void AdaptiveParryMatchesTheEnemyMouthClosureAndDeflectsBeforeShowingStagger(int fps,int direction)
        {
            var target=BeginAuthoredAdaptiveParry(direction);
            var enemyArt=target.GetComponent(T("PrototypeEnemyArtPresentation"));
            var drawing=Get<SpriteRenderer>(enemyArt,"Artwork");
            int delay=Get<int>(presentation,"ParryImpactDelayFrames"),impacts=Get<int>(presentation,"ParryImpactCount");
            long success=TimeValue<long>("TickCount");
            Assert.That(delay,Is.EqualTo(8));Assert.That(Get<bool>(target,"IsParryStaggered"),Is.True);
            Assert.That(Get<string>(enemyArt,"MotionName"),Is.EqualTo("ParryApproach"));
            int guard=0;while(Get<bool>(presentation,"IsParryImpactPending")&&guard++<fps*2)RenderAt(fps);
            Assert.That(Get<bool>(presentation,"IsParryImpactPending"),Is.False);
            Assert.That(Get<long>(presentation,"ParryImpactTick")-success,Is.EqualTo(delay));
            Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts+1));
            Assert.That(drawing.sprite.name,Is.EqualTo("EnemySample_attack_14"));
            Assert.That(Get<string>(enemyArt,"MotionName"),Is.EqualTo("ParryImpact"));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.InRange(29,30));
            long contact=TimeValue<long>("TickCount");var contactSprite=drawing.sprite;
            int held=0;while(TimeValue<bool>("IsHitStopped")&&held++<fps*2)
            {
                Assert.That(drawing.sprite,Is.SameAs(contactSprite));RenderAt(fps);
                if(TimeValue<bool>("IsHitStopped"))Assert.That(TimeValue<long>("TickCount"),Is.EqualTo(contact));
                else Assert.That(TimeValue<long>("TickCount"),Is.InRange(contact,contact+Mathf.CeilToInt(60f/fps)-1),
                    "A 30fps render may use its remaining 1/60s for the first post-stop combat tick.");
            }
            Assert.That(held/(float)fps,Is.EqualTo(30f/60).Within(1.01f/fps));
            Assert.That(Get<bool>(presentation,"IsParrySequenceActive"),Is.True);
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));
            Assert.That(Get<string>(enemyArt,"MotionName"),Is.EqualTo("Parried"));
            Assert.That(drawing.sprite.name,Does.Contain("down"));
            Assert.That(Get<int>(target,"HitCount"),Is.Zero);Assert.That(Get<int>(target,"CurrentHp"),Is.EqualTo(120));
            Assert.That(Get<int>(player.GetComponent(T("PlayerDamageReceiver2D")),"CurrentHp"),Is.EqualTo(100));
        }
        [TestCase(false)] [TestCase(true)]
        public void AdaptiveParryQueuesOneAttackWithoutSkippingTheEnemyPeakOrConsumingTwoStocks(bool duringImpact)
        {
            var target=BeginAuthoredAdaptiveParry(1);
            if(duringImpact)Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            for(int i=0;i<3;i++)Assert.That((bool)Call(player,"RequestAction",PlayerActionRequest.Attack),Is.True);
            Assert.That(Get<bool>(presentation,"HasQueuedParryCounter"),Is.True);
            Assert.That(Get<bool>(counter,"IsActive"),Is.False);Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            Until(()=>Get<bool>(counter,"IsActive"));
            Assert.That(Get<bool>(presentation,"IsParrySequenceActive"),Is.False);
            Assert.That(Get<int>(counter,"ConsumedCount"),Is.EqualTo(1));
            Until(()=>!Get<bool>(counter,"IsActive"));Frames(20);
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));Assert.That(Get<int>(target,"CurrentHp"),Is.EqualTo(30));
            Assert.That(Get<bool>(presentation,"HasQueuedParryCounter"),Is.False);AssertClean();
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("TargetLost")] [TestCase("SessionReset")]
        public void AdaptiveParryCancellationCannotEmitALateImpactOrQueuedCounter(string operation)
        {
            var target=BeginAuthoredAdaptiveParry(1);Call(player,"RequestAction",PlayerActionRequest.Attack);
            int impacts=Get<int>(presentation,"ParryImpactCount"),starts=Get<int>(counter,"StartCount");
            if(operation=="Reset")Call(arena,"ResetActionsOnly");
            else if(operation=="Disable"){((Behaviour)presentation).enabled=false;((Behaviour)presentation).enabled=true;}
            else if(operation=="SessionReset")Static("ResetSession");
            else target.gameObject.SetActive(false);
            Frames(100);
            Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts));
            Assert.That(Get<int>(counter,"StartCount"),Is.EqualTo(starts));
            Assert.That(Get<bool>(presentation,"IsParrySequenceActive"),Is.False);
            Assert.That(Get<bool>(presentation,"HasQueuedParryCounter"),Is.False);
            Assert.That(TimeValue<int>("HitstopOwnerCount"),Is.Zero);AssertClean();
        }
        [TestCase(0f)] [TestCase(.5f)] [TestCase(1f)]
        public void AdaptiveParryFlashStrengthChangesOnlyTheSoftLightNotTheImpactTiming(float flash)
        {
            Set(Get<object>(feedback,"Settings"),"flash",flash);BeginAuthoredAdaptiveParry(-1);
            Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));Frames(2);Call(presentation,"AdvanceVisuals",0f);
            var light=Camera.main.GetComponent(T("VideoParryBacklight"));
            Assert.That(Get<float>(light,"BackgroundAlpha"),Is.EqualTo(.4f*flash).Within(.001f));
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.EqualTo(flash>0));
        }
        [TestCase(4,4)] [TestCase(8,8)] [TestCase(15,15)] [TestCase(30,15)]
        public void AdaptiveParryApproachCanBeRetimedWithoutMovingItsAuthoredImpact(int limit,int expectedDelay)
        {
            var profile=Get<object>(counter,"CinematicProfile");int original=Get<int>(profile,"parryApproachMaxFrames");
            try
            {
                Set(profile,"parryApproachMaxFrames",limit);var target=BeginAuthoredAdaptiveParry(1);
                long success=TimeValue<long>("TickCount");Assert.That(Get<int>(presentation,"ParryImpactDelayFrames"),Is.EqualTo(expectedDelay));
                Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
                Assert.That(Get<long>(presentation,"ParryImpactTick")-success,Is.EqualTo(expectedDelay));
                Assert.That(Get<SpriteRenderer>(target.GetComponent(T("PrototypeEnemyArtPresentation")),"Artwork").sprite.name,Is.EqualTo("EnemySample_attack_14"));
                Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));Assert.That(Get<int>(target,"HitCount"),Is.Zero);
            }
            finally{Call(arena,"ResetActionsOnly");Set(profile,"parryApproachMaxFrames",original);}
        }
        [Test] public void AdaptiveParryRetainsAnAttackStillBufferedImmediatelyBeforeSuccess()
        {
            var target=BeginAuthoredAdaptiveParry(1,true,true);Frames(1);
            Assert.That(Get<bool>(presentation,"HasQueuedParryCounter"),Is.True);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));Assert.That(Get<bool>(counter,"IsActive"),Is.False);
            Until(()=>Get<bool>(counter,"IsActive"));Until(()=>!Get<bool>(counter,"IsActive"));Frames(20);
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));Assert.That(Get<int>(counter,"ConsumedCount"),Is.EqualTo(1));AssertClean();
        }
        [TestCase("TargetLost")] [TestCase("Disable")] [TestCase("Hit")] [TestCase("SessionReset")]
        public void AdaptiveParryInterruptedDuringImpactImmediatelyClearsTheWaveAndQueuedInput(string operation)
        {
            var target=BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.True);Call(player,"RequestAction",PlayerActionRequest.Attack);
            int starts=Get<int>(counter,"StartCount"),impacts=Get<int>(presentation,"ParryImpactCount");
            if(operation=="TargetLost")target.gameObject.SetActive(false);
            else if(operation=="Disable")((Behaviour)presentation).enabled=false;
            else if(operation=="Hit")Call(player,"ForceHit","AdaptiveParryTestHit");
            else Static("ResetSession");
            Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(feedback,"IsParryShockwaveVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsParrySequenceActive"),Is.False);
            Assert.That(Get<bool>(presentation,"HasQueuedParryCounter"),Is.False);
            Assert.That(TimeValue<int>("HitstopOwnerCount"),Is.Zero);Frames(100);
            Assert.That(Get<int>(counter,"StartCount"),Is.EqualTo(starts));Assert.That(Get<int>(presentation,"ParryImpactCount"),Is.EqualTo(impacts));
        }
        [Test] public void AdaptiveParryFlashZeroDuringPauseCannotResurrectTheOldLight()
        {
            BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));Frames(2);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);Static("SetPaused",true);
            Set(Get<object>(feedback,"Settings"),"flash",0f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Set(Get<object>(feedback,"Settings"),"flash",1f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Static("SetPaused",false);Frames(10);Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
        }
        [TestCase(1)] [TestCase(-1)]
        public void OrangeParryAddsNewSparksAndGlowWhileSuppressingTheOldWhiteHalo(int direction)
        {
            var target=BeginAuthoredAdaptiveParry(direction);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.EqualTo(3));
            var accent=Get<Component>(presentation,"orangeImpact");var mesh=Get<Mesh>(accent,"sparkMesh");
            Assert.That(mesh.name,Is.EqualTo("ParryOrangeSparks"));
            Assert.That(Array.Exists(mesh.colors,c=>c.r>.9f&&c.g>.2f&&c.g<.5f&&c.b<.1f),Is.True);
            var wave=T("ParryShockwaveFeature").GetMethod("GetSample").Invoke(null,new object[]{Camera.main});
            Assert.That((Vector2)Get<Vector3>(wave,"worldCenter"),Is.EqualTo(Get<Vector2>(accent,"WorldCenter")));
            var light=Camera.main.GetComponent(T("VideoParryBacklight"));
            Assert.That(Get<bool>(light,"IsHaloVisible"),Is.False);Assert.That(Get<float>(light,"ActorLightStrength"),Is.EqualTo(.08f));
            Assert.That(Get<float>(light,"BloomStrength"),Is.Zero);
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));
            Assert.That(Get<int>(target,"HitCount"),Is.Zero);
            Frames(24);Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.Zero);
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));AssertClean();
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,-1)] [TestCase(60,-1)] [TestCase(120,-1)]
        public void HitstopJittersBothDrawingsInOppositeDirectionsWithoutMovingBodiesOrColliders(int fps,int direction)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);
            var target=BeginAuthoredAdaptiveParry(direction);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            var enemyArt=target.GetComponent(T("PrototypeEnemyArtPresentation"));
            Vector2 playerRoot=body.position,enemyRoot=target.transform.position;
            Vector3 playerCollider=player.GetComponent<Collider2D>().bounds.center,enemyCollider=target.GetComponent<Collider2D>().bounds.center;
            bool positive=false,negative=false;int samples=0;
            while(TimeValue<bool>("IsHitStopped")&&samples++<fps)
            {
                float p=Get<float>(art,"ImpactOffsetX"),e=Get<float>(enemyArt,"ImpactOffsetX");
                Assert.That(p+e,Is.Zero.Within(.00001f));Assert.That(Mathf.Abs(p),Is.LessThanOrEqualTo(2f/32));
                positive|=p>0;negative|=p<0;
                Assert.That(body.position,Is.EqualTo(playerRoot));Assert.That((Vector2)target.transform.position,Is.EqualTo(enemyRoot));
                Assert.That(player.GetComponent<Collider2D>().bounds.center,Is.EqualTo(playerCollider));
                Assert.That(target.GetComponent<Collider2D>().bounds.center,Is.EqualTo(enemyCollider));
                Assert.That(Camera.main.orthographicSize,Is.EqualTo(4.25f));
                Assert.That(Get<SpriteRenderer>(art,"Artwork").transform.position.x-playerRoot.x,Is.EqualTo(p).Within(.00001f));
                Assert.That(Get<SpriteRenderer>(enemyArt,"Artwork").transform.position.x-enemyRoot.x,Is.EqualTo(e).Within(.00001f));
                RenderAt(fps);
            }
            Assert.That(positive&&negative,Is.True);Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Zero);
            Assert.That(Get<float>(enemyArt,"ImpactOffsetX"),Is.Zero);Assert.That(Get<bool>(presentation,"IsParryActorJitterActive"),Is.False);
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Frames(24);AssertClean();
        }
        [TestCase(0f,1f,1f,false,false,true)] [TestCase(1f,0f,1f,true,false,true)]
        [TestCase(1f,1f,0f,true,true,false)] [TestCase(.5f,.5f,.5f,true,true,true)] [TestCase(0f,0f,0f,false,false,false)]
        public void OrangeParryVisualSettingsDoNotChangeTheImpactStopOrStock(float vfx,float flash,float shake,bool sparks,bool glow,bool jitter)
        {
            var settings=Get<object>(feedback,"Settings");Set(settings,"vfx",vfx);Set(settings,"flash",flash);Set(settings,"shake",shake);
            BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Get<bool>(presentation,"IsOrangeParrySparkVisible"),Is.EqualTo(sparks));
            Assert.That(Get<bool>(presentation,"IsOrangeParryGlowVisible"),Is.EqualTo(glow));
            Assert.That(Get<bool>(presentation,"IsParryActorJitterActive"),Is.EqualTo(jitter));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            Assert.That(Get<int>(presentation,"ParryImpactDelayFrames"),Is.EqualTo(8));
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Frames(24);AssertClean();
        }
        [Test] public void OrangeAccentAndActorJitterPauseThenZeroSettingsCannotResurrectOldEffects()
        {
            var settings=Get<object>(feedback,"Settings");Set(settings,"shake",1f);
            BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));Frames(3);
            float offset=Get<float>(art,"ImpactOffsetX");var accent=Get<Component>(presentation,"orangeImpact");float age=Get<float>(accent,"Elapsed");
            Static("SetPaused",true);Frames(20);
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.EqualTo(offset));Assert.That(Get<float>(accent,"Elapsed"),Is.EqualTo(age));
            Set(settings,"shake",0f);Set(settings,"flash",0f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Zero);Assert.That(Get<bool>(presentation,"IsOrangeParryGlowVisible"),Is.False);
            Set(settings,"shake",1f);Set(settings,"flash",1f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Zero);Assert.That(Get<bool>(presentation,"IsOrangeParryGlowVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsOrangeParrySparkVisible"),Is.True);
            Set(settings,"vfx",0f);Call(presentation,"AdvanceVisuals",0f);Set(settings,"vfx",1f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.Zero);
            Static("SetPaused",false);Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Frames(24);AssertClean();
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("TargetLost")] [TestCase("Hit")]
        [TestCase("SessionReset")] [TestCase("PlayerArtDisable")] [TestCase("EnemyArtDisable")]
        public void OrangeImpactInterruptionClearsBothDrawingOffsetsAndAdditionalEffects(string operation)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);
            var target=BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            var enemyArt=target.GetComponent(T("PrototypeEnemyArtPresentation"));
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Not.Zero);Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.EqualTo(3));
            if(operation=="Reset")Call(arena,"ResetActionsOnly");
            else if(operation=="Disable")((Behaviour)presentation).enabled=false;
            else if(operation=="TargetLost")target.gameObject.SetActive(false);
            else if(operation=="Hit")Call(player,"ForceHit","OrangeParryTestHit");
            else if(operation=="PlayerArtDisable")((Behaviour)art).enabled=false;
            else if(operation=="EnemyArtDisable")((Behaviour)enemyArt).enabled=false;
            else Static("ResetSession");
            Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<float>(art,"ImpactOffsetX"),Is.Zero);Assert.That(Get<float>(enemyArt,"ImpactOffsetX"),Is.Zero);
            Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.Zero);Assert.That(Get<bool>(presentation,"IsParryActorJitterActive"),Is.False);
            Frames(80);Assert.That(Get<int>(presentation,"OrangeParryEffectCount"),Is.Zero);
        }
        [Test] public void PlaceholderEnemyDrawingAlsoJittersWithoutMovingItsRoot()
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);var target=BeginActualParry(1,true);
            var cue=target.GetComponent(T("EnemyCombatCue2D"));Vector3 root=target.transform.position;
            Assert.That(Get<float>(cue,"ImpactOffsetX"),Is.EqualTo(-Get<float>(art,"ImpactOffsetX")));
            Frames(5);Assert.That(Get<float>(cue,"ImpactOffsetX"),Is.Not.Zero);Assert.That(target.transform.position,Is.EqualTo(root));
            Until(()=>!TimeValue<bool>("IsHitStopped"));Assert.That(Get<float>(cue,"ImpactOffsetX"),Is.Zero);
        }
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,-1)] [TestCase(60,-1)] [TestCase(120,-1)]
        public void ParryCameraApproachesContactPeaksAtImpactHoldsThenRestoresTheOriginalView(int fps,int direction)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);Vector3 original=Camera.main.transform.position;
            BeginAuthoredAdaptiveParry(direction);
            Assert.That(Get<string>(presentation,"ParryCameraPhase"),Is.EqualTo("Preparation"));
            Assert.That(Camera.main.orthographicSize,Is.EqualTo(5.625f),"The first preparation frame holds zoom while position leads.");
            Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Camera.main.orthographicSize,Is.EqualTo(4.25f).Within(.0001f));
            Vector2 center=Get<Vector2>(presentation,"ParryCameraCenter");
            Assert.That((Vector2)(Camera.main.transform.position-Get<Vector3>(feedback,"ShakeOffset")),Is.EqualTo(center));
            int guard=0;while(Get<bool>(presentation,"IsParrySequenceActive")&&guard++<fps*2)RenderAt(fps);
            Assert.That(Get<string>(presentation,"ParryCameraPhase"),Is.EqualTo("Hold"));
            Assert.That(Get<Vector3>(feedback,"ShakeOffset").magnitude,Is.Zero.Within(.0001f));
            int held=0;while(Get<string>(presentation,"ParryCameraPhase")=="Hold"&&held++<fps)RenderAt(fps);
            Assert.That(held/(float)fps,Is.EqualTo(.2f).Within(2f/fps));
            Until(()=>!Get<bool>(presentation,"IsParryCameraActive"));
            Assert.That(Camera.main.transform.position,Is.EqualTo(original));Assert.That(Camera.main.orthographicSize,Is.EqualTo(5.625f));AssertClean();
        }
        [TestCase(0f)] [TestCase(.5f)] [TestCase(1f)]
        public void ParryCameraIntensityDoesNotChangeSuccessOrHitstop(float strength)
        {
            Set(Get<object>(feedback,"Settings"),"shake",strength);BeginAuthoredAdaptiveParry(1);
            Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Camera.main.orthographicSize,Is.EqualTo(Mathf.Lerp(5.625f,4.25f,strength)).Within(.0001f));
            Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Until(()=>!Get<bool>(presentation,"IsParryCameraActive"));AssertClean();
        }
        [TestCase("Preparation")] [TestCase("Impact")] [TestCase("Hold")]
        public void ParryCameraPauseAndZeroSettingRestoreWithoutResurrectingTheOldFocus(string phase)
        {
            var settings=Get<object>(feedback,"Settings");Set(settings,"shake",1f);Vector3 original=Camera.main.transform.position;
            BeginAuthoredAdaptiveParry(1);
            if(phase!="Preparation")Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            if(phase=="Hold")Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));
            Static("SetPaused",true);Vector3 held=Camera.main.transform.position;float size=Camera.main.orthographicSize;Frames(20);
            Assert.That(Camera.main.transform.position,Is.EqualTo(held));Assert.That(Camera.main.orthographicSize,Is.EqualTo(size));
            Set(settings,"shake",0f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Camera.main.transform.position,Is.EqualTo(original));Assert.That(Camera.main.orthographicSize,Is.EqualTo(5.625f));
            Set(settings,"shake",1f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"IsParryCameraActive"),Is.False);Static("SetPaused",false);
        }
        [TestCase("Preparation","Reset")] [TestCase("Impact","TargetLost")] [TestCase("Hold","Hit")]
        [TestCase("Returning","Disable")] [TestCase("Hold","Reset")] [TestCase("Preparation","TargetLost")]
        public void ParryCameraInterruptionRestoresItsSavedBaseWithoutALingeringShake(string phase,string operation)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);Vector3 original=Camera.main.transform.position;
            var target=BeginAuthoredAdaptiveParry(1);
            if(phase!="Preparation")Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            if(phase=="Hold"||phase=="Returning")Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));
            if(phase=="Returning")Until(()=>Get<string>(presentation,"ParryCameraPhase")=="Returning");
            if(operation=="Reset")Call(arena,"ResetActionsOnly");
            else if(operation=="TargetLost")target.gameObject.SetActive(false);
            else if(operation=="Hit")Call(player,"ForceHit","ParryCameraTestHit");
            else ((Behaviour)presentation).enabled=false;
            Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Camera.main.transform.position,Is.EqualTo(original));Assert.That(Camera.main.orthographicSize,Is.EqualTo(5.625f));
            Assert.That(Get<bool>(presentation,"IsParryCameraActive"),Is.False);Assert.That(Get<bool>(feedback,"HasCameraFocus"),Is.False);
        }
        [Test] public void ParryCameraTransfersToCounterWithoutReplacingItsOriginalReturnView()
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);Vector3 original=Camera.main.transform.position;
            BeginAuthoredAdaptiveParry(1);Call(player,"RequestAction",PlayerActionRequest.Attack);
            Until(()=>Get<bool>(counter,"IsActive"));
            Assert.That((bool)Call(feedback,"IsCameraFocusOwnedBy",presentation),Is.True);
            Assert.That(Get<bool>(presentation,"IsParryCameraActive"),Is.False);
            Until(()=>!Get<bool>(counter,"IsActive"));Frames(24);
            Assert.That(Camera.main.transform.position,Is.EqualTo(original));AssertClean();
        }
        [TestCase(true,true)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(false,false)]
        public void ExpressiveCameraAndReferenceEffectsCanBeRolledBackIndependently(bool expressive,bool reference)
        {
            var profile=Get<object>(counter,"CinematicProfile");bool originalCamera=Get<bool>(profile,"parryExpressiveCamera"),originalEffects=Get<bool>(profile,"parryReferenceAccent");
            try
            {
                Set(profile,"parryExpressiveCamera",expressive);Set(profile,"parryReferenceAccent",reference);
                Set(Get<object>(feedback,"Settings"),"shake",1f);BeginAuthoredAdaptiveParry(1);
                Assert.That(Camera.main.orthographicSize,expressive?Is.EqualTo(5.625f):Is.LessThan(5.625f));
                Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
                Assert.That(Get<int>(presentation,"ParryParticleCount"),Is.EqualTo(reference?32:14));
                Assert.That(Get<bool>(presentation,"IsParryFlareVisible"),Is.EqualTo(reference));
                Assert.That(Camera.main.orthographicSize,Is.EqualTo(4.25f));
                Assert.That(TimeValue<int>("HitstopRemainingTicks"),Is.EqualTo(30));
                Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Until(()=>!Get<bool>(presentation,"IsParryCameraActive"));AssertClean();
            }
            finally{Call(arena,"ResetActionsOnly");Set(profile,"parryExpressiveCamera",originalCamera);Set(profile,"parryReferenceAccent",originalEffects);}
        }
        [TestCase(1)] [TestCase(-1)]
        public void ExpressiveCameraPositionArrivesBeforeItsZoomWithoutMovingTheBody(int direction)
        {
            Set(Get<object>(feedback,"Settings"),"shake",1f);BeginAuthoredAdaptiveParry(direction);
            Vector3 normal=Camera.main.transform.position;Vector2 root=body.position;Frames(2);
            Assert.That(body.position,Is.EqualTo(root));Assert.That(Camera.main.orthographicSize,Is.EqualTo(5.625f));
            Assert.That(Mathf.Abs(Camera.main.transform.position.x-normal.x),Is.GreaterThan(.01f));
            Frames(4);Vector2 center=Get<Vector2>(presentation,"ParryCameraCenter");
            Assert.That((Vector2)Camera.main.transform.position,Is.EqualTo(center));
            Assert.That(Camera.main.orthographicSize,Is.GreaterThan(4.25f));
            Frames(2);Assert.That(Camera.main.orthographicSize,Is.EqualTo(4.25f));
            Assert.That(Get<bool>(presentation,"IsParryImpactPending"),Is.False);
        }
        [Test] public void ReferenceFlareIsBriefAndCannotResurrectWhenFlashIsToggledDuringPause()
        {
            BeginAuthoredAdaptiveParry(1);Until(()=>!Get<bool>(presentation,"IsParryImpactPending"));
            Assert.That(Get<bool>(presentation,"IsParryFlareVisible"),Is.True);Static("SetPaused",true);
            var settings=Get<object>(feedback,"Settings");Set(settings,"flash",0f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"IsParryFlareVisible"),Is.False);Set(settings,"flash",1f);Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"IsParryFlareVisible"),Is.False);Assert.That(Get<bool>(presentation,"IsOrangeParrySparkVisible"),Is.True);
            Static("SetPaused",false);Frames(12);Assert.That(Get<bool>(presentation,"IsParryFlareVisible"),Is.False);
            Until(()=>!Get<bool>(presentation,"IsParrySequenceActive"));Frames(24);AssertClean();
        }
        [UnityTest] public IEnumerator ActiveSceneChangeRestoresCounterAndLaunchOwnership()
        {
            Set(Get<object>(feedback, "Settings"), "shake", 1f);
            var target = Begin(); ToFrame(22); var previous = SceneManager.GetActiveScene();
            var temporary = SceneManager.CreateScene("CounterCinematicSceneChangeTest"); SceneManager.SetActiveScene(temporary);
            AssertClean(); Assert.That(Get<int>(target, "MotionOwnerCount"), Is.Zero); Assert.That(Get<int>(target, "DeathPresentationOwnerCount"), Is.Zero);
            SceneManager.SetActiveScene(previous); yield return SceneManager.UnloadSceneAsync(temporary);
        }
    }
}
