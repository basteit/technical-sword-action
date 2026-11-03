using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Additive, idempotent upgrade of our validation scene; never overwrites user scenes or tuning.</summary>
public static class PrototypeCombatPolishInstaller
{
    private const string Folder = "Assets/Settings/PrototypeCombat/";
    [MenuItem("Tools/Technical Sword Action/Prototype/Install Strong Enemy and Feedback")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeCombatSceneBuilder.ScenePath) == null)
            PrototypeCombatSceneBuilder.Build();
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Validation scene has unsaved changes; review them first.");
        SceneManager.SetActiveScene(scene);
        try
        {
            InstallIntoScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    private static void InstallIntoScene(Scene scene)
    {
        PlayerStateMachine player = null; PrototypeCombatArena arena = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent<PlayerStateMachine>(out var p)) player = p;
            if (root.TryGetComponent<PrototypeCombatArena>(out var a)) arena = a;
        }
        if (player == null || arena == null) throw new InvalidOperationException("Prototype player/arena missing.");
        var camera = Array.Find(scene.GetRootGameObjects(), x => x.GetComponent<Camera>() != null).GetComponent<Camera>();
        int playerMask = 1 << player.gameObject.layer;
        int groundMask = 1 << LayerMask.NameToLayer("Ground");
        var slash = LoadOrCreate<EnemyAttackDefinition>("StrongSlash", out bool createSlash);
        if (createSlash)
        {
            slash.attackId = "heavy-overhead-slash"; slash.windupFrames = 42; slash.activeFrames = 4;
            slash.recoveryFrames = 38; slash.cooldownFrames = 40; slash.damage = 14;
            slash.attackRange = 1.65f; slash.hitOffset = new Vector2(.8f, 0); slash.hitSize = new Vector2(1.65f, 1.3f);
            slash.windupClip = Audio("HitSound_RayTap");
            Save(slash);
        }
        var sweep = LoadOrCreate<EnemyAttackDefinition>("StrongLowSweep", out bool createSweep);
        if (createSweep)
        {
            sweep.attackId = "heavy-low-unparryable-sweep"; sweep.windupFrames = 54; sweep.activeFrames = 5;
            sweep.recoveryFrames = 48; sweep.cooldownFrames = 42; sweep.damage = 20;
            sweep.parryable = false; sweep.resistHitStunWhileAttacking = true;
            sweep.attackRange = 1.65f; sweep.hitOffset = new Vector2(.9f, -.5f); sweep.hitSize = new Vector2(1.9f, .55f);
            sweep.countermeasure = "Jump over the low sweep, or Dash through it; punish the long recovery.";
            sweep.windupClip = Audio("HitSound_RayStay"); Save(sweep);
        }
        var strong = LoadOrCreate<EnemyCombatDefinition>("StrongMelee", out bool createStrong);
        if (createStrong)
        {
            strong.rank = EnemyCombatRank.Variant; strong.maxHp = 120; strong.moveSpeed = 1.25f;
            strong.lightHitStunFrames = 0; strong.strongHitStunFrames = 16; strong.hitKnockback = .2f;
            strong.targetLayers = playerMask; strong.obstacleLayers = groundMask;
            strong.attacks = new[] { slash, sweep }; Save(strong);
        }
        EnemyAttackCoordinator2D coordinator = null; EnemyMeleeChaser2D strongEnemy = null;
        var normal = new System.Collections.Generic.List<Damageable2D>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent<EnemyAttackCoordinator2D>(out var c)) coordinator = c;
            if (root.TryGetComponent<EnemyMeleeChaser2D>(out var enemy))
            {
                if (root.name == "StrongMelee") strongEnemy = enemy;
                else normal.Add(root.GetComponent<Damageable2D>());
            }
        }
        if (coordinator == null)
        {
            var root = new GameObject("PrototypeAttackRoles"); SceneManager.MoveGameObjectToScene(root, scene);
            coordinator = root.AddComponent<EnemyAttackCoordinator2D>();
            Set(coordinator, "maximumAttackers", 1); Set(coordinator, "minimumStartGapFrames", 18);
        }
        if (strongEnemy == null)
        {
            var root = new GameObject("StrongMelee"); SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = LayerMask.NameToLayer("Enemy"); root.transform.position = new Vector3(6, .8f);
            root.AddComponent<BoxCollider2D>().size = new Vector2(1, 1.6f);
            var hp = root.AddComponent<Damageable2D>(); Set(hp, "combatDefinition", strong); Set(hp, "maxHp", 120);
            Set(hp, "individualId", "prototype-strong-1"); Set(hp, "playerBodyLayers", playerMask);
            strongEnemy = root.AddComponent<EnemyMeleeChaser2D>(); Set(strongEnemy, "combatDefinition", strong); Set(strongEnemy, "target", player.transform);
            var torso = Box(root.transform, "HeavySilhouette", Vector3.zero, new Vector2(1.05f, 1.25f), new Color(.7f, .75f, .8f));
            Box(root.transform, "BroadShoulders", new Vector3(0, .45f), new Vector2(1.4f, .28f), new Color(.45f, .5f, .6f));
            var weapon = Box(root.transform, "HeavyWeapon", Vector3.right * .7f, new Vector2(1.35f, .22f), Color.white);
            var tell = Box(root.transform, "DangerDiamond", Vector3.up * 1.1f, Vector2.one * .3f, Color.yellow);
            var cue = root.AddComponent<EnemyCombatCue2D>(); Set(cue, "silhouette", torso.transform); Set(cue, "weapon", weapon.transform); Set(cue, "cue", tell.GetComponent<SpriteRenderer>());
            root.AddComponent<AudioSource>().playOnAwake = false;
            root.SetActive(false);
        }
        Set(strongEnemy, "attackCoordinator", coordinator);
        foreach (var enemy in normal) Set(enemy.GetComponent<EnemyMeleeChaser2D>(), "attackCoordinator", coordinator);
        var profile = LoadOrCreate<CombatFeedbackProfile>("CombatFeedback", out bool createProfile);
        if (createProfile)
        {
            profile.entries = new[] {
                Entry(CombatFeedbackKind.LightHit, 3, 3, .08f, 8, .18f, new Color(1,.9f,.55f), "HitSound_RayTap", 1.15f),
                Entry(CombatFeedbackKind.HeavyHit, 5, 4, .12f, 12, .34f, new Color(1,.75f,.3f), "HitSound_Release", .9f),
                Entry(CombatFeedbackKind.Counter, 5, 4, .12f, 14, .42f, new Color(.5f,1,1), "HitSound_RayRelease", .8f),
                Entry(CombatFeedbackKind.Parry, 3, 4, .12f, 10, .28f, Color.white, "HitSound_RayTap", 1.3f),
                Entry(CombatFeedbackKind.JustParry, 7, 6, .16f, 14, .4f, new Color(.5f,.8f,1), "HitSound_RayRelease", 1.3f),
                Entry(CombatFeedbackKind.PlayerHit, 3, 3, .08f, 12, .3f, new Color(1,.45f,.4f), "HitSound_RayStay", .85f),
                Entry(CombatFeedbackKind.Defeat, 0, 0, 0, 18, .45f, new Color(.7f,.75f,.8f), "", 1),
                Entry(CombatFeedbackKind.ParryMiss, 0, 0, 0, 8, .15f, new Color(.5f,.5f,.5f), "HitSound_RayStay", .65f)
            }; Save(profile);
        }
        var settings = LoadOrCreate<CombatFeedbackSettings>("FeedbackSettings", out bool createSettings);
        if (createSettings) Save(settings);
        var feedback = player.GetComponent<CombatFeedback2D>();
        if (feedback == null) feedback = player.gameObject.AddComponent<CombatFeedback2D>();
        Set(feedback, "profile", profile); Set(feedback, "settings", settings); Set(feedback, "feedbackCamera", camera);
        normal.Add(strongEnemy.GetComponent<Damageable2D>());
        var data = new SerializedObject(feedback); var enemies = data.FindProperty("enemies"); enemies.arraySize = normal.Count;
        for (int i = 0; i < normal.Count; i++) enemies.GetArrayElementAtIndex(i).objectReferenceValue = normal[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        Set(arena, "strongEnemy", strongEnemy); Set(arena, "coordinator", coordinator); Set(arena, "feedback", feedback);
    }
    private static CombatFeedbackProfile.Entry Entry(CombatFeedbackKind kind, int stop, float pixels, float seconds,
        int lifetime, float size, Color tint, string clip, float pitch) => new() {
            kind = kind, hitstopFrames = stop, shakePixels = pixels, shakeSeconds = seconds, lifetimeFrames = lifetime,
            glyphSize = size, tint = tint, clip = Audio(clip), volume = .5f, pitch = pitch, flashFrames = 3
        };
    private static AudioClip Audio(string name) => string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".mp3");
    private static T LoadOrCreate<T>(string name, out bool created) where T : ScriptableObject
    {
        string path = Folder + name + ".asset"; var data = AssetDatabase.LoadAssetAtPath<T>(path);
        created = data == null;
        if (created) { data = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(data, path); }
        return data;
    }
    private static void Save(Object data) { EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector2 size, Color color)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "PrototypeUnlit.mat");
        renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size; renderer.color = color; return root;
    }
    private static void Set(Object target, string name, object value)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(name);
        if (property == null) throw new InvalidOperationException(name + " not found");
        if (value is Object obj) property.objectReferenceValue = obj;
        else if (value is int integer) property.intValue = integer;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
