using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeEnemyArtInstaller
{
    public const string ArtPath = "Assets/Settings/PrototypeCombat/NormalMeleeArt.asset";
    private static readonly string[] Motions = { "Enemy_Idle", "Enemy_Move", "Enemy_Windup", "Enemy_Attack", "Enemy_Hit", "Enemy_Parried", "Enemy_Death" };

    [MenuItem("Tools/Technical Sword Action/Prototype/Apply Common Enemy Artwork")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit PlayMode first.");
        if (scene.path != PrototypeCombatSceneBuilder.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved PrototypeCombatValidation scene first.");
        InstallIntoScene(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void InstallIntoScene(Scene scene)
    {
        var source = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        if (source == null || Motions.Any(name => source.Find(name) == null))
            throw new InvalidOperationException("Submitted common enemy motions are missing.");
        var library = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(ArtPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<VideoArtLibrary>();
            AssetDatabase.CreateAsset(library, ArtPath);
            // Independent timing/reference arrays. Reuse received PNG sprites without reimporting them.
            library.motions = Motions.Select(name =>
            {
                var original = source.Find(name);
                return new VideoArtLibrary.Motion { name = name, provisional = original.provisional,
                    frames = (Sprite[])original.frames.Clone(), durations = (int[])original.durations.Clone() };
            }).ToArray();
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
        }
        var normals = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyMeleeChaser2D>(true))
            .Where(e => e.Definition != null && e.Definition.name == "NormalMelee").ToArray();
        if (normals.Length != 2) throw new InvalidOperationException("Expected the two normal-enemy preset instances.");
        foreach (var enemy in normals)
        {
            var launch = enemy.GetComponent<EnemyCounterLaunch2D>();
            if (launch == null) throw new InvalidOperationException("Install gameplay counter launch visuals first.");
            var launchData = new SerializedObject(launch);
            var parent = (Transform)launchData.FindProperty("visualRoot").objectReferenceValue;
            var art = enemy.GetComponent<PrototypeEnemyArtPresentation>();
            var drawing = art != null ? art.Artwork : null;
            if (drawing == null)
            {
                var go = new GameObject("CommonEnemyArtwork");
                go.transform.SetParent(parent, false); go.transform.SetAsFirstSibling();
                drawing = go.AddComponent<SpriteRenderer>();
                drawing.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PrototypeCombat/PrototypeUnlit.mat");
                drawing.sortingOrder = 8;
                drawing.sprite = library.Find("Enemy_Idle").At(0);
                art = enemy.gameObject.AddComponent<PrototypeEnemyArtPresentation>();
            }
            // Keep the existing cue shapes and counter launch parent; hide only old body/weapon art.
            var cue = new SerializedObject(enemy.GetComponent<EnemyCombatCue2D>());
            foreach (string field in new[] { "silhouette", "weapon" })
            {
                var old = cue.FindProperty(field).objectReferenceValue as Transform;
                if (old != null) old.gameObject.SetActive(false);
            }
            drawing.transform.SetAsFirstSibling();
            // Received idle/chance feet are 32px below their pivot at 32 PPU.
            var body = enemy.GetComponent<BoxCollider2D>();
            drawing.transform.localPosition = new Vector3(0, body.offset.y - body.size.y * .5f + 1f, 0);
            Set(art, "library", library); Set(art, "artwork", drawing);
            Set(enemy.GetComponent<Damageable2D>(), "spriteRenderer", drawing);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
