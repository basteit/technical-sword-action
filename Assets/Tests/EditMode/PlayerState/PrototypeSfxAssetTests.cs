using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeSfxAssetTests
    {
        private const string ProfilePath = "Assets/Settings/PrototypeCombat/FreeSfx.asset";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target) ?? target.GetType().GetField(name, Flags).GetValue(target);
        [Test] public void PaletteCoversEveryCueWithValidPublicDomainClips()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            Assert.That(profile, Is.Not.Null); Assert.That(Get(profile, "IsValid"), Is.True);
            var entries = (Array)Get(profile, "entries"); Assert.That(entries.Length, Is.EqualTo(30));
            foreach (var entry in entries)
            {
                foreach (var clip in (AudioClip[])Get(entry, "clips"))
                {
                    Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith("Assets/Audio/PrototypeFreeSfx/"));
                    Assert.That(clip.length, Is.InRange(.1f, 1f)); Assert.That(clip.channels, Is.EqualTo(1));
                    Assert.That(clip.frequency, Is.EqualTo(48000));
                }
            }
        }
        [Test] public void AllProcessedClipsArePreloadedPcmWithHeadroom()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/PrototypeFreeSfx" });
            Assert.That(clips.Length, Is.EqualTo(24));
            foreach (var guid in clips)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM), path);
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True, path);
                Assert.That(clip.LoadAudioData(), Is.True, path);
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True, path);
                float peak = samples.Max(Mathf.Abs);
                Assert.That(peak, Is.InRange(.48f, .52f), path);
            }
        }
        [Test] public void PrototypeHasNoDependencyOnOldSoundPaths()
        {
            var paths = AssetDatabase.GetDependencies("Assets/Scenes/PrototypeCombatValidation.unity", true);
            Assert.That(paths, Does.Contain(ProfilePath));
            Assert.That(paths.Where(p => p.StartsWith("Assets/Audio/HitSound_")), Is.Empty);
            Assert.That(paths.Count(p => p.StartsWith("Assets/Audio/PrototypeFreeSfx/") && p.EndsWith(".wav")), Is.EqualTo(24));
        }
        [TestCase("volume", float.NaN)] [TestCase("volume", 1.1f)] [TestCase("volume", -.01f)]
        [TestCase("pitch", float.PositiveInfinity)] [TestCase("pitch", .4f)]
        [TestCase("pitchVariation", float.NaN)] [TestCase("pitchVariation", .11f)]
        public void InvalidAudioTuningIsRejected(string field, float value)
        {
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var entry = ((Array)Get(copy, "entries")).GetValue(0);
                entry.GetType().GetField(field).SetValue(entry, value);
                Assert.That(Get(copy, "IsValid"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
