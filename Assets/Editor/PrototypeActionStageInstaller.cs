using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Reuses the filming stage without rebuilding art or touching filming scenes.</summary>
public static class PrototypeActionStageInstaller
{
    public const float CameraSize = 5.625f;
    private const string BackgroundName = "VideoWhiteGrid";

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Action Test Stage")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Preserve the validation scene's unsaved changes first.");
            InstallIntoScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    internal static void InstallIntoScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var floor = roots.Single(x => x.name == "Ground");
        var camera = roots.Select(x => x.GetComponent<Camera>()).Single(x => x != null);
        var source = EditorSceneManager.OpenPreviewScene(VideoStudioSceneBuilder.ScenePath);
        try
        {
            var stage = source.GetRootGameObjects().Single(x => x.name == "01_Stage").transform;
            var sourceGrid = stage.Find("WhiteGrid");
            var sourceFloor = stage.Find("SolidFloor");
            if (sourceGrid == null || sourceFloor == null) throw new InvalidOperationException("Existing video grid/floor missing.");
            // Never regenerate the source stage or its textures. Copy its already-authored objects/settings.
            var previousGrid = roots.FirstOrDefault(x => x.name == BackgroundName);
            if (previousGrid == null)
            {
                var grid = Object.Instantiate(sourceGrid.gameObject);
                grid.name = BackgroundName;
                SceneManager.MoveGameObjectToScene(grid, scene);
            }
            EditorUtility.CopySerialized(sourceFloor.GetComponent<SpriteRenderer>(), floor.GetComponent<SpriteRenderer>());
            EditorUtility.CopySerialized(sourceFloor.GetComponent<BoxCollider2D>(), floor.GetComponent<BoxCollider2D>());
            floor.transform.SetPositionAndRotation(sourceFloor.position, sourceFloor.rotation);
            floor.transform.localScale = sourceFloor.lossyScale;
            floor.layer = sourceFloor.gameObject.layer;
            camera.backgroundColor = source.GetRootGameObjects().Select(x => x.GetComponent<Camera>()).Single(x => x != null).backgroundColor;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.orthographicSize = CameraSize;
        camera.transform.position = new Vector3(0, 3, -10);
        var floorRenderer = floor.GetComponent<SpriteRenderer>();
        foreach (var wall in roots.Where(x => x.name == "ArenaWall"))
        {
            wall.transform.position = new Vector3(Mathf.Sign(wall.transform.position.x) * 9.5f, 6, 0);
            ConfigureBox(wall, new Vector2(.5f, 12), floorRenderer, false);
        }

        var platforms = roots.FirstOrDefault(x => x.name == "ActionPlatforms");
        if (platforms == null)
        {
            platforms = new GameObject("ActionPlatforms");
            SceneManager.MoveGameObjectToScene(platforms, scene);
        }
        ConfigurePlatform(scene, platforms.transform, "SolidStep", new Vector2(-8, .75f), new Vector2(2, 1.5f), floorRenderer, false);
        // Leave full body clearance below the lower floor so drop-through collision can restore on landing.
        ConfigurePlatform(scene, platforms.transform, "OneWayFloor", new Vector2(-6, 2), new Vector2(3, .1875f), floorRenderer, true);
        ConfigurePlatform(scene, platforms.transform, "UpperOneWayFloor", new Vector2(-3.5f, 3.5f), new Vector2(3, .1875f), floorRenderer, true);
        foreach (var enemy in roots.SelectMany(x => x.GetComponentsInChildren<EnemyMeleeChaser2D>(true)))
            enemy.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigurePlatform(Scene scene, Transform parent, string name, Vector2 position,
        Vector2 size, SpriteRenderer source, bool oneWay)
    {
        var platform = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(x => x.name == name)?.gameObject;
        if (platform == null)
        {
            platform = new GameObject(name);
            SceneManager.MoveGameObjectToScene(platform, scene);
        }
        platform.transform.SetParent(parent, false);
        platform.transform.position = position;
        ConfigureBox(platform, size, source, oneWay);
    }

    private static void ConfigureBox(GameObject obj, Vector2 size, SpriteRenderer source, bool oneWay)
    {
        obj.layer = LayerMask.NameToLayer("Ground");
        var renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = source.color;
        renderer.sortingOrder = 0;
        var collider = obj.GetComponent<BoxCollider2D>();
        if (collider == null) collider = obj.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.usedByEffector = oneWay;
        if (oneWay)
        {
            var effector = obj.GetComponent<PlatformEffector2D>();
            if (effector == null) effector = obj.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
        }
    }
}
