using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates an isolated basic-action test scene from the prototype layout.</summary>
public static class BasicActionValidationSceneBuilder
{
    [MenuItem("Tools/Technical Sword Action/Build Basic Action Validation Scene")]
    public static void Build()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new System.InvalidOperationException("Save the current scene before building the validation scene.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var player = Object.FindFirstObjectByType<PlayerStateMachine>();
        if (player == null) throw new System.InvalidOperationException("SampleScene has no player.");
        Vector3 origin = player.transform.position;
        Physics2D.SyncTransforms();
        var motorData = new SerializedObject(player.GetComponent<PlayerMotor2D>());
        int groundMask = motorData.FindProperty("groundLayer").intValue;
        var ground = Physics2D.Raycast(origin, Vector2.down, 30f, groundMask);
        float floorY = ground.collider != null ? ground.collider.bounds.max.y : origin.y - 1;
        var target = new GameObject("BasicActionInteractCounter");
        target.transform.position = new Vector3(origin.x - 2, floorY + 0.5f, origin.z);
        var trigger = target.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(2, 2);
        target.AddComponent<PrototypeInteractable2D>();
        AddVisual(target, new Vector2(0.5f, 1f), Color.cyan);
        var platform = new GameObject("BasicActionOneWayFloor");
        int groundLayer = LayerMask.NameToLayer("Ground");
        platform.layer = groundLayer >= 0 ? groundLayer : 0;
        platform.transform.position = new Vector3(origin.x - 4, floorY + 1.25f, origin.z);
        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(5, 0.2f);
        collider.usedByEffector = true;
        platform.AddComponent<PlatformEffector2D>().useOneWay = true;
        AddVisual(platform, collider.size, Color.green);
        if (player.GetComponent<PlayerHeal2D>() == null) player.gameObject.AddComponent<PlayerHeal2D>();
        if (player.GetComponent<PlayerRespawn2D>() == null) player.gameObject.AddComponent<PlayerRespawn2D>();
        if (player.GetComponent<PlayerParryCounter2D>() == null) player.gameObject.AddComponent<PlayerParryCounter2D>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BasicActionValidation.unity");
    }

    private static void AddVisual(GameObject target, Vector2 size, Color color)
    {
        var renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
    }
}
