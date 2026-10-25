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

        [Test] public void ExistingSpriteContactRemainsWithinTwoTicksAtTrialSpeed()
        {
            var art = Load("PrototypePlayerArtSet"); var fast = Steps(Load("FourStepAttack_FastTrial"));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Animations/Player/Submitted/PlayerSubmitted_FastTrial.overrideController");
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
