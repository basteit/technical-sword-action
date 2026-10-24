using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Upgrade only the saved gameplay prototype. Never regenerate or edit filming assets.</summary>
public static class PrototypeCounterCinematicInstaller
{
    public const string ProfilePath = "Assets/Settings/PrototypeCombat/CounterCinematic.asset";
    public const string ArtPath = "Assets/Settings/PrototypeCombat/CounterCinematicArt.asset";
    private const string ClipPath = "Assets/Animations/Player/Submitted/PlayerPrototype_ParryCounter.anim";
    private static readonly EditorCurveBinding SpriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Counter Cinematic")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before installing the counter cinematic.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Prototype scene has unsaved changes; review/save them first.");
            var player = scene.GetRootGameObjects().Select(root => root.GetComponent<PlayerStateMachine>()).Single(p => p != null);
            var drawing = player.GetComponent<PrototypePlayerArtPresentation>();
            if (drawing == null || drawing.ArtSet == null || drawing.Artwork == null)
                throw new InvalidOperationException("Install submitted player art before installing the counter cinematic.");
            var profile = BuildProfile(player, drawing);
            if (!profile.IsValid) throw new InvalidOperationException("Invalid counter cinematic profile or source timing; review the video snapshot.");
            var counter = player.GetComponent<PlayerParryCounter2D>();
            SetReference(counter, "cinematicProfile", profile);
            ApplyCounterArtwork(drawing.ArtSet, profile);
            var effects = player.GetComponent<VideoVfxRenderer>();
            if (effects == null) effects = player.gameObject.AddComponent<VideoVfxRenderer>();
            SetReference(effects, "library", profile.art); SetReference(effects, "glowMaterial", profile.glowMaterial);
            var presentation = player.GetComponent<PrototypeCounterPresentation>();
            if (presentation == null) presentation = player.gameObject.AddComponent<PrototypeCounterPresentation>();
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            presentation.Configure(counter, drawing, player.GetComponent<CombatFeedback2D>(), effects, camera);
            EditorUtility.SetDirty(presentation);
            foreach (var enemy in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyMeleeChaser2D>(true)))
            {
                var group = enemy.transform.Find("CounterLaunchVisual");
                if (group == null)
                {
                    group = new GameObject("CounterLaunchVisual").transform; group.SetParent(enemy.transform, false);
                    foreach (var child in enemy.transform.Cast<Transform>().ToArray())
                        if (child != group && child.GetComponentInChildren<SpriteRenderer>(true) != null && child.GetComponentInChildren<Collider2D>(true) == null)
                            child.SetParent(group, false);
                }
                var launch = enemy.GetComponent<EnemyCounterLaunch2D>();
                if (launch == null) launch = enemy.gameObject.AddComponent<EnemyCounterLaunch2D>();
                launch.Configure(group, 1 << LayerMask.NameToLayer("Ground")); EditorUtility.SetDirty(launch);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var errors = Validate(); if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("[PrototypeCounter] Connected rear transfer, dim/charge and launch; filming assets and legacy counter data unchanged.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static CounterCinematicProfile BuildProfile(PlayerStateMachine player, PrototypePlayerArtPresentation drawing)
    {
        var source = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>("Assets/Settings/CombatVideoArt.asset");
        if (source == null) throw new InvalidOperationException("Missing existing video artwork library.");
        var art = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(ArtPath);
        if (art == null) { art = ScriptableObject.CreateInstance<VideoArtLibrary>(); AssetDatabase.CreateAsset(art, ArtPath); }
        art.playerArtworkIsRough = source.playerArtworkIsRough; art.fourStageArtworkIsRough = source.fourStageArtworkIsRough;
        art.motions = new[] { "CounterTransfer", "CounterKick", "Fx_ElectricKick", "CounterKickEffect" }.Where(name=>source.Find(name)!=null).Select(name =>
        {
            var motion = source.Find(name); if (motion == null) throw new InvalidOperationException("Missing counter source: " + name);
            return new VideoArtLibrary.Motion
            {
                name = motion.name, provisional = motion.provisional, includesCounterTrail = motion.includesCounterTrail,
                counterContactOffset = motion.counterContactOffset,
                frames = (Sprite[])motion.frames.Clone(), eyeFrames = motion.eyeFrames != null ? (Sprite[])motion.eyeFrames.Clone() : null,
                durations = (int[])motion.durations.Clone(), hitFrame = motion.hitFrame, chargeFrame = motion.chargeFrame,
                releaseFrame = motion.releaseFrame, landingFrame = motion.landingFrame, recoveryFrame = motion.recoveryFrame
            };
        }).ToArray();
        EditorUtility.SetDirty(art); AssetDatabase.SaveAssetIfDirty(art);
        var profile = AssetDatabase.LoadAssetAtPath<CounterCinematicProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CounterCinematicProfile>();
            var collider = player.GetComponent<BoxCollider2D>();
            float visualOffset = collider.offset.y - collider.size.y * .5f + drawing.ArtSet.commonFeetBelowPivot;
            profile.contactOffset = source.Find("CounterKick").counterContactOffset + Vector2.up * visualOffset;
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.art = art;
        profile.glowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/VideoPixelGlow.mat");
        profile.subjectLiftShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/VideoSubjectLift.shader");
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        return profile;
    }

    public static void ApplyCounterArtwork(PrototypePlayerArtSet set, CounterCinematicProfile profile)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null || set.Find("ParryCounter") == null) throw new InvalidOperationException("Missing installed prototype counter clip.");
        var keys = new List<ObjectReferenceKeyframe>(); int frame = 0;
        foreach (var motion in new[] { profile.Transfer, profile.Kick })
            for (int i = 0; i < motion.frames.Length; i++)
            {
                keys.Add(new ObjectReferenceKeyframe { time = frame / 60f, value = motion.frames[i] }); frame += motion.durations[i];
            }
        keys.Add(new ObjectReferenceKeyframe { time = (profile.endFrame - 1) / 60f, value = profile.Kick.frames[^1] });
        clip.frameRate = 60; AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding,
            keys.GroupBy(key => key.time).Select(group => group.Last()).OrderBy(key => key.time).ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.startTime = 0; settings.stopTime = profile.endFrame / 60f; settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var entry = set.Find("ParryCounter"); entry.source = "CounterTransfer + CounterKick";
        entry.status = profile.Transfer.provisional || profile.Kick.provisional ? PrototypePlayerArtSet.ArtworkStatus.SubmittedProvisional : PrototypePlayerArtSet.ArtworkStatus.Received;
        entry.contactSprite = profile.ContactSprite; entry.visualHitFrame = profile.hitFrame;
        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
    }
    public static string[] Validate()
    {
        var errors = new List<string>();
        var profile = AssetDatabase.LoadAssetAtPath<CounterCinematicProfile>(ProfilePath);
        if (profile == null || !profile.IsValid) return new[] { "Missing/invalid prototype counter cinematic profile." };
        int expectedMotions=profile.art.Find("CounterKickEffect")!=null?4:3;
        if (AssetDatabase.GetAssetPath(profile.art) != ArtPath || profile.art.motions.Length != expectedMotions)
            errors.Add("Prototype counter must use its own minimal art snapshot, not the full mutable filming library.");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var set = AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(PrototypePlayerArtInstaller.SetPath);
        if (clip == null || Mathf.Abs(clip.length * 60 - profile.endFrame) > .001f ||
            set == null || set.Find("ParryCounter") == null || set.Find("ParryCounter").contactSprite != profile.ContactSprite ||
            PrototypePlayerArtInstaller.Sample(clip, profile.hitFrame / 60f) != profile.ContactSprite)
            errors.Add("Counter contact sprite/duration must match the cinematic clock.");
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded; var previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            var player = scene.GetRootGameObjects().Select(root => root.GetComponent<PlayerStateMachine>()).Single(p => p != null);
            if (player.GetComponent<PlayerParryCounter2D>().CinematicProfile != profile || player.GetComponent<PrototypeCounterPresentation>() == null || player.GetComponent<VideoVfxRenderer>() == null)
                errors.Add("Prototype cinematic action, display and VFX are not connected.");
            var enemies = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyMeleeChaser2D>(true)).ToArray();
            if (enemies.Length != 3 || enemies.Any(enemy => enemy.GetComponent<EnemyCounterLaunch2D>() == null || enemy.transform.Find("CounterLaunchVisual") == null))
                errors.Add("Keep the three optional enemy presets and connect each launch visual owner.");
            if (player.GetComponent<VideoCombatPresentation>() != null || player.GetComponent<VideoPlayerAnimationDriver>() != null || player.GetComponent<CombatVideoDirector>() != null)
                errors.Add("Filming camera/director/animation drivers must not own gameplay.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        return errors.ToArray();
    }
    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
}
