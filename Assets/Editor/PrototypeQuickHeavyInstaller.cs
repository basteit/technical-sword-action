using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs the received quick/heavy roughs without regenerating existing video/player art.</summary>
public static class PrototypeQuickHeavyInstaller
{
    public const string ArtFolder = "Assets/Art/Player/QuickHeavySubmitted";
    public const string QuickPath = "Assets/Settings/PrototypeCombat/QuickCounter.asset";
    public const string HeavyPath = "Assets/Settings/PrototypeCombat/HeavyParry.asset";
    public const string HeavyEnemyPath = "Assets/Settings/PrototypeCombat/HeavyParryTraining.asset";
    public const string HeavyAttackPath = "Assets/Settings/PrototypeCombat/HeavyParryTrainingAttack.asset";
    private static readonly EditorCurveBinding SpriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Quick Counter and Heavy Parry Roughs")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before installing received roughs.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("The prototype has unsaved changes; do not overwrite them.");
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerStateMachine>(true)).Single();
            var counter = player.GetComponent<PlayerParryCounter2D>();
            var legacy = counter.CinematicProfile;
            if (legacy == null || !legacy.IsValid) throw new InvalidOperationException("The existing cinematic comparison is required.");
            var quick = LoadOrCreate<QuickCounterProfile>(QuickPath);
            quick.transfer = new VideoArtLibrary.Motion {
                name = "CounterTransfer", provisional = legacy.Transfer.provisional,
                frames = (Sprite[])legacy.Transfer.frames.Clone(), durations = (int[])legacy.Transfer.durations.Clone() };
            quick.strike = new VideoArtLibrary.Motion {
                name = "CounterQuick", provisional = true, frames = LoadSprites("CounterQuick", 6),
                durations = new[] { 2, 2, 2, 2, 1, 7 }, hitFrame = 8 };
            quick.clip = BuildClip("PlayerPrototype_CounterQuick", quick.EndFrame, quick.Sample);
            var heavy = LoadOrCreate<HeavyParryProfile>(HeavyPath);
            heavy.reaction = new VideoArtLibrary.Motion {
                name = "ParrySuccessHeavy", provisional = true, frames = LoadSprites("ParryHeavy", 8),
                durations = new[] { 2, 2, 1, 2, 3, 3, 3, 3 }, hitFrame = 4 };
            heavy.obstacleLayers = counter.Definition.obstacleLayers;
            heavy.clip = BuildClip("PlayerPrototype_ParrySuccessHeavy", heavy.reaction.TotalFrames, frame => heavy.reaction.At(frame));
            if (!quick.IsValid || !heavy.IsValid) throw new InvalidOperationException("The received rough profiles are invalid.");
            Save(quick); Save(heavy);

            var originalEnemy = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>("Assets/Settings/PrototypeCombat/NormalMelee_ReferenceTrial.asset");
            if (originalEnemy == null)
            {
                var trial = scene.GetRootGameObjects().Select(root => root.GetComponent<PrototypeCombatTrial>()).Single(component => component != null);
                originalEnemy = (EnemyCombatDefinition)new SerializedObject(trial).FindProperty("referenceEnemy").objectReferenceValue;
            }
            var attack = LoadOrCreate<EnemyAttackDefinition>(HeavyAttackPath);
            attack.attackId = "training-heavy-parry";
            attack.windupFrames = originalEnemy.windupFrames;
            attack.parryCueLeadFrames = originalEnemy.parryCueLeadFrames;
            attack.activeFrames = originalEnemy.activeFrames;
            attack.recoveryFrames = originalEnemy.recoveryFrames;
            attack.cooldownFrames = originalEnemy.cooldownFrames;
            attack.damage = originalEnemy.damage; attack.knockback = originalEnemy.knockback;
            attack.parryable = true; attack.parryReaction = ParryReactionWeight.Heavy;
            attack.attackRange = originalEnemy.attackRange; attack.hitOffset = originalEnemy.hitOffset; attack.hitSize = originalEnemy.hitSize;
            attack.resistHitStunWhileAttacking = true;
            attack.countermeasure = "Heavy parry, then the same quick counter; Dash also avoids this attack.";
            Save(attack);
            var enemy = LoadOrCreate<EnemyCombatDefinition>(HeavyEnemyPath);
            EditorUtility.CopySerialized(originalEnemy, enemy);
            enemy.name = "HeavyParryTraining";
            enemy.attacks = new[] { attack };
            enemy.parryReaction = ParryReactionWeight.Heavy;
            enemy.countermeasure = attack.countermeasure;
            Save(enemy);
            SetReference(counter, "quickProfile", quick);
            var counterData = new SerializedObject(counter); counterData.FindProperty("quickCounterEnabled").boolValue = true; counterData.ApplyModifiedPropertiesWithoutUndo();
            SetReference(player.GetComponent<PrototypeCounterPresentation>(), "heavyParryProfile", heavy);
            var arena = scene.GetRootGameObjects().Select(root => root.GetComponent<PrototypeCombatArena>()).Single(component => component != null);
            SetReference(arena, "heavyTrainingDefinition", enemy);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var errors = Validate(); if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("[QuickHeavy] Received roughs installed. Quick is default; preset 5 uses a normal enemy and a heavy-only infinite-HP enemy. Originals/video clips preserved.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    public static string[] Validate()
    {
        var errors = new List<string>();
        var quick = AssetDatabase.LoadAssetAtPath<QuickCounterProfile>(QuickPath);
        var heavy = AssetDatabase.LoadAssetAtPath<HeavyParryProfile>(HeavyPath);
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>(HeavyEnemyPath);
        if (quick == null || !quick.IsValid || quick.clip == null ||
            Mathf.Abs(quick.clip.length * 60 - quick.EndFrame) > .001f ||
            PrototypePlayerArtInstaller.Sample(quick.clip, quick.hitFrame / 60f) != quick.ContactSprite)
            errors.Add("Quick counter contact clip must agree with the single hit tick.");
        if (heavy == null || !heavy.IsValid || heavy.clip == null ||
            PrototypePlayerArtInstaller.Sample(heavy.clip, heavy.reaction.hitFrame / 60f) != heavy.ContactSprite)
            errors.Add("Heavy parry requires the eight received cels and matching contact clip.");
        if (enemy == null || !enemy.IsValid || enemy.attacks.Length != 1 ||
            enemy.attacks[0].parryReaction != ParryReactionWeight.Heavy || !enemy.attacks[0].parryable)
            errors.Add("The training enemy must use one dedicated, parryable heavy attack.");
        return errors.ToArray();
    }
    private static Sprite[] LoadSprites(string prefix, int count)
    {
        return Enumerable.Range(1, count).Select(index =>
        {
            string path = $"{ArtFolder}/{prefix}_{index:00}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing rough export: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(32f / 128, 38f / 128);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite import failed: " + path);
        }).ToArray();
    }
    private static AnimationClip BuildClip(string name, int frames, Func<int, Sprite> sample)
    {
        string path = PrototypePlayerArtInstaller.Folder + "/" + name + ".anim";
        var clip = LoadOrCreate<AnimationClip>(path); clip.name = name; clip.frameRate = 60;
        var keys = new List<ObjectReferenceKeyframe>(); Sprite previous = null;
        for (int frame = 0; frame < frames; frame++)
        {
            Sprite sprite = sample(frame);
            if (sprite != previous || frame == frames - 1) keys.Add(new ObjectReferenceKeyframe { time = frame / 60f, value = sprite });
            previous = sprite;
        }
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.startTime = 0; settings.stopTime = frames / 60f; settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings); Save(clip); return clip;
    }
    private static T LoadOrCreate<T>(string path) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
        value = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
        AssetDatabase.CreateAsset(value, path); return value;
    }
    private static void Save(UnityEngine.Object value) { EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value); }
    private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
    }
}
