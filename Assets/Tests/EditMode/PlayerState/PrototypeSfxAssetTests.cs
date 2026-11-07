using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
        [Test] public void PaletteCoversEveryCueWithValidLicensedClips()
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
        private static object EntryFor(string cue)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            return ((Array)Get(profile, "entries")).Cast<object>().Single(e => Get(e, "cue").ToString() == cue);
        }
        private static string Sha256(string path)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        [Test] public void ApprovedJumpBytesAndPlaybackTuningAreUnchanged()
        {
            Assert.That(Sha256("Assets/Audio/PrototypeFreeSfx/Jump.wav"), Is.EqualTo("9e923b4a2ffe056a4eb271416676f11c95fb141a7e87f74a26822a260f882c7c"));
            var entry = EntryFor("Jump");
            Assert.That(Get(entry, "volume"), Is.EqualTo(.25f));
            Assert.That(Get(entry, "pitch"), Is.EqualTo(1f));
            Assert.That(Get(entry, "pitchVariation"), Is.EqualTo(.025f));
        }
        [TestCase("Land", "19cc52d95a407006651edd3d44d48a8e5239c579271bf5f77d6722408dc49f43")]
        [TestCase("ParryClang", "ff5535dc87648c7e0a5ee36594be7420e624d3583dfc35c679751849769cf5ea")]
        [TestCase("SwordA", "7aea953a5d6e02f0e706db44ee243dd21f4c54d27cb8c91238bde86f3444623b")]
        [TestCase("SwordB", "38eeaecb54b5a43669af92c0af31acaefef80fde64243236e9d9076ee0ba1088")]
        [TestCase("SwordIai", "590107f74af80149fd8069e97f4709abca6fcc2d3d3764c8db067d62948cf936")]
        [TestCase("ParryReady", "aae2b7bb4c9014b1cd84d7cd0814a5a66368b133c3eb1ab34736150318c2e073")]
        [TestCase("Dash", "a2e34e96e9703534012aeeb50766db1a26134c3524f9da431ebf29a45a621209")]
        public void ApprovedLandingParryAndWhiffsRemainByteIdentical(string name, string expected)
        { Assert.That(Sha256("Assets/Audio/PrototypeFreeSfx/" + name + ".wav"), Is.EqualTo(expected)); }
        [Test] public void WhiffsHitsAndParriesUseDifferentClips()
        {
            var contacts = new[] { "LightHit", "HeavyHit", "Parry", "JustParry" }
                .SelectMany(cue => (AudioClip[])Get(EntryFor(cue), "clips")).ToArray();
            foreach (var cue in new[] { "Swing1", "Swing2", "Swing3", "Swing4" })
                foreach (var clip in (AudioClip[])Get(EntryFor(cue), "clips"))
                    CollectionAssert.DoesNotContain(contacts, clip, cue);
            Assert.That(Get(EntryFor("Parry"), "clips"), Is.Not.EqualTo(Get(EntryFor("LightHit"), "clips")));
        }
        [Test] public void LandingIsShorterAndQuieterThanJump()
        {
            var land = EntryFor("Land"); var jump = EntryFor("Jump");
            Assert.That(((AudioClip[])Get(land, "clips"))[0].length, Is.InRange(.15f, .21f));
            Assert.That((float)Get(land, "volume"), Is.LessThan((float)Get(jump, "volume")));
        }
        [Test] public void RequiredAudioCreditsAreIncludedInStreamingAssets()
        {
            string text = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "AudioCredits.txt"));
            foreach (var credit in new[] { "Mike Koenig", "Mark DiAngelo", "Sound Explorer", "Sword Swing", "Swords Collide", "Swords Clashing", "Ninja Samurai Sword", "Electricity", "Woosh", "Kick", "Realistic Punch", "https://creativecommons.org/licenses/by/3.0/", "Modified", "Joseph SARDIN", "Kenney" })
                Assert.That(text, Does.Contain(credit));
            // StreamingAssets are copied verbatim; Unity intentionally uses DefaultImporter, not TextAsset.
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/StreamingAssets/AudioCredits.txt"), Is.Not.Empty);
        }
        [Serializable] private sealed class AudioManifest { public ClipRecord[] clips = Array.Empty<ClipRecord>(); }
        [Serializable] private sealed class ClipRecord { public string asset = ""; public string sha256 = ""; public string license = ""; public SourceRecord[] sources = Array.Empty<SourceRecord>(); }
        [Serializable] private sealed class SourceRecord { public string path = ""; public string sha256 = ""; public string author = ""; public string title = ""; public string license = ""; public string url = ""; }
        [Test] public void CounterElectricityIsDisabledInTheShippedPalette()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            Assert.That(Get(profile,"counterAccentEnabled"),Is.False);
            Assert.That(((AudioClip[])Get(EntryFor("CounterCharge"),"clips")),Is.Not.Empty,
                "Keep the optional accent available without enabling it by default.");
        }
        [Test] public void SparkleWindElectricityAndContactsHaveSeparateRoles()
        {
            var cues = new[] { "ParryReady", "Dash", "CounterCharge", "CounterHit", "LightHit", "Parry" };
            var clips = cues.Select(cue => ((AudioClip[])Get(EntryFor(cue), "clips"))[0]).ToArray();
            Assert.That(clips.Distinct().Count(), Is.EqualTo(cues.Length));
            var manifest = JsonUtility.FromJson<AudioManifest>(File.ReadAllText("ThirdParty/PrototypeAudio/manifest.json"));
            foreach (var name in new[] { "LightHit", "HeavyHit" })
            {
                var sources = manifest.clips.Single(c => c.asset.EndsWith("/" + name + ".wav")).sources;
                CollectionAssert.Contains(sources.Select(s => s.title).ToArray(), "Woosh");
                Assert.That(sources.Any(s => s.title.StartsWith("knifeSlice")),Is.True);
                Assert.That(sources.Any(s => s.title.Contains("Ninja") || s.title == "chop" || s.title.StartsWith("Swords ") ||
                    s.title.StartsWith("laser") || s.title.StartsWith("explosion") || s.title.StartsWith("impactMetal")),
                    Is.False, "Cutting uses air and blade sweeps, not gore, clangs or electronic layers.");
            }
            var charge = manifest.clips.Single(c => c.asset.EndsWith("/CounterCharge.wav"));
            CollectionAssert.Contains(charge.sources.Select(s => s.title).ToArray(), "Electricity");
            var hit = manifest.clips.Single(c => c.asset.EndsWith("/CounterImpact.wav"));
            CollectionAssert.AreEquivalent(new[] { "Realistic Punch", "impactPunch_heavy_004", "impactPlate_medium_000", "Woosh" },
                hit.sources.Select(s => s.title).ToArray());
            var dash = manifest.clips.Single(c => c.asset.EndsWith("/Dash.wav"));
            CollectionAssert.AreEqual(new[] { "Woosh" }, dash.sources.Select(s => s.title).ToArray());
        }
        [Test] public void StylizedContactsAndElectricAccentHaveShortEnvelopes()
        {
            Assert.That(((AudioClip[])Get(EntryFor("CounterCharge"), "clips"))[0].length, Is.InRange(.16f,.20f));
            var kick = EntryFor("CounterHit");
            Assert.That(((AudioClip[])Get(kick, "clips"))[0].length, Is.InRange(.32f,.36f));
            Assert.That((float)Get(kick, "volume"), Is.EqualTo(1f));
            Assert.That(((AudioClip[])Get(EntryFor("LightHit"), "clips"))[0].length, Is.InRange(.19f,.23f));
            Assert.That(((AudioClip[])Get(EntryFor("HeavyHit"), "clips"))[0].length, Is.InRange(.26f,.30f));
        }
        [Test] public void CombinedManifestMatchesAllOriginalAndProcessedBytes()
        {
            var manifest = JsonUtility.FromJson<AudioManifest>(File.ReadAllText("ThirdParty/PrototypeAudio/manifest.json"));
            Assert.That(manifest.clips.Length, Is.EqualTo(24));
            foreach (var clip in manifest.clips)
            {
                Assert.That(Sha256(clip.asset), Is.EqualTo(clip.sha256), clip.asset);
                Assert.That(clip.license, Is.EqualTo("CC0-1.0").Or.EqualTo("CC-BY-3.0"));
                Assert.That(clip.sources, Is.Not.Empty);
                foreach (var source in clip.sources)
                {
                    Assert.That(Sha256(source.path), Is.EqualTo(source.sha256), source.path);
                    Assert.That(source.author, Is.Not.Empty); Assert.That(source.title, Is.Not.Empty);
                    Assert.That(source.url, Does.StartWith("https://"));
                    Assert.That(source.license, Is.EqualTo("CC0-1.0").Or.EqualTo("CC-BY-3.0"));
                }
            }
        }
        [TestCase(-1)] [TestCase(61)]
        public void InvalidCounterAccentOffsetIsRejected(int value)
        {
            var copy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath));
            try
            {
                copy.GetType().GetField("counterAccentOffsetFrames").SetValue(copy,value);
                Assert.That(Get(copy,"IsValid"),Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
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
