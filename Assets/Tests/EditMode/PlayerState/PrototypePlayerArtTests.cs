using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypePlayerArtTests
    {
        private const string Folder = "Assets/Animations/Player/Submitted/";
        private static readonly EditorCurveBinding Binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        private static AnimationClip Clip(string action) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "PlayerPrototype_" + action + ".anim");
        private static ObjectReferenceKeyframe[] Keys(string action) => AnimationUtility.GetObjectReferenceCurve(Clip(action), Binding);
        private static object Set => AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/PrototypePlayerArtSet.asset");
        private static object Entry(string action) => Set.GetType().GetMethod("Find").Invoke(Set, new object[] { action });
        private static object Field(object obj, string name) => obj.GetType().GetField(name).GetValue(obj);

        [Test] public void AllTwentyFourOverridesHaveValidReferencesAndNoLegacyArt()
        {
            var installer = Type.GetType("PrototypePlayerArtInstaller, Assembly-CSharp-Editor", true);
            var errors = (string[])installer.GetMethod("Validate", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
            Assert.That(errors, Is.Empty);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Folder + "PlayerSubmitted.overrideController");
            Assert.That(controller.overridesCount, Is.EqualTo(24));
            var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            foreach (var pair in overrides)
            {
                Assert.That(pair.Value, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(pair.Value), Does.StartWith(Folder));
                Assert.That(pair.Value.name, Is.EqualTo(pair.Key.name));
                Assert.That(AnimationUtility.GetCurveBindings(pair.Value), Is.Empty, "No transform, physics or color animation.");
            }
        }

        [Test] public void PresentationControllerPreservesAllStatesAndDoesNotRestartDirectlyPlayedAttacks()
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/Prototype/PlayerPrototypeAnimator.controller");
            var updated = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "PlayerSubmittedBase.controller");
            Assert.That(updated.layers[0].stateMachine.states.Select(s => s.state.name).OrderBy(n => n),
                Is.EqualTo(original.layers[0].stateMachine.states.Select(s => s.state.name).OrderBy(n => n)));
            Assert.That(updated.parameters.Select(p => (p.name, p.type)), Is.EqualTo(original.parameters.Select(p => (p.name, p.type))));
            foreach (var transition in updated.layers[0].stateMachine.anyStateTransitions)
                if (transition.destinationState.name.StartsWith("PlayerPrototype_Attack_"))
                    Assert.That(transition.canTransitionToSelf, Is.False);
        }

        [TestCase("Idle", 6, .1f)] [TestCase("Move", 8, .08f)]
        public void ReceivedLocomotionKeepsItsOriginalCadence(string action, int count, float celSeconds)
        {
            var keys = Keys(action);
            Assert.That(keys.Length, Is.EqualTo(count + 1));
            for (int i = 0; i < count; i++) Assert.That(keys[i].time, Is.EqualTo(i * celSeconds).Within(.00001f));
            Assert.That(Clip(action).length, Is.EqualTo(count * celSeconds).Within(.00001f));
            Assert.That(keys[^2].value, Is.SameAs(keys[^1].value));
            Assert.That(AnimationUtility.GetAnimationClipSettings(Clip(action)).loopTime, Is.True);
            Assert.That(Field(Entry(action), "status").ToString(), Is.EqualTo("Received"));
        }

        [TestCase(1, "VerticalSlash", 15, 38)]
        [TestCase(2, "HorizontalSlash", 12, 32)]
        [TestCase(3, "RapidSheathe", 15, 38)]
        [TestCase(4, "IaiSlash", 38, 62)]
        public void FourAttacksKeepEventsAndExposeContactAtConfiguredTick(int step, string source, int hit, int end)
        {
            string action = "Attack_" + step;
            var original = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Prototype/PlayerPrototype_" + action + ".anim");
            var clip = Clip(action); var entry = Entry(action);
            Assert.That(Field(entry, "source"), Is.EqualTo(source));
            Assert.That(clip.length, Is.EqualTo(original.length).Within(.00001f));
            var events = AnimationUtility.GetAnimationEvents(clip);
            var oldEvents = AnimationUtility.GetAnimationEvents(original);
            Assert.That(events.Select(e => (e.time, e.functionName, e.intParameter)),
                Is.EqualTo(oldEvents.Select(e => (e.time, e.functionName, e.intParameter))));
            var contact = (Sprite)Field(entry, "contactSprite");
            var firstContact = Keys(action).First(key => key.value == contact);
            float clockFrame = firstContact.time / (original.length / (end / 60f)) * 60;
            Assert.That(clockFrame, Is.EqualTo(hit).Within(.01f), "Contact cel onset, not just a held middle frame.");
        }

        [Test] public void ParryDoesNotShowASuccessBeforeContactAndMissingHeavyUsesExistingLight()
        {
            Assert.That(Field(Entry("Parry"), "source"), Is.EqualTo("ParryReady"));
            Assert.That(Field(Entry("ParrySuccess"), "source"), Is.EqualTo("ParrySuccessLight"));
            Assert.That(Keys("Parry").Select(key => key.value).Intersect(Keys("ParrySuccess").Select(key => key.value)), Is.Empty);
            Assert.That(Keys("Parry").Last().value, Is.SameAs(Keys("Parry")[2].value), "Hold the ready cel, never deflect on an empty input.");
            var success=Keys("ParrySuccess").Select(key=>(Sprite)key.value).Distinct().ToArray();
            Assert.That(success.Length,Is.EqualTo(8),"The revised light parry must include its authored spark and decay cels.");
            Assert.That(success.All(sprite=>sprite.rect.size==new Vector2(128,128)&&sprite.pivot==new Vector2(32,38)&&sprite.pixelsPerUnit==32),Is.True);
        }

        [Test] public void JumpSamplesAreKeptAsOriginalsAndUnusedActionsAreNotClaimedFinished()
        {
            var feet = (Sprite[])Field(Set, "feetPivotSprites");
            Assert.That(feet.Length, Is.EqualTo(6));
            Assert.That(feet.All(sprite => Mathf.Approximately(sprite.pixelsPerUnit, 100)), Is.True);
            Assert.That(Keys("Jump").Select(key => key.value).Distinct().Count(), Is.EqualTo(5));
            foreach (string action in new[] { "Heal", "Death", "Hit", "Rest", "Respawn", "Special" })
                Assert.That(Field(Entry(action), "status").ToString(), Is.EqualTo("Substitute"), action);
            Assert.That(Field(Entry("Fall"),"status").ToString(),Is.EqualTo("Received"),"Use the received revision with hair.");
            Assert.That(Field(Entry("ParryCounter"), "source"), Is.EqualTo("CounterTransfer + CounterKick"));
            Assert.That(Field(Entry("ParryCounter"), "status").ToString(), Is.EqualTo("SubmittedProvisional"), "The received kick includes a provisional transfer; do not label the whole motion finished.");
        }

        [Test] public void SubmittedMobilityHasNineMappedJumpCelsAndThreeDashCels()
        {
            var jump=(Sprite[])Field(Set,"jumpFrames");Assert.That(jump.Length,Is.EqualTo(9));
            Assert.That(jump.All(s=>s!=null&&s.rect.size==new Vector2(64,64)&&s.pivot==new Vector2(32,38)&&s.pixelsPerUnit==32),Is.True);
            Assert.That(jump.Select(s=>s.name),Is.EqualTo(Enumerable.Range(1,9).Select(i=>$"Jump_{i:00}")));
            Assert.That(Keys("Jump").Take(7).Select(k=>k.value),Is.EqualTo(jump.Take(5).Concat(jump.Skip(3).Take(2))));
            foreach(string action in new[]{"Fall","DropThrough"})
                Assert.That(Keys(action).Take(3).Select(k=>k.value),Is.EqualTo(new[]{jump[7],jump[8],jump[7]}));
            Assert.That(Keys("Dash").Select(k=>k.value).Distinct().Count(),Is.EqualTo(3));
            Assert.That(Field(Entry("Dash"),"status").ToString(),Is.EqualTo("Received"));
            foreach(var sprite in jump.Concat(Keys("Dash").Select(k=>(Sprite)k.value)).Distinct())
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
    }
}
