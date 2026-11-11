using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeDialogueAssetTests
    {
        private const string Folder = "Assets/Settings/PrototypeDialogue/";
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name)?.GetValue(obj) ?? obj.GetType().GetField(name).GetValue(obj));
        private static ScriptableObject Asset(string name) => AssetDatabase.LoadAssetAtPath<ScriptableObject>(Folder + name + ".asset");

        [Test]
        public void SampleUsesLeftTestRightTestLeftTestAndDistinctStableIds()
        {
            var sequence = Asset("Dialogue_PrototypeTest");
            Assert.That(sequence, Is.Not.Null);
            var lines = Get<IList>(sequence, "Lines").Cast<object>().ToArray();
            Assert.That(lines.Length, Is.EqualTo(3));
            Assert.That(lines.Select(l => Get<string>(l, "Body")), Is.All.EqualTo("Test"));
            Assert.That(lines.Select(l => Get<string>(l, "StableId")).Distinct().Count(), Is.EqualTo(3));
            Assert.That(Get<Object>(lines[0], "Speaker"), Is.SameAs(Get<Object>(lines[2], "Speaker")));
            Assert.That(Get<object>(Get<Object>(lines[0], "Speaker"), "PortraitSide").ToString(), Is.EqualTo("Left"));
            Assert.That(Get<object>(Get<Object>(lines[1], "Speaker"), "PortraitSide").ToString(), Is.EqualTo("Right"));
            Assert.That(Get<string>(sequence, "StableId"), Is.EqualTo(AssetDatabase.AssetPathToGUID(Folder + "Dialogue_PrototypeTest.asset")));
        }

        [Test]
        public void PresentationTimingAndArtSlotsAreEditableData()
        {
            var profile = Asset("Presentation"); Assert.That(profile, Is.Not.Null);
            Assert.That(Get<float>(profile, "fadeInSeconds"), Is.EqualTo(.35f));
            Assert.That(Get<float>(profile, "portraitSlideSeconds"), Is.EqualTo(.3f));
            Assert.That(Get<float>(profile, "portraitSlideDistance"), Is.EqualTo(80));
            Assert.That(Get<Vector2>(profile, "boxSize"), Is.EqualTo(new Vector2(608, 104)));
            Assert.That(Get<Object>(profile, "boxSprite"), Is.Null, "User artwork has not been received.");
            foreach (string speaker in new[] { "Speaker_PrototypeLeft", "Speaker_PrototypeRight" })
                Assert.That(Get<Object>(Asset(speaker), "DefaultPortrait"), Is.Null, "Do not label placeholder geometry as received portrait art.");
        }

        [Test]
        public void DedicatedPrefabHasExactlyTwoPortraitSlotsAndStartsHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PrototypeDialogueSystem.prefab");
            Assert.That(prefab, Is.Not.Null);
            var slots = prefab.GetComponentsInChildren(T("DialoguePortraitSlot"), true);
            Assert.That(slots.Length, Is.EqualTo(2));
            Assert.That(slots.Select(s => Get<object>(s, "Side").ToString()), Is.EquivalentTo(new[] { "Left", "Right" }));
            var view = prefab.GetComponentInChildren(T("DialogueView"), true);
            var data = new SerializedObject(view);
            foreach (string field in new[] { "root", "speakerLabel", "bodyLabel", "continueLabel", "boxImage", "leftPortrait", "rightPortrait", "presentation" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(((CanvasGroup)data.FindProperty("root").objectReferenceValue).alpha, Is.Zero);
            Assert.That(prefab.GetComponent<Canvas>().sortingOrder, Is.EqualTo(210));
        }

        [Test]
        public void PrototypeSceneOmitsDialogueTestsWithoutDeletingTheReusableSystem()
        {
            const string path = "Assets/Scenes/PrototypeCombatValidation.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Select(r => r.name), Has.None.EqualTo("InteractCounter"));
                Assert.That(roots.Select(r => r.name), Has.None.EqualTo("BasicActionPromptCanvas"));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren(T("DialogueController"), true)), Is.Empty);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren(T("DialogueInteractable2D"), true)), Is.Empty);
                var player = roots.SelectMany(r => r.GetComponentsInChildren(T("PlayerStateMachine"), true)).Single();
                Assert.That(player.GetComponent(T("PlayerInteractor2D")), Is.Not.Null, "Shared-B and future interactions remain supported.");
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PrototypeDialogueSystem.prefab"), Is.Not.Null);
                Assert.That(Asset("Dialogue_PrototypeTest"), Is.Not.Null);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void AssetCopiesReceiveNewConversationLineAndSpeakerIdsWithoutChangingOriginals()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string conversationPath = Folder + "__SequenceTest_" + suffix + ".asset";
            string speakerPath = Folder + "__SpeakerTest_" + suffix + ".asset";
            var original = Asset("Dialogue_PrototypeTest");
            var originalIds = Get<IList>(original, "Lines").Cast<object>().Select(l => Get<string>(l, "StableId")).ToArray();
            try
            {
                Assert.That(AssetDatabase.CopyAsset(Folder + "Dialogue_PrototypeTest.asset", conversationPath), Is.True);
                var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(conversationPath);
                copy.GetType().GetMethod("SynchronizeStableIdsForEditor").Invoke(copy, null);
                Assert.That(Get<string>(copy, "StableId"), Is.Not.EqualTo(Get<string>(original, "StableId")));
                Assert.That(Get<IList>(copy, "Lines").Cast<object>().Select(l => Get<string>(l, "StableId")), Has.None.Matches<string>(id => originalIds.Contains(id)));
                Assert.That(AssetDatabase.CopyAsset(Folder + "Speaker_PrototypeLeft.asset", speakerPath), Is.True);
                var speaker = AssetDatabase.LoadAssetAtPath<ScriptableObject>(speakerPath);
                speaker.GetType().GetMethod("SynchronizeStableIdForEditor").Invoke(speaker, null);
                Assert.That(Get<string>(speaker, "StableId"), Is.Not.EqualTo(Get<string>(Asset("Speaker_PrototypeLeft"), "StableId")));
                Assert.That(Get<IList>(original, "Lines").Cast<object>().Select(l => Get<string>(l, "StableId")), Is.EqualTo(originalIds));
            }
            finally
            {
                AssetDatabase.DeleteAsset(conversationPath);
                AssetDatabase.DeleteAsset(speakerPath);
            }
        }

        [Test]
        public void ValidatorReportsDuplicateAndEmptyIds()
        {
            var method = Type.GetType("DialogueAssetValidator, Assembly-CSharp-Editor", true)
                .GetMethod("ValidateStableId", BindingFlags.Static | BindingFlags.NonPublic);
            var used = new Dictionary<string, Object>(); var problems = new List<string>();
            foreach (string id in new[] { "same", "same", "" })
                method.Invoke(null, new object[] { "Test", id, "test", Asset("Presentation"), used, problems });
            Assert.That(problems.Count, Is.EqualTo(2));
            Assert.That(problems[0], Does.Contain("duplicate"));
            Assert.That(problems[1], Does.Contain("empty"));
        }

        [Test]
        public void ValidatorDetectsEmptyBodyWithoutSavingDirtyDialogue()
        {
            string path = Folder + "__EmptyBodyTest_" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                Assert.That(AssetDatabase.CopyAsset(Folder + "Dialogue_PrototypeTest.asset", path), Is.True);
                var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                var data = new SerializedObject(copy);
                data.FindProperty("lines").GetArrayElementAtIndex(0).FindPropertyRelative("body").stringValue = "";
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);
                var validator = Type.GetType("DialogueAssetValidator, Assembly-CSharp-Editor", true);
                var problems = (List<string>)validator.GetMethod("CollectProblems").Invoke(null, null);
                Assert.That(problems.Any(p => p.Contains(path) && p.Contains("empty body")), Is.True);
                Assert.That(EditorUtility.IsDirty(copy), Is.True, "Validation must not save this pending edit.");
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void ValidatorAcceptsPrototypeUnifiedLockWiring()
        {
            var problems = new List<string>();
            Type.GetType("DialogueAssetValidator, Assembly-CSharp-Editor", true)
                .GetMethod("ValidateLoadedSceneControllers", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { problems });
            Assert.That(problems, Is.Empty);
        }
    }
}
