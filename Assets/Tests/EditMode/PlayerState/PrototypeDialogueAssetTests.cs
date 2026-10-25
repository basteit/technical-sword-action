using System;
using System.Collections;
using System.Linq;
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
        public void PrototypeSceneReplacesOnlyCounterAndWiresUnifiedStateGate()
        {
            const string path = "Assets/Scenes/PrototypeCombatValidation.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var target = scene.GetRootGameObjects().Single(r => r.name == "InteractCounter");
                Assert.That(target.GetComponent(T("PrototypeInteractable2D")), Is.Null);
                var dialogue = target.GetComponent(T("DialogueInteractable2D")); Assert.That(dialogue, Is.Not.Null);
                Assert.That(target.GetComponent<Collider2D>().isTrigger, Is.True);
                Assert.That(target.transform.position.x, Is.EqualTo(-6));
                var data = new SerializedObject(dialogue);
                Assert.That(data.FindProperty("sequence").objectReferenceValue, Is.SameAs(Asset("Dialogue_PrototypeTest")));
                var controller = data.FindProperty("controller").objectReferenceValue as Component;
                Assert.That(controller, Is.Not.Null);
                var lockData = new SerializedObject(controller);
                var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(T("PlayerStateMachine"), true)).Single();
                Assert.That(lockData.FindProperty("playerStateMachine").objectReferenceValue, Is.SameAs(player));
                Assert.That(lockData.FindProperty("playerBody").objectReferenceValue, Is.SameAs(player.GetComponent<Rigidbody2D>()));
                Assert.That(lockData.FindProperty("gameplayBehavioursToDisable").arraySize, Is.Zero, "Keep combat participants registered.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
