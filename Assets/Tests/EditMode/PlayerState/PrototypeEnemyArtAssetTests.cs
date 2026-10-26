using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeEnemyArtAssetTests
    {
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static V Get<V>(object obj, string name) => (V)(obj.GetType().GetProperty(name)?.GetValue(obj) ?? obj.GetType().GetField(name).GetValue(obj));
        private static object Find(object obj, string name) => obj.GetType().GetMethod("Find").Invoke(obj, new object[] { name });

        [TestCase(30)] [TestCase(96)]
        public void ExistingLargeFlashIsHeldForSixTicksAndStartsFourteenTicksBeforeContact(int total)
        {
            var art = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/NormalMeleeArt.asset");
            var windup = Find(art, "Enemy_Windup");
            Assert.That(Get<bool>(windup, "HasValidParryCue"), Is.True);
            Assert.That(Get<int>(windup, "parryCueFrame"), Is.EqualTo(66));
            Assert.That(Get<int>(windup, "ParryCueCelFrames"), Is.EqualTo(6));
            var map = T("PrototypeEnemyArtPresentation").GetMethod("MapWindupFrame");
            var at = windup.GetType().GetMethod("At"); var source = Get<Sprite[]>(windup, "frames");
            var shown = Enumerable.Range(0, total).Select(frame =>
                (Sprite)at.Invoke(windup, new object[] { (int)map.Invoke(null, new object[] { windup, frame, total, 14 }), false })).ToArray();
            Assert.That(Array.FindIndex(shown, sprite => sprite == source[7]), Is.EqualTo(total - 14));
            Assert.That(shown.Count(sprite => sprite == source[7]), Is.EqualTo(6));
            Assert.That(shown, Is.SupersetOf(source), "Do not drop submitted poses while retiming the flash.");
            Assert.That(shown.Last(), Is.SameAs(source.Last()));
        }

        [Test] public void VideoLibraryWithoutGameplayCueMetadataKeepsLinearSampling()
        {
            var video = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/CombatVideoArt.asset");
            var windup = Find(video, "Enemy_Windup"); Assert.That(Get<bool>(windup, "hasParryCue"), Is.False);
            var map = T("PrototypeEnemyArtPresentation").GetMethod("MapWindupFrame"); int frames = Get<int>(windup, "TotalFrames");
            for (int frame = 0; frame < 96; frame++)
                Assert.That(map.Invoke(null, new object[] { windup, frame, 96, 12 }), Is.EqualTo(Mathf.FloorToInt(frame * frames / 96f)));
        }

        [Test]
        public void SnapshotContainsOnlySevenEnemyMotionsAndKeepsReceivedSprites()
        {
            var art = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/NormalMeleeArt.asset");
            var video = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/CombatVideoArt.asset");
            Assert.That(art, Is.Not.Null); Assert.That(art, Is.Not.SameAs(video));
            var motions = Get<Array>(art, "motions").Cast<object>().ToArray();
            Assert.That(motions.Select(m => Get<string>(m, "name")), Is.EquivalentTo(new[] {
                "Enemy_Idle", "Enemy_Move", "Enemy_Windup", "Enemy_Attack", "Enemy_Hit", "Enemy_Parried", "Enemy_Death" }));
            foreach (var motion in motions)
            {
                var original = Find(video, Get<string>(motion, "name"));
                Assert.That(Get<Sprite[]>(motion, "frames"), Is.EqualTo(Get<Sprite[]>(original, "frames")));
                Assert.That(Get<int[]>(motion, "durations"), Is.EqualTo(Get<int[]>(original, "durations")));
                Assert.That(Get<int[]>(motion, "durations"), Is.Not.SameAs(Get<int[]>(original, "durations")));
                foreach (Sprite sprite in Get<Sprite[]>(motion, "frames"))
                {
                    Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith("Assets/Art/Enemy/Submitted/20261019/"));
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                }
            }
        }

        [Test]
        public void CombatNumbersAndPlayerAttackTempoRemainUnchanged()
        {
            var enemy = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/NormalMelee.asset");
            Assert.That(Get<int>(enemy, "maxHp"), Is.EqualTo(30)); Assert.That(Get<int>(enemy, "damage"), Is.EqualTo(10));
            Assert.That(Get<int>(enemy, "windupFrames"), Is.EqualTo(30)); Assert.That(Get<int>(enemy, "parryStaggerFrames"), Is.EqualTo(180));
            var attack = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/FourStepAttack.asset");
            var steps = Get<Array>(attack, "steps").Cast<object>().ToArray();
            Assert.That(steps.Select(s => Get<int>(s, "activeStart")), Is.EqualTo(new[] { 15, 12, 15, 38 }));
            Assert.That(steps.Select(s => Get<int>(s, "cancelStart")), Is.EqualTo(new[] { 17, 14, 17, 40 }));
        }

        [Test]
        public void SceneKeepsBodyAndLaunchOwnersButHidesOnlyOldNormalEnemyDrawing()
        {
            const string path = "Assets/Scenes/PrototypeCombatValidation.unity";
            Scene scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var enemies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(T("EnemyMeleeChaser2D"), true)).ToArray();
                Assert.That(enemies.Length, Is.EqualTo(3));
                foreach (var enemy in enemies.Where(e => e.name.StartsWith("Melee_")))
                {
                    var art = enemy.GetComponent(T("PrototypeEnemyArtPresentation")); Assert.That(art, Is.Not.Null);
                    var drawing = Get<SpriteRenderer>(art, "Artwork");
                    Assert.That(drawing.transform.parent.name, Is.EqualTo("CounterLaunchVisual"));
                    Assert.That(drawing.transform.GetSiblingIndex(), Is.Zero);
                    Assert.That(drawing.transform.localPosition.y, Is.EqualTo(.2f).Within(.0001f));
                    var shape = enemy.GetComponent<BoxCollider2D>(); Assert.That(shape.size, Is.EqualTo(new Vector2(.7f, 1.6f)));
                    var health = new SerializedObject(enemy.GetComponent(T("Damageable2D")));
                    Assert.That(health.FindProperty("spriteRenderer").objectReferenceValue, Is.SameAs(drawing));
                    Assert.That(enemy.GetComponent(T("VideoEnemyAnimationDriver")), Is.Null);
                    Assert.That(enemy.gameObject.activeSelf, Is.False);
                    var cue = new SerializedObject(enemy.GetComponent(T("EnemyCombatCue2D")));
                    foreach (string field in new[] { "silhouette", "weapon" })
                        Assert.That(((Transform)cue.FindProperty(field).objectReferenceValue).gameObject.activeSelf, Is.False);
                }
                Assert.That(enemies.Single(e => e.name == "StrongMelee").GetComponent(T("PrototypeEnemyArtPresentation")), Is.Null);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
