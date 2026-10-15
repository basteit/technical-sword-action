using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VideoStudioSceneBuilder
{
    public const string ScenePath="Assets/Scenes/CombatVideoStudio.unity";
    public const string SetFolder="Assets/Settings/VideoStudio";
    public const string PrefabFolder="Assets/Prefabs/VideoStudio";
    [MenuItem("Tools/Technical Sword Action/Video/Open Studio")]
    public static void Open()
    {
        if (Application.isPlaying) { VideoStudioWindow.ShowWindow(); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) Build();
        else EditorSceneManager.OpenScene(ScenePath);
        VideoStudioWindow.ShowWindow();
    }
    // Explicit generation only. Opening the studio never rebuilds an artist's edited stage.
    [MenuItem("Tools/Technical Sword Action/Video/Create Studio Scene (New Only)")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Studio Scene already exists. Open it or save a copy before regenerating.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve loaded Scene changes before creating the studio.");
        Directory.CreateDirectory(SetFolder); Directory.CreateDirectory(PrefabFolder);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var library=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        if (library==null) throw new InvalidOperationException("Build video art first.");
        int ground=LayerMask.NameToLayer("Ground"), enemyLayer=LayerMask.NameToLayer("Enemy"), playerLayer=LayerMask.NameToLayer("Player");
        var stage=new GameObject("01_Stage");
        var square=CreateSquare();
        var floor=Rect("SolidFloor",stage.transform,new Vector2(0,-10),new Vector2(44,20),new Color(.74f,.78f,.8f),square,-10);
        floor.gameObject.layer=ground; floor.gameObject.AddComponent<BoxCollider2D>().size=new Vector2(44,20);
        var grid=new GameObject("WhiteGrid"); grid.transform.SetParent(stage.transform);
        for (int x=-22;x<=22;x++) Rect("GridX",grid.transform,new Vector2(x,7),new Vector2(1/32f,14),new Color(.91f,.93f,.94f),square,-15);
        for (int y=0;y<=14;y++) Rect("GridY",grid.transform,new Vector2(0,y),new Vector2(44,1/32f),new Color(.91f,.93f,.94f),square,-15);
        // Solid floor covers all grid at and below the contact plane.
        var left=Rect("LeftBoundary",stage.transform,new Vector2(-22,5),new Vector2(.5f,10),new Color(.74f,.78f,.8f),square,-10);
        left.gameObject.layer=ground; left.gameObject.AddComponent<BoxCollider2D>().size=new Vector2(.5f,10);
        var right=UnityEngine.Object.Instantiate(left,stage.transform); right.name="RightBoundary"; right.transform.position=new Vector3(22,5);

        var player=CombatVideoSceneBuilder.CreatePlayer(library,ground,enemyLayer,playerLayer,false);
        player.name="02_Player";
        player.AddComponent<PlayerHeal2D>();
        var camera=new GameObject("03_StudioCamera",typeof(Camera)).GetComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=2.109375f; camera.transform.position=new Vector3(0,1.05f,-10);
        camera.backgroundColor=new Color(.98f,.985f,.99f); camera.clearFlags=CameraClearFlags.SolidColor;
        var effects=new GameObject("04_Effects");
        var vfx=effects.AddComponent<VideoVfxRenderer>(); Set(vfx,"library",library);
        Set(vfx,"glowMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/VideoPixelGlow.mat"));
        var presentation=effects.AddComponent<VideoCombatPresentation>();
        Set(presentation,"captureCamera",camera);Set(presentation,"counter",player.GetComponent<PlayerParryCounter2D>());
        Set(presentation,"vfx",vfx);Set(presentation,"library",library);Set(presentation,"playerVisual",player.GetComponent<SpriteRenderer>());
        CombatVideoSceneBuilder.Set(presentation,"adaptiveFocusHeight",true);
        var enemies=new GameObject("05_SetEnemies").transform;
        var props=new GameObject("06_SetGimmicks").transform;
        var custom=new GameObject("07_CustomContent").transform;
        var studio=new GameObject("08_StudioControls").AddComponent<CombatVideoStudio>();
        Set(studio,"player",player.GetComponent<PlayerStateMachine>());Set(studio,"presentation",presentation);Set(studio,"vfx",vfx);
        Set(studio,"studioCamera",camera);Set(studio,"enemiesRoot",enemies);Set(studio,"gimmicksRoot",props);Set(studio,"customContent",custom);

        var dummy=CombatVideoSceneBuilder.Enemy("StudioMeleeDummy",Vector3.up,enemyLayer,playerLayer,null,library);
        CombatVideoSceneBuilder.Set(dummy,"individualId",""); dummy.gameObject.AddComponent<StudioEnemyActor>();
        var enemyPrefab=PrefabUtility.SaveAsPrefabAsset(dummy.gameObject,PrefabFolder+"/StudioMeleeDummy.prefab");
        UnityEngine.Object.DestroyImmediate(dummy.gameObject);Set(studio,"enemyPrefab",enemyPrefab);
        var block=Rect("StudioGimmick",null,Vector2.zero,new Vector2(2,1),Color.gray,square,0);
        block.gameObject.layer=ground;block.gameObject.AddComponent<BoxCollider2D>().usedByEffector=true;block.gameObject.AddComponent<PlatformEffector2D>().useOneWay=true;
        var gimmick=block.gameObject.AddComponent<StudioStageGimmick>(); CombatVideoSceneBuilder.Set(gimmick,"obstacleLayers",1<<ground);
        var propPrefab=PrefabUtility.SaveAsPrefabAsset(block.gameObject,PrefabFolder+"/StudioGimmick.prefab");
        UnityEngine.Object.DestroyImmediate(block.gameObject);Set(studio,"gimmickPrefab",propPrefab);
        var sets=CreatePresets();
        var serialized=new SerializedObject(studio);var array=serialized.FindProperty("presets");array.arraySize=sets.Length;
        for (int i=0;i<sets.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=sets[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        player.SetActive(true);studio.ApplyPreset(sets[1]);
        EditorSceneManager.SaveScene(scene,ScenePath);
    }
    private static Sprite CreateSquare()
    {
        string path=PrefabFolder+"/StudioSolid.png";
        if (!File.Exists(path))
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static StudioPreset[] CreatePresets()
    {
        var action=Preset("01_Actions","アクション", "敵のいない平面。歩行・ジャンプ・ダッシュ・復帰を試す。");
        var combo=Preset("02_Combo","コンボ", "HP20の待機ダミー。4段まで繋ぐ／途中で止める／キャンセルを試す。",
            new[]{new StudioEnemySetup{position=new Vector2(1.1f,1),hp=20}});
        var parry=Preset("03_Parry","パリィ", "背後の敵が予備動作0.65秒＋回復後1.2秒間隔で攻撃。単発にも変更できる。",
            new[]{new StudioEnemySetup{position=new Vector2(-1.3f,1),hp=3,mode=StudioEnemyMode.RepeatAttack}});
        var movement=Preset("04_Platforms","足場", "ブロック・すり抜け床・移動床。S＋Spaceで床抜け。",
            gimmicks:new[]{
                new StudioGimmickSetup{position=new Vector2(2.5f,.5f),size=new Vector2(1.5f,1)},
                new StudioGimmickSetup{kind=StudioGimmickKind.OneWayPlatform,position=new Vector2(-2,1.2f),size=new Vector2(3,.25f)},
                new StudioGimmickSetup{kind=StudioGimmickKind.MovingPlatform,position=new Vector2(4,1.6f),size=new Vector2(2,.25f),travel=new Vector2(2,1),periodSeconds=4}});
        var battle=Preset("05_Melee","複数敵", "左右から既存の近接AIで追跡。敵ごとにHP・攻撃間隔を変更可能。",
            new[]{new StudioEnemySetup{position=new Vector2(3,1),hp=8,mode=StudioEnemyMode.Chase},new StudioEnemySetup{position=new Vector2(-3,1),hp=3,mode=StudioEnemyMode.Chase}});
        return new[]{action,combo,parry,movement,battle};
    }
    private static StudioPreset Preset(string filename,string title,string description,StudioEnemySetup[] enemies=null,StudioGimmickSetup[] gimmicks=null)
    {
        string path=SetFolder+"/"+filename+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<StudioPreset>(path);if(existing!=null)return existing;
        var asset=ScriptableObject.CreateInstance<StudioPreset>();asset.displayName=title;asset.description=description;
        asset.enemies=enemies??Array.Empty<StudioEnemySetup>();asset.gimmicks=gimmicks??Array.Empty<StudioGimmickSetup>();
        AssetDatabase.CreateAsset(asset,path);return asset;
    }
    private static SpriteRenderer Rect(string name,Transform parent,Vector2 position,Vector2 size,Color color,Sprite sprite,int order)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent);obj.transform.position=position;
        var renderer=obj.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.drawMode=SpriteDrawMode.Sliced;renderer.size=size;renderer.color=color;renderer.sortingOrder=order;
        return renderer;
    }
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)=>CombatVideoSceneBuilder.Set(target,field,value);
}
