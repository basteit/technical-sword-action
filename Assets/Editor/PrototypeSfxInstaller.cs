using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit audio-only upgrade. Existing scene and gameplay tuning are not regenerated.</summary>
public static class PrototypeSfxInstaller
{
    public const string ProfilePath = "Assets/Settings/PrototypeCombat/FreeSfx.asset";
    public const string AudioFolder = "Assets/Audio/PrototypeFreeSfx/";
    [MenuItem("Tools/Technical Sword Action/Prototype/Install Free SFX")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != PrototypeCombatSceneBuilder.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved, clean PrototypeCombatValidation scene first.");
        InstallIntoScene(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    public static AudioClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");
        if (clip == null) throw new InvalidOperationException("Missing licensed prototype sound: " + name);
        return clip;
    }
    public static AudioClip FeedbackClip(CombatFeedbackKind kind) => Clip(kind switch {
        CombatFeedbackKind.LightHit => "LightHit", CombatFeedbackKind.HeavyHit => "HeavyHit",
        CombatFeedbackKind.Counter => "CounterImpact", CombatFeedbackKind.Parry => "ParryClang",
        CombatFeedbackKind.JustParry => "ParryClang", CombatFeedbackKind.PlayerHit => "PlayerHit",
        CombatFeedbackKind.Defeat => "Fall", _ => "ParryMiss"
    });
    private static PrototypeSfxProfile.Entry Entry(PrototypeSfxCue cue, string name, float volume,
        int priority = 30, float pitch = 1, float variation = .025f) => new() {
            cue = cue, clips = new[] { Clip(name) }, volume = volume, priority = priority, pitch = pitch, pitchVariation = variation
        };
    public static void InstallIntoScene(Scene scene)
    {
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerStateMachine>(true)).Single();
        var enemies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyMeleeChaser2D>(true)).ToArray();
        var profile = AssetDatabase.LoadAssetAtPath<PrototypeSfxProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PrototypeSfxProfile>();
            profile.entries = new[] {
                Entry(PrototypeSfxCue.Footstep,"FootstepA",.13f,5),
                Entry(PrototypeSfxCue.Jump,"Jump",.25f), Entry(PrototypeSfxCue.Land,"Land",.22f,20),
                Entry(PrototypeSfxCue.Dash,"Dash",.34f,45),
                Entry(PrototypeSfxCue.Swing1,"SwordA",.28f,35), Entry(PrototypeSfxCue.Swing2,"SwordB",.30f,35,1.04f),
                Entry(PrototypeSfxCue.Swing3,"SwordA",.31f,35,1.1f), Entry(PrototypeSfxCue.Swing4,"SwordIai",.34f,45,1),
                Entry(PrototypeSfxCue.Sheathe,"Sheathe",.14f,8), Entry(PrototypeSfxCue.ParryReady,"ParryReady",.22f,35),
                Entry(PrototypeSfxCue.Parry,"ParryClang",.6f,90,1,0), Entry(PrototypeSfxCue.JustParry,"ParryClang",.65f,95,1.07f,0),
                Entry(PrototypeSfxCue.ParryMiss,"ParryMiss",.12f,5),
                Entry(PrototypeSfxCue.LightHit,"LightHit",.5f,65), Entry(PrototypeSfxCue.HeavyHit,"HeavyHit",.58f,75,1),
                Entry(PrototypeSfxCue.CounterTransfer,"CounterTransfer",.4f,50,1,0),
                Entry(PrototypeSfxCue.CounterCharge,"CounterCharge",.42f,40,1,0),
                Entry(PrototypeSfxCue.CounterRelease,"Dash",.25f,70,1.1f,0),
                Entry(PrototypeSfxCue.CounterHit,"CounterImpact",.85f,95,1,0),
                Entry(PrototypeSfxCue.PlayerHit,"PlayerHit",.55f,80), Entry(PrototypeSfxCue.PlayerDeath,"Fall",.26f,90,.9f,0),
                Entry(PrototypeSfxCue.EnemyWindup,"EnemyWindup",.16f,10), Entry(PrototypeSfxCue.EnemyDanger,"Danger",.24f,45,1,0),
                Entry(PrototypeSfxCue.EnemyAttack,"EnemyAttack",.22f,25), Entry(PrototypeSfxCue.EnemyDefeat,"Fall",.18f,10),
                Entry(PrototypeSfxCue.HealStart,"Jump",.18f,20), Entry(PrototypeSfxCue.HealApply,"Heal",.3f,40),
                Entry(PrototypeSfxCue.SpecialCast,"CounterTransfer",.38f,50,.85f), Entry(PrototypeSfxCue.SpecialHit,"HeavyHit",.55f,75,.9f),
                Entry(PrototypeSfxCue.Interact,"Interact",.16f,20)
            };
            profile.Find(PrototypeSfxCue.Footstep).clips = new[] { Clip("FootstepA"), Clip("FootstepB") };
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        if (!profile.IsValid) throw new InvalidOperationException("Review the existing SFX palette before installing.");
        var sfx = player.GetComponent<PrototypeSfx2D>() ?? player.gameObject.AddComponent<PrototypeSfx2D>();
        var serialized = new SerializedObject(sfx); serialized.FindProperty("profile").objectReferenceValue = profile;
        var list = serialized.FindProperty("enemies"); list.arraySize = enemies.Length;
        for (int i = 0; i < enemies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var feedback = AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Settings/PrototypeCombat/CombatFeedback.asset");
        if (feedback == null) throw new InvalidOperationException("Install the common combat feedback first.");
        foreach (var entry in feedback.entries)
        {
            entry.clip = FeedbackClip(entry.kind);
            var cue = entry.kind switch {
                CombatFeedbackKind.LightHit => PrototypeSfxCue.LightHit, CombatFeedbackKind.HeavyHit => PrototypeSfxCue.HeavyHit,
                CombatFeedbackKind.Counter => PrototypeSfxCue.CounterHit, CombatFeedbackKind.Parry => PrototypeSfxCue.Parry,
                CombatFeedbackKind.JustParry => PrototypeSfxCue.JustParry, CombatFeedbackKind.PlayerHit => PrototypeSfxCue.PlayerHit,
                CombatFeedbackKind.Defeat => PrototypeSfxCue.EnemyDefeat, _ => PrototypeSfxCue.ParryMiss
            };
            entry.volume = profile.Find(cue).volume; entry.pitch = profile.Find(cue).pitch;
        }
        EditorUtility.SetDirty(feedback); AssetDatabase.SaveAssetIfDirty(feedback);
        foreach (var name in new[] { "StrongSlash", "StrongLowSweep" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyAttackDefinition>("Assets/Settings/PrototypeCombat/" + name + ".asset");
            if (definition == null) continue;
            definition.windupClip = Clip(name == "StrongSlash" ? "EnemyWindup" : "Danger");
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder.TrimEnd('/') }))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate; settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssetIfDirty(profile);
    }
}
