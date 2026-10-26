using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Create independent trial data; never rewrite original artwork, clips or balance assets.</summary>
public static class PrototypeCombatTrialInstaller
{
    public const string FastPath = "Assets/Settings/PrototypeCombat/FourStepAttack_FastTrial.asset";
    public const string SnappyPath = "Assets/Settings/PrototypeCombat/FourStepAttack_SnappyTrial.asset";
    public const string EnemyPath = "Assets/Settings/PrototypeCombat/NormalMelee_ReferenceTrial.asset";
    public const string LaunchPath = "Assets/Settings/PrototypeCombat/CounterLaunch_ReferenceTrial.asset";
    public const string ControllerPath = "Assets/Animations/Player/Submitted/PlayerSubmitted_FastTrial.overrideController";
    public const string SnappyControllerPath = "Assets/Animations/Player/Submitted/PlayerSubmitted_SnappyTrial.overrideController";
    private const string AttackPath = "Assets/Settings/PrototypeCombat/FourStepAttack.asset";
    private const string OriginalEnemyPath = "Assets/Settings/PrototypeCombat/NormalMelee.asset";

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Reference Combat Trial")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before installing trial data.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Review/save existing prototype Scene edits before installing.");
            var attack = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>(AttackPath);
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>(OriginalEnemyPath);
            var fast = CopyOnce<PlayerAttackDefinition>(AttackPath, FastPath, out bool newFast);
            if (newFast)
            {
                int[] hit = { 12, 9, 12, 30 }, recovery = { 28, 22, 28, 47 }, end = { 30, 24, 30, 49 };
                for (int i = 0; i < 4; i++)
                {
                    var step = fast.steps[i]; step.activeStart = hit[i]; step.activeEnd = hit[i] + 1;
                    step.cancelStart = hit[i] + 2; step.recoveryStart = recovery[i]; step.endFrame = end[i];
                }
                Save(fast);
            }
            var snappy = CopyOnce<PlayerAttackDefinition>(FastPath, SnappyPath, out bool newSnappy);
            if (newSnappy)
            {
                int[] hit = { 9, 7 }, recovery = { 22, 18 }, end = { 24, 20 };
                float[] distance = { .25f, .30f };
                for (int i = 0; i < 2; i++)
                {
                    var step = snappy.steps[i]; step.activeStart = hit[i]; step.activeEnd = hit[i] + 1;
                    step.cancelStart = hit[i] + 2; step.recoveryStart = recovery[i]; step.endFrame = end[i];
                    step.motionDistance = distance[i]; step.motionFrames = 6;
                }
                Save(snappy);
            }
            var reference = CopyOnce<EnemyCombatDefinition>(OriginalEnemyPath, EnemyPath, out bool newEnemy);
            if (newEnemy) { reference.windupFrames = 96; reference.recoveryFrames = 24; Save(reference); }
            var launch = CopyOnce<CounterCinematicProfile>(PrototypeCounterCinematicInstaller.ProfilePath, LaunchPath, out bool newLaunch);
            if (newLaunch)
            {
                launch.launchDistance = 1.6f; launch.launchHeight = .27f; launch.launchFrames = 18;
                launch.launchRotationDegrees = 0; Save(launch);
            }
            if (!fast.IsValid || !snappy.IsValid || !reference.IsValid || !launch.IsValid) throw new InvalidOperationException("Invalid comparison data.");
            var originalController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(PrototypePlayerArtInstaller.ControllerPath);
            var fastController = BuildRetimedController(originalController, attack, fast, ControllerPath, "FastTrial", 4);
            var snappyController = BuildRetimedController(fastController, fast, snappy, SnappyControllerPath, "SnappyTrial", 2);
            var roots = scene.GetRootGameObjects();
            var arena = roots.SelectMany(root => root.GetComponentsInChildren<PrototypeCombatArena>(true)).Single();
            var player = roots.SelectMany(root => root.GetComponentsInChildren<PlayerAttack2D>(true)).Single();
            var normals = roots.SelectMany(root => root.GetComponentsInChildren<EnemyMeleeChaser2D>(true))
                .Where(value => value.Definition == enemy).OrderBy(value => value.name).ToArray();
            if (normals.Length != 2) throw new InvalidOperationException("Expected exactly two original normal enemies.");
            var trial = arena.GetComponent<PrototypeCombatTrial>();
            if (trial == null) trial = arena.gameObject.AddComponent<PrototypeCombatTrial>();
            var so = new SerializedObject(trial);
            void Reference(string key, UnityEngine.Object value) => so.FindProperty(key).objectReferenceValue = value;
            Reference("attack", player); Reference("originalAttack", attack); Reference("fastAttack", fast);
            Reference("snappyAttack", snappy);
            Reference("originalController", originalController); Reference("fastController", fastController);
            Reference("snappyController", snappyController);
            Reference("originalEnemy", enemy); Reference("referenceEnemy", reference); Reference("referenceLaunch", launch);
            var array = so.FindProperty("normalEnemies"); array.arraySize = normals.Length;
            for (int i = 0; i < normals.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = normals[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!trial.IsConfigured) throw new InvalidOperationException("Missing trial references.");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[PrototypeTrial] F10 SNAPPY -> previous FAST -> ORIGINAL / F11 enemy timing / F12 reaction; baselines preserved.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    private static T CopyOnce<T>(string source, string destination, out bool created) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(destination); created = value == null;
        if (!created) return value;
        if (!AssetDatabase.CopyAsset(source, destination)) throw new InvalidOperationException("Could not snapshot " + source);
        return AssetDatabase.LoadAssetAtPath<T>(destination);
    }
    public static string[] Validate()
    {
        var errors = new List<string>();
        var fast = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>(FastPath);
        var snappy = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>(SnappyPath);
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>(EnemyPath);
        var launch = AssetDatabase.LoadAssetAtPath<CounterCinematicProfile>(LaunchPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
        var snappyController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(SnappyControllerPath);
        var original = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(PrototypePlayerArtInstaller.ControllerPath);
        var art = AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(PrototypePlayerArtInstaller.SetPath);
        if (fast == null || !fast.IsValid) errors.Add("Missing/invalid fast trial attack definition.");
        if (snappy == null || !snappy.IsValid) errors.Add("Missing/invalid snappy trial attack definition.");
        if (enemy == null || !enemy.IsValid) errors.Add("Missing/invalid reference trial enemy definition.");
        if (launch == null || !launch.IsValid) errors.Add("Missing/invalid reference trial launch profile.");
        if (controller == null || original == null || controller.runtimeAnimatorController != original.runtimeAnimatorController)
            errors.Add("Fast trial must preserve the original Animator state graph.");
        if (snappyController == null || original == null || snappyController.runtimeAnimatorController != original.runtimeAnimatorController)
            errors.Add("Snappy trial must preserve the original Animator state graph.");
        ValidateAttackTiming(fast, controller, art, "Fast", errors);
        ValidateAttackTiming(snappy, snappyController, art, "Snappy", errors);
        return errors.ToArray();
    }
    private static void ValidateAttackTiming(PlayerAttackDefinition definition, AnimatorOverrideController controller,
        PrototypePlayerArtSet art, string label, List<string> errors)
    {
        if (definition == null || !definition.IsValid || controller == null || art == null) return;
        for (int i = 0; i < definition.steps.Length; i++)
        {
            var step = definition.steps[i]; var clip = controller[step.clip]; var entry = art.Find("Attack_" + (i + 1));
            if (clip == null || entry == null || entry.contactSprite == null)
            { errors.Add("Missing " + label + " trial attack/contact clip: " + (i + 1)); continue; }
            float Frame(float time) => time / step.clip.length * step.endFrame;
            var marker = AnimationUtility.GetAnimationEvents(clip).FirstOrDefault(item => item.functionName == "OnAttackHit");
            if (marker == null || Mathf.Abs(Frame(marker.time) - step.activeStart) > 2.001f)
                errors.Add(label + " trial hit marker differs by more than 2F: " + (i + 1));
            var keys = AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(binding => binding.propertyName == "m_Sprite")
                .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding)).ToArray();
            if (keys.Length == 0 || keys.Any(key => key.value == null)) errors.Add("Missing " + label + " trial sprite keys: " + (i + 1));
            var contact = keys.FirstOrDefault(key => key.value == entry.contactSprite);
            if (contact.value == null || Mathf.Abs(Frame(contact.time) - step.activeStart) > 2.001f)
                errors.Add(label + " trial contact sprite differs by more than 2F: " + (i + 1));
        }
    }
    private static AnimatorOverrideController BuildRetimedController(AnimatorOverrideController original, PlayerAttackDefinition baseline,
        PlayerAttackDefinition target, string controllerPath, string suffix, int stepCount)
    {
        var result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (result == null) { result = new AnimatorOverrideController(); AssetDatabase.CreateAsset(result, controllerPath); }
        result.runtimeAnimatorController = original.runtimeAnimatorController;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(); original.GetOverrides(overrides);
        for (int i = 0; i < stepCount; i++)
        {
            var oldStep = baseline.steps[i]; var newStep = target.steps[i];
            int index = overrides.FindIndex(pair => pair.Key == oldStep.clip);
            if (index < 0) throw new InvalidOperationException("Missing baseline attack override.");
            var source = overrides[index].Value;
            string path = "Assets/Animations/Player/Submitted/PlayerPrototype_Attack_" + (i + 1) + "_" + suffix + ".anim";
            var clip = CopyOnce<AnimationClip>(AssetDatabase.GetAssetPath(source), path, out _);
            if (AnimationUtility.GetCurveBindings(source).Length != 0) throw new InvalidOperationException("Unexpected numeric attack curves; retime them explicitly.");
            float Map(float seconds)
            {
                float frame = seconds / oldStep.clip.length * oldStep.endFrame;
                float mapped = frame <= oldStep.activeStart ? frame / oldStep.activeStart * newStep.activeStart :
                    frame <= oldStep.recoveryStart ? Mathf.Lerp(newStep.activeStart, newStep.recoveryStart,
                        (frame - oldStep.activeStart) / (oldStep.recoveryStart - oldStep.activeStart)) :
                    Mathf.Lerp(newStep.recoveryStart, newStep.endFrame, (frame - oldStep.recoveryStart) / (oldStep.endFrame - oldStep.recoveryStart));
                return mapped / newStep.endFrame * oldStep.clip.length;
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
                for (int key = 0; key < keys.Length; key++) keys[key].time = Map(keys[key].time);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            var events = AnimationUtility.GetAnimationEvents(source);
            foreach (var item in events) item.time = Map(item.time);
            AnimationUtility.SetAnimationEvents(clip, events);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = oldStep.clip.length; settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); Save(clip);
            overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(oldStep.clip, clip);
        }
        result.ApplyOverrides(overrides); Save(result); return result;
    }
    private static void Save(UnityEngine.Object value) { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
}
