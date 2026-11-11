using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeCombatTrialAssetTests
    {
        private const string Folder = "Assets/Settings/PrototypeCombat/";
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name)?.GetValue(obj) ?? obj.GetType().GetField(name).GetValue(obj));
        private static ScriptableObject Load(string name) => AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder + name + ".asset");
        private static object[] Steps(ScriptableObject definition) => Get<Array>(definition, "steps").Cast<object>().ToArray();
        private static string[] Validate() => (string[])Type.GetType("PrototypeCombatTrialInstaller, Assembly-CSharp-Editor", true).GetMethod("Validate").Invoke(null, null);

        [TestCase("FourStepAttack")] [TestCase("FourStepAttack_FastTrial")] [TestCase("FourStepAttack_SnappyTrial")]
        public void HorizontalReachMatchesTheExistingFirstTwoSlashSpritesWithoutGrowingHeight(string name)
        {
            var steps = Steps(Load(name));
            Assert.That(steps.Select(s => Get<float>(s, "forwardExtension")), Is.EqualTo(new[] { .6125f, .95625f, 0, 0 }));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(Get<float>(steps[i], "radius"), Is.EqualTo(.8f));
                var size = Get<Vector2>(steps[i], "HitboxSize");
                Assert.That(size.y, Is.EqualTo(1.6f));
                var centerMethod = steps[i].GetType().GetMethod("HitboxCenter");
                foreach (int direction in new[] { -1, 1 })
                {
                    var center = (Vector2)centerMethod.Invoke(steps[i], new object[] { new Vector2(.9f * direction, 0), direction });
                    float front = center.x * direction + size.x / 2;
                    float rear = center.x * direction - size.x / 2;
                    Assert.That(front, Is.EqualTo(i == 0 ? 2.3125f : i == 1 ? 2.65625f : 1.7f).Within(.00001f));
                    Assert.That(rear, Is.EqualTo(.1f).Within(.00001f));
                }
            }
        }

        [Test] public void InvalidForwardExtensionsAreRejectedAndZeroRestoresTheOriginalCircle()
        {
            var copy = UnityEngine.Object.Instantiate(Load("FourStepAttack"));
            try
            {
                var step = Steps(copy)[0]; var field = step.GetType().GetField("forwardExtension");
                foreach (float invalid in new[] { -.1f, float.NaN, float.PositiveInfinity })
                { field.SetValue(step, invalid); Assert.That(Get<bool>(copy, "IsValid"), Is.False); }
                field.SetValue(step, 0f); Assert.That(Get<bool>(copy, "IsValid"), Is.True);
                Assert.That(Get<Vector2>(step, "HitboxSize"), Is.EqualTo(new Vector2(1.6f, 1.6f)));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test] public void ParryCueLeadIsExplicitPerEnemyAndPerAttackAndTimingWindowsAreUnchanged()
        {
            foreach (string name in new[] { "NormalMelee", "NormalMelee_ReferenceTrial", "StrongMelee", "StrongSlash", "StrongLowSweep" })
                Assert.That(Get<int>(Load(name), "parryCueLeadFrames"), Is.EqualTo(name.StartsWith("NormalMelee") ? 14 : 12), name);
            var timing = Load("ParryTiming");
            Assert.That(Get<int>(timing, "startFrame"), Is.Zero);
            Assert.That(Get<int>(timing, "justEndFrame"), Is.EqualTo(3));
            Assert.That(Get<int>(timing, "endFrame"), Is.EqualTo(13));
        }

        [Test] public void HeavyImpactTrialIsNormalAttackOnlyAndInheritsLaterSteps()
        {
            var impact = Load("AttackImpact_HeavyTrial");
            Assert.That(Get<bool>(impact, "IsValid"), Is.True);
            Assert.That(Get<int[]>(impact, "stepHitstopFrames"), Is.EqualTo(new[] { 8, 8, -1, -1 }));
            var resolve = impact.GetType().GetMethod("Resolve");
            Assert.That(resolve.Invoke(impact, new object[] { 1, 3 }), Is.EqualTo(8));
            Assert.That(resolve.Invoke(impact, new object[] { 2, 3 }), Is.EqualTo(8));
            Assert.That(resolve.Invoke(impact, new object[] { 3, 3 }), Is.EqualTo(3));
            Assert.That(resolve.Invoke(impact, new object[] { 4, 5 }), Is.EqualTo(5));
            var entries = Get<Array>(Load("CombatFeedback"), "entries").Cast<object>().ToArray();
            Assert.That(Get<int>(entries.Single(entry => Get<object>(entry, "kind").ToString() == "LightHit"), "hitstopFrames"), Is.EqualTo(3));
            Assert.That(Get<int>(entries.Single(entry => Get<object>(entry, "kind").ToString() == "HeavyHit"), "hitstopFrames"), Is.EqualTo(5));
        }

        [Test] public void ImpactProfileValidatesAllFourStepsAndAllowsExplicitZero()
        {
            var impact = UnityEngine.Object.Instantiate(Load("AttackImpact_HeavyTrial"));
            var field = impact.GetType().GetField("stepHitstopFrames");
            try
            {
                field.SetValue(impact, new[] { 0, 0, -1, -1 });
                Assert.That(Get<bool>(impact, "IsValid"), Is.True);
                Assert.That(impact.GetType().GetMethod("Resolve").Invoke(impact, new object[] { 1, 3 }), Is.EqualTo(0));
                field.SetValue(impact, new[] { 8, -2, -1, -1 }); Assert.That(Get<bool>(impact, "IsValid"), Is.False);
                field.SetValue(impact, new[] { 8, 8 }); Assert.That(Get<bool>(impact, "IsValid"), Is.False);
                field.SetValue(impact, null); Assert.That(Get<bool>(impact, "IsValid"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(impact); }
        }

        [TestCase("FastTrial")] [TestCase("SnappyTrial")]
        public void EditorValidatorAcceptsInstalledTrialAndDetectsAThreeTickDesync(string mode)
        {
            Assert.That(Validate(), Is.Empty);
            object step = Steps(Load("FourStepAttack_" + mode))[0];
            var names = new[] { "activeStart", "activeEnd", "cancelStart" };
            var values = names.Select(name => Get<int>(step, name)).ToArray();
            try
            {
                for (int i = 0; i < names.Length; i++) step.GetType().GetField(names[i]).SetValue(step, values[i] + 3);
                Assert.That(Validate(), Has.Some.Contains("more than 2F"));
            }
            finally { for (int i = 0; i < names.Length; i++) step.GetType().GetField(names[i]).SetValue(step, values[i]); }
            Assert.That(Validate(), Is.Empty);
        }

        [Test] public void TrialKeepsOriginalDefinitionsClipsDamageAndGauge()
        {
            var original = Steps(Load("FourStepAttack")); var fast = Steps(Load("FourStepAttack_FastTrial"));
            Assert.That(Get<bool>(Load("FourStepAttack_FastTrial"), "IsValid"), Is.True);
            Assert.That(original.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 15, 12, 15, 38 }));
            Assert.That(fast.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 12, 9, 12, 30 }));
            Assert.That(fast.Select(s => Get<int>(s, "endFrame")), Is.EqualTo(new[] { 30, 24, 30, 49 }));
            for (int i = 0; i < 4; i++)
            {
                foreach (string field in new[] { "damage", "gaugeGain", "motionFrames" }) Assert.That(Get<int>(fast[i], field), Is.EqualTo(Get<int>(original[i], field)));
                foreach (string field in new[] { "radius", "motionDistance" }) Assert.That(Get<float>(fast[i], field), Is.EqualTo(Get<float>(original[i], field)));
                Assert.That(Get<AnimationClip>(fast[i], "clip"), Is.SameAs(Get<AnimationClip>(original[i], "clip")));
                Assert.That(Get<int>(fast[i], "activeEnd") - Get<int>(fast[i], "activeStart"), Is.EqualTo(1));
                Assert.That(Get<int>(fast[i], "cancelStart"), Is.EqualTo(Get<int>(fast[i], "activeEnd") + 1));
            }
        }

        [TestCase("FastTrial")] [TestCase("SnappyTrial")]
        public void ExistingSpriteContactRemainsWithinTwoTicksAtTrialSpeed(string mode)
        {
            var art = Load("PrototypePlayerArtSet"); var fast = Steps(Load("FourStepAttack_" + mode));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animations/Player/Submitted/PlayerSubmitted_" + mode + ".overrideController");
            for (int i = 0; i < 4; i++)
            {
                var entry = art.GetType().GetMethod("Find").Invoke(art, new object[] { "Attack_" + (i + 1) });
                var clip = controller[Get<AnimationClip>(fast[i], "clip")]; var contact = Get<Sprite>(entry, "contactSprite");
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
                var firstContact = AnimationUtility.GetObjectReferenceCurve(clip, binding).First(k => k.value == contact);
                float definitionLength = Get<AnimationClip>(fast[i], "clip").length;
                float shownAt = firstContact.time / definitionLength * Get<int>(fast[i], "endFrame");
                Assert.That(Mathf.Abs(shownAt - Get<int>(fast[i], "activeStart")), Is.LessThanOrEqualTo(2), "step " + (i + 1));
            }
        }

        [Test] public void SnappyTrialChangesOnlyFirstTwoTimelinesAndGroundBursts()
        {
            var fast = Steps(Load("FourStepAttack_FastTrial")); var snappy = Steps(Load("FourStepAttack_SnappyTrial"));
            Assert.That(Get<bool>(Load("FourStepAttack_SnappyTrial"), "IsValid"), Is.True);
            Assert.That(snappy.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 9, 7, 12, 30 }));
            Assert.That(snappy.Select(s => Get<int>(s, "cancelStart")), Is.EqualTo(new[] { 11, 9, 14, 32 }));
            Assert.That(snappy.Select(s => Get<int>(s, "recoveryStart")), Is.EqualTo(new[] { 22, 18, 28, 47 }));
            Assert.That(snappy.Select(s => Get<int>(s, "endFrame")), Is.EqualTo(new[] { 24, 20, 30, 49 }));
            Assert.That(snappy.Select(s => Get<float>(s, "motionDistance")), Is.EqualTo(new[] { .25f, .30f, .18f, .22f }));
            Assert.That(snappy.Select(s => Get<int>(s, "motionFrames")), Is.EqualTo(new[] { 6, 6, 8, 8 }));
            var changed = new[] { "activeStart", "activeEnd", "cancelStart", "recoveryStart", "endFrame", "motionDistance", "motionFrames" };
            for (int i = 0; i < 4; i++)
                foreach (var field in fast[i].GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (i < 2 && changed.Contains(field.Name)) continue;
                    Assert.That(field.GetValue(snappy[i]), Is.EqualTo(field.GetValue(fast[i])), "step " + (i + 1) + " / " + field.Name);
                }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animations/Player/Submitted/PlayerSubmitted_SnappyTrial.overrideController");
            var previous = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animations/Player/Submitted/PlayerSubmitted_FastTrial.overrideController");
            var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            previous.GetOverrides(overrides);
            foreach (var pair in overrides)
            {
                if (pair.Key == Get<AnimationClip>(fast[0], "clip") || pair.Key == Get<AnimationClip>(fast[1], "clip"))
                    Assert.That(controller[pair.Key], Is.Not.SameAs(pair.Value));
                else Assert.That(controller[pair.Key], Is.SameAs(pair.Value), pair.Key.name + " must remain unchanged, including parry/counter and steps 3/4.");
            }
        }

        [Test] public void EnemyAndLaunchComparisonsPreserveAllOtherCombatSettings()
        {
            var original = Load("NormalMelee"); var reference = Load("NormalMelee_ReferenceTrial");
            Assert.That(Get<int>(original, "windupFrames"), Is.EqualTo(30));
            Assert.That(Get<int>(reference, "windupFrames"), Is.EqualTo(96));
            Assert.That(Get<int>(reference, "recoveryFrames"), Is.EqualTo(24));
            foreach (var field in original.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == "windupFrames" || field.Name == "recoveryFrames") continue;
                Assert.That(field.GetValue(reference), Is.EqualTo(field.GetValue(original)), field.Name);
            }
            var baseline = Load("CounterCinematic"); var launch = Load("CounterLaunch_ReferenceTrial");
            Assert.That(Get<bool>(launch, "IsValid"), Is.True);
            Assert.That(Get<float>(baseline, "launchDistance"), Is.EqualTo(3.2f));
            Assert.That(Get<float>(baseline, "launchRotationDegrees"), Is.EqualTo(95));
            Assert.That(Get<float>(launch, "launchDistance"), Is.EqualTo(1.6f));
            Assert.That(Get<float>(launch, "launchHeight"), Is.EqualTo(.27f));
            Assert.That(Get<int>(launch, "launchFrames"), Is.EqualTo(18));
            Assert.That(Get<float>(launch, "launchRotationDegrees"), Is.Zero);
            foreach (var field in baseline.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name.StartsWith("launch")) continue;
                Assert.That(field.GetValue(launch), Is.EqualTo(field.GetValue(baseline)), field.Name);
            }
        }
    }
}
