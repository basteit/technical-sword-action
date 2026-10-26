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
