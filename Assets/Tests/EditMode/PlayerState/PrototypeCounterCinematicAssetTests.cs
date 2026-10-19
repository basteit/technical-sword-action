using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeCounterCinematicAssetTests
    {
        private const string Folder = "Assets/Settings/PrototypeCombat/";
        private static ScriptableObject Profile => AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder + "CounterCinematic.asset");
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name)?.GetValue(obj) ?? obj.GetType().GetField(name).GetValue(obj));
        private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method).Invoke(obj, args);

        [Test] public void GameplaySnapshotHasExplicitVideoTimingAndOnlyThreeRequiredMotions()
        {
            var profile = Profile; Assert.That(profile, Is.Not.Null); Assert.That(Get<bool>(profile, "IsValid"), Is.True);
            Assert.That(Get<int>(profile, "blinkStartFrame"), Is.EqualTo(10)); Assert.That(Get<int>(profile, "BlinkArrivalFrame"), Is.EqualTo(12));
            Assert.That(Get<int>(profile, "chargeFrame"), Is.EqualTo(12)); Assert.That(Get<int>(profile, "releaseFrame"), Is.EqualTo(20));
            Assert.That(Get<int>(profile, "hitFrame"), Is.EqualTo(21)); Assert.That(Get<int>(profile, "endFrame"), Is.EqualTo(45));
            Assert.That(Get<float>(profile, "chargeScale"), Is.EqualTo(.15f)); Assert.That(Get<int>(profile, "launchFrames"), Is.EqualTo(22));
            Assert.That(Get<float>(profile, "launchDistance"), Is.EqualTo(3.2f));
            Assert.That(Get<float>(profile, "cameraOrthographicSize"), Is.EqualTo(1.875f));
            Assert.That(Get<float>(profile, "cameraBlendSeconds"), Is.EqualTo(.1f));
            var art = Get<Object>(profile, "art"); Assert.That(AssetDatabase.GetAssetPath(art), Is.EqualTo(Folder + "CounterCinematicArt.asset"));
            var motions = Get<Array>(art, "motions").Cast<object>().ToArray();
            Assert.That(motions.Select(m => Get<string>(m, "name")), Is.EquivalentTo(new[] { "CounterTransfer", "CounterKick", "Fx_ElectricKick" }));
            var video = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/CombatVideoArt.asset");
            Assert.That(art, Is.Not.SameAs(video));
            foreach (var motion in motions)
            {
                var original = Call(video, "Find", Get<string>(motion, "name"));
                Assert.That(Get<Sprite[]>(motion, "frames"), Is.EqualTo(Get<Sprite[]>(original, "frames")), "Reuse received sprites, not duplicated PNGs.");
                Assert.That(Get<int[]>(motion, "durations"), Is.EqualTo(Get<int[]>(original, "durations")));
                Assert.That(Get<Sprite[]>(motion, "frames"), Is.Not.SameAs(Get<Sprite[]>(original, "frames")), "Independent timing/reference snapshot.");
            }
        }

        [Test] public void TransferAndKickClipMatchesClockWithoutChangingLegacyDataOrEvents()
        {
            var profile = Profile;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Submitted/PlayerPrototype_ParryCounter.anim");
            Assert.That(clip.length * 60, Is.EqualTo(45).Within(.001f)); Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            foreach (int frame in new[] { 0, 9, 10, 11, 12, 19, 20, 21, 22, 44 })
            {
                var sprite = keys.Last(key => key.time <= frame / 60f + .00001f).value;
                Assert.That(sprite, Is.SameAs((Sprite)Call(profile, "Sample", frame)), frame + "F");
            }
            Assert.That(Get<Sprite>(profile, "ContactSprite"), Is.SameAs(Call(profile, "Sample", 21)));
            var legacy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder + "ParryCounter.asset");
            Assert.That(Get<int>(legacy, "hitFrame"), Is.EqualTo(18)); Assert.That(Get<int>(legacy, "endFrame"), Is.EqualTo(35));
            Assert.That(Get<int>(legacy, "baseDamage"), Is.EqualTo(30)); Assert.That(Get<int>(legacy, "chainBufferFrames"), Is.EqualTo(6));
        }

        [TestCase("blinkTravelFrames", 0)] [TestCase("releaseFrame", 12)] [TestCase("endFrame", 30)] [TestCase("launchFrames", 25)]
        public void InvalidTimingIsRejectedBeforeCounterCanConsumeStock(string field, int value)
        {
            var copy = Object.Instantiate(Profile);
            try { copy.GetType().GetField(field).SetValue(copy, value); Assert.That(Get<bool>(copy, "IsValid"), Is.False); }
            finally { Object.DestroyImmediate(copy); }
        }

        [TestCase("chargeScale", 0f)] [TestCase("chargeRealSeconds", .1f)] [TestCase("hitRadius", -1f)] [TestCase("dimFadeSeconds", 0f)]
        [TestCase("cameraOrthographicSize", 0f)] [TestCase("cameraBlendSeconds", 0f)]
        public void UnsafeOrInconsistentNumericSettingsAreRejected(string field, float value)
        {
            var copy = Object.Instantiate(Profile);
            try { copy.GetType().GetField(field).SetValue(copy, value); Assert.That(Get<bool>(copy, "IsValid"), Is.False); }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test] public void SavedScenePassesDedicatedAndCommonValidation()
        {
            foreach (string name in new[] { "PrototypeCounterCinematicInstaller", "PrototypeCombatValidator" })
            {
                var type = Type.GetType(name + ", Assembly-CSharp-Editor", true);
                Assert.That((string[])type.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static).Invoke(null, null), Is.Empty);
            }
        }
    }
}
