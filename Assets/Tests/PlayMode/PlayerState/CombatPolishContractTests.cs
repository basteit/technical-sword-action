using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class CombatPolishContractTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<Object> owned = new();
        private Component clock;
        private bool manual;
        [UnitySetUp] public IEnumerator Setup()
        {
            clock = (Component)Object.FindFirstObjectByType(T("CombatTimeController"));
            manual = Get<bool>(clock, "ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, true);
            Static("ResetSession"); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); Static("ResetSession");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock, manual); yield return null;
        }

        [TestCase("Normal", 30, 90, false)]
        [TestCase("Variant", 120, 90, true)]
        [TestCase("Normal", 200, 90, true)]
        public void HpIsPerEnemyNotAnAutomaticKillByCategory(string rank, int hp, int damage, bool alive)
        {
            var definition = Data(hp); Set(definition, "rank", Enum.Parse(T("EnemyCombatRank"), rank));
            var enemy = Enemy(definition);
            Call(enemy, "TryTakeCombatHit", damage, Vector2.right, Enum.Parse(T("EnemyHitStrength"), "Strong"));
            Assert.That(Get<bool>(enemy, "IsAlive"), Is.EqualTo(alive));
            Assert.That(Get<int>(enemy, "CurrentHp"), Is.EqualTo(Mathf.Max(0, hp - damage)));
        }
        [Test]
        public void DefinitionAndIndividualOverridesAreIndependentAndPreservedByReset()
        {
            var data = Data(120); Set(data, "overrideCounterMultiplier", true); Set(data, "counterMultiplier", 2.5f);
            var a = Enemy(data); var b = Enemy(data);
            Call(a, "ConfigureInstanceOverrides", 180, 1.25f);
            Assert.That(Get<int>(a, "MaxHp"), Is.EqualTo(180));
            Assert.That(Get<float>(a, "CounterMultiplier"), Is.EqualTo(1.25f));
            Assert.That(Get<int>(b, "MaxHp"), Is.EqualTo(120));
            Assert.That(Get<float>(b, "CounterMultiplier"), Is.EqualTo(2.5f));
            Call(a, "ResetForCapture", a.transform.position, Get<int>(a, "ConfiguredMaxHp"));
            Assert.That(Get<int>(a, "CurrentHp"), Is.EqualTo(180));
            Assert.That((int)data.GetType().GetField("maxHp").GetValue(data), Is.EqualTo(120));
        }
        [Test]
        public void StrongMobResistsLightHitsButStillTakesDamageAndStrongHitStuns()
        {
            var data = Data(120); Set(data, "lightHitStunFrames", 0);
            var enemy = Enemy(data);
            Call(enemy, "TryTakeHit", 10, Vector2.right);
            Assert.That(Get<int>(enemy, "CurrentHp"), Is.EqualTo(110));
            Assert.That(Get<bool>(enemy, "IsStunned"), Is.False);
            Call(enemy, "TryTakeCombatHit", 25, Vector2.right, Enum.Parse(T("EnemyHitStrength"), "Strong"));
            Assert.That(Get<bool>(enemy, "IsStunned"), Is.True);
        }
        [Test]
        public void LightStunHasResistanceButNeverBlocksHpDamage()
        {
            var enemy = Enemy(Data(120));
            Call(enemy, "TryTakeHit", 10, Vector2.right);
            for (int i = 0; i < 6; i++) Call(enemy, "CombatTickTimers");
            Assert.That(Get<bool>(enemy, "IsStunned"), Is.False);
            Call(enemy, "TryTakeHit", 10, Vector2.right);
            Assert.That(Get<int>(enemy, "CurrentHp"), Is.EqualTo(100));
            Assert.That(Get<string>(enemy, "LastHitReaction"), Is.EqualTo("LightResistance"));
        }
        [TestCase("Normal")] [TestCase("Just")]
        public void SuccessfulParryHoldsTheEnemyForExactlyThreeCombatSeconds(string result)
        {
            var enemy = Enemy(Data(120));
            Call(enemy, "ApplyParryStun", Enum.Parse(T("ParryResult"), result));
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.EqualTo(180));
            for (int i = 0; i < 179; i++) Call(enemy, "CombatTickTimers");
            Assert.That(Get<bool>(enemy, "IsParryStaggered"), Is.True);
            Assert.That(Get<bool>(enemy, "IsStunned"), Is.True);
            Call(enemy, "CombatTickTimers");
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.Zero);
            Assert.That(Get<bool>(enemy, "IsStunned"), Is.False);
        }
        [Test]
        public void EnemyTypeAndInstanceCanTuneStaggerWithoutChangingSharedData()
        {
            var data = Data(120); Set(data, "parryStaggerFrames", 120);
            var a = Enemy(data); var b = Enemy(data);
            Call(a, "ConfigureParryStaggerFrames", 240);
            Call(a, "RefreshParryStagger"); Call(b, "RefreshParryStagger");
            Assert.That(Get<int>(a, "ParryStaggerRemainingFrames"), Is.EqualTo(240));
            Assert.That(Get<int>(b, "ParryStaggerRemainingFrames"), Is.EqualTo(120));
            Call(a, "ResetForCapture", a.transform.position, 120); Call(a, "RefreshParryStagger");
            Assert.That(Get<int>(a, "ParryStaggerRemainingFrames"), Is.EqualTo(240));
            Call(a, "ConfigureParryStaggerFrames", 0); Call(a, "RefreshParryStagger");
            Assert.That(Get<int>(a, "ParryStaggerRemainingFrames"), Is.EqualTo(120));
            Set(data, "parryStaggerFrames", 0); Assert.That(Get<bool>(data, "IsValid"), Is.False);
        }
        [Test]
        public void PauseAndHitstopFreezeTheStaggerWindow()
        {
            var enemy = Enemy(Data(120)); Call(enemy, "RefreshParryStagger");
            Static("SetPaused", true);
            for (int i = 0; i < 120; i++) Call(clock, "AdvanceFrame", 1d / 60d);
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.EqualTo(180));
            Static("SetPaused", false); Static("RequestHitstop", enemy, 3f / 60f);
            for (int i = 0; i < 3; i++) Call(clock, "AdvanceFrame", 1d / 60d);
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.EqualTo(180));
            Call(clock, "AdvanceFrame", 1d / 60d);
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.EqualTo(179));
        }
        [TestCase("Death")] [TestCase("Disable")] [TestCase("Reset")] [TestCase("Exit")]
        public void LeavingTheOpportunityNeverRetainsStagger(string reason)
        {
            var enemy = Enemy(Data(120)); Call(enemy, "RefreshParryStagger");
            if (reason == "Death") Call(enemy, "TryTakeHit", 1000, Vector2.right);
            else if (reason == "Disable") enemy.gameObject.SetActive(false);
            else if (reason == "Reset") Call(enemy, "ResetForCapture", enemy.transform.position, 120);
            else Call(enemy, "SetCombatParticipation", false);
            Assert.That(Get<int>(enemy, "ParryStaggerRemainingFrames"), Is.Zero);
            Assert.That(Get<bool>(enemy, "IsParryStaggered"), Is.False);
        }

        [Test]
        public void AttackRolesAreBoundedAndReleasedOnParryDeathDisableAndReset()
        {
            var coordinator = Add(Own(new GameObject("RoleOwner")), "EnemyAttackCoordinator2D");
            Set(coordinator, "minimumStartGapFrames", 0);
            var a = Melee(Data(120), coordinator); var b = Melee(Data(120), coordinator);
            Assert.That((bool)Call(a, "BeginCaptureAttack"), Is.True);
            Assert.That((bool)Call(b, "BeginCaptureAttack"), Is.False);
            Assert.That(Get<int>(coordinator, "ActiveAttackers"), Is.EqualTo(1));
            Call(a.GetComponent(T("Damageable2D")), "ApplyParryStun", Enum.Parse(T("ParryResult"), "Normal"));
            Assert.That((bool)Call(b, "BeginCaptureAttack"), Is.True);
            Call(b.GetComponent(T("Damageable2D")), "TryTakeHit", 1000, Vector2.right);
            Assert.That(Get<int>(coordinator, "ActiveAttackers"), Is.Zero);
            Call(a, "ResetForCapture", null, true);
            Assert.That(Get<int>(coordinator, "ActiveAttackers"), Is.Zero);
            Call(coordinator, "ResetEncounter");
            Assert.That(Get<int>(coordinator, "StartCount"), Is.Zero);
        }
        [Test]
        public void AttackSequenceAlternatesParryableAndLowUnparryableWithoutDuplicateActiveDamage()
        {
            var data = Data(120);
            var slash = Own(ScriptableObject.CreateInstance(T("EnemyAttackDefinition")));
            var sweep = Own(ScriptableObject.CreateInstance(T("EnemyAttackDefinition")));
            Set(sweep, "parryable", false); Set(sweep, "resistHitStunWhileAttacking", true);
            var array = Array.CreateInstance(T("EnemyAttackDefinition"), 2); array.SetValue(slash, 0); array.SetValue(sweep, 1); Set(data, "attacks", array);
            var melee = Melee(data, null);
            Call(melee, "BeginCaptureAttack"); Assert.That(Get<string>(melee, "AttackKind"), Is.EqualTo("Parryable"));
            for (int i = 0; i < 40; i++) { Call(melee, "CombatTick"); Call(melee, "CombatTickTimers"); }
            Call(melee, "BeginCaptureAttack"); Assert.That(Get<string>(melee, "AttackKind"), Is.EqualTo("Unparryable"));
            Assert.That(Get<bool>(melee, "ResistsHitStun"), Is.True);
        }
        [TestCase("LightHit", 3)] [TestCase("HeavyHit", 5)] [TestCase("Counter", 5)]
        [TestCase("Parry", 3)] [TestCase("JustParry", 7)] [TestCase("PlayerHit", 3)]
        public void FeedbackUsesConfiguredHitstopAndEmitsOnCurrentTick(string kind, int frames)
        {
            var feedback = Feedback(out _, out _);
            Call(feedback, "Present", Enum.Parse(T("CombatFeedbackKind"), kind), Vector2.zero, null);
            Assert.That((int)T("CombatTimeController").GetProperty("HitstopRemainingTicks").GetValue(null), Is.EqualTo(frames));
            Assert.That(Get<long>(feedback, "LastEmissionTick"), Is.EqualTo((long)T("CombatTimeController").GetProperty("TickCount").GetValue(null)));
        }
        [Test]
        public void MultipleHitsUseMaximumStopNotSumAndReleaseOnDisable()
        {
            var feedback = Feedback(out _, out _);
            Present(feedback, "LightHit"); Present(feedback, "HeavyHit"); Present(feedback, "JustParry");
            Assert.That((int)T("CombatTimeController").GetProperty("HitstopRemainingTicks").GetValue(null), Is.EqualTo(7));
            ((Behaviour)feedback).enabled = false;
            Assert.That((int)T("CombatTimeController").GetProperty("HitstopRemainingTicks").GetValue(null), Is.Zero);
            Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.Zero);
            Assert.That(Get<Vector3>(feedback, "ShakeOffset"), Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void ZeroPresentationStillShowsEssentialGlyphWithoutFlashOrShake()
        {
            var feedback = Feedback(out var sprite, out _);
            object settings = Get<object>(feedback, "Settings");
            foreach (string field in new[] { "shake", "flash", "vfx", "masterVolume", "sfxVolume" }) Set(settings, field, 0f);
            Color original = sprite.color;
            Present(feedback, "PlayerHit"); Call(feedback, "AdvanceVisuals", .02f);
            Assert.That(Get<int>(feedback, "EssentialGlyphCount"), Is.EqualTo(1));
            Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.EqualTo(1));
            Assert.That(Get<int>(feedback, "ActiveFlashCount"), Is.Zero);
            Assert.That(sprite.color, Is.EqualTo(original));
            Assert.That(Get<Vector3>(feedback, "ShakeOffset"), Is.EqualTo(Vector3.zero));
            Assert.That((int)T("CombatTimeController").GetProperty("HitstopRemainingTicks").GetValue(null), Is.EqualTo(3), "Hitstop is not a cosmetic accessibility toggle");
        }
        [Test]
        public void PauseFreezesPresentationAndSessionResetClearsEverything()
        {
            var feedback = Feedback(out _, out _); Present(feedback, "HeavyHit");
            Static("SetPaused", true); Call(feedback, "AdvanceVisuals", 1f);
            Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.EqualTo(1));
            Static("ResetSession");
            Assert.That(Get<int>(feedback, "ActiveEffectCount"), Is.Zero);
            Assert.That(Get<int>(feedback, "ActiveFlashCount"), Is.Zero);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [Test]
        public void ActualAttackHitDispatchesOnceAndEmptySwingDispatchesNothing()
        {
            var feedback = Feedback(out _, out var attack);
            var enemy = Enemy(Data(120)); enemy.transform.position = attack.transform.position + Vector3.right * .8f;
            Physics2D.SyncTransforms(); Call(attack, "TryStartAttackFromStateMachine"); Call(attack, "OnAttackHit", 1);
            Assert.That(Get<int>(feedback, "EmissionCount"), Is.EqualTo(1));
            Assert.That(Get<int>(enemy, "HitCount"), Is.EqualTo(1));
            Call(attack, "OnAttackHit", 1); Assert.That(Get<int>(feedback, "EmissionCount"), Is.EqualTo(1));
            Call(attack, "CancelAttack", "EmptyTest"); enemy.transform.position += Vector3.right * 10;
            Static("ResetSession"); Physics2D.SyncTransforms();
            Call(attack, "TryStartAttackFromStateMachine"); Call(attack, "OnAttackHit", 1);
            Assert.That(Get<int>(feedback, "EmissionCount"), Is.EqualTo(1));
        }

        private Component Feedback(out SpriteRenderer sprite, out Component attack)
        {
            var profile = Own(ScriptableObject.CreateInstance(T("CombatFeedbackProfile")));
            Type entryType = T("CombatFeedbackProfile").GetNestedType("Entry");
            var kinds = Enum.GetValues(T("CombatFeedbackKind")); var entries = Array.CreateInstance(entryType, kinds.Length);
            int index = 0;
            foreach (object kind in kinds)
            {
                object entry = Activator.CreateInstance(entryType); Set(entry, "kind", kind);
                string name = kind.ToString(); Set(entry, "hitstopFrames", name == "JustParry" ? 7 : name == "Counter" || name == "HeavyHit" ? 5 : name == "Defeat" || name == "ParryMiss" ? 0 : 3);
                entries.SetValue(entry, index++);
            }
            Set(profile, "entries", entries);
            var settings = Own(ScriptableObject.CreateInstance(T("CombatFeedbackSettings")));
            var root = Own(new GameObject("FeedbackContract")); root.SetActive(false); root.transform.position = new Vector3(2000, 0);
            root.AddComponent<BoxCollider2D>().isTrigger = true; root.AddComponent<Rigidbody2D>().gravityScale = 0;
            sprite = root.AddComponent<SpriteRenderer>(); sprite.color = Color.cyan;
            attack = Add(root, "PlayerAttack2D");
            var point = new GameObject("HitPoint"); point.transform.SetParent(root.transform, false); point.transform.localPosition = Vector3.right * .8f;
            Set(attack, "hitPoint", point.transform); Set(attack, "targetLayers", (LayerMask)1);
            var feedback = Add(root, "CombatFeedback2D"); Set(feedback, "profile", profile); Set(feedback, "settings", settings);
            root.SetActive(true); return feedback;
        }
        private ScriptableObject Data(int hp)
        {
            var data = Own(ScriptableObject.CreateInstance(T("EnemyCombatDefinition")));
            Set(data, "maxHp", hp); Set(data, "hitKnockback", 0f); Set(data, "allowOffscreenStart", true);
            return data;
        }
        private Component Enemy(ScriptableObject data)
        {
            var root = Own(new GameObject("PolishEnemy")); root.SetActive(false); root.transform.position = new Vector3(2100, 0);
            root.AddComponent<BoxCollider2D>().isTrigger = true;
            var health = Add(root, "Damageable2D"); Set(health, "combatDefinition", data); root.SetActive(true); return health;
        }
        private Component Melee(ScriptableObject data, Component coordinator)
        {
            var target = Own(new GameObject("PolishTarget")); target.transform.position = new Vector3(2101, 0);
            var root = Own(new GameObject("PolishMelee")); root.SetActive(false); root.transform.position = new Vector3(2100, 0);
            root.AddComponent<BoxCollider2D>().isTrigger = true; Add(root, "Damageable2D");
            var ai = Add(root, "EnemyMeleeChaser2D"); Set(ai, "combatDefinition", data); Set(ai, "target", target.transform); Set(ai, "attackCoordinator", coordinator);
            root.SetActive(true); return ai;
        }
        private static void Present(Component feedback, string kind) => Call(feedback, "Present", Enum.Parse(T("CombatFeedbackKind"), kind), Vector2.zero, null);
        private TObj Own<TObj>(TObj value) where TObj : Object { owned.Add(value); return value; }
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Component Add(GameObject root, string type) => root.AddComponent(T(type));
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static void Static(string name, params object[] args) => T("CombatTimeController").GetMethod(name).Invoke(null, args);
        private static TObj Get<TObj>(object target, string name) => (TObj)target.GetType().GetProperty(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    }
}
