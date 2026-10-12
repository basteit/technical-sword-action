using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatVideoSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CombatVideo.unity";
    [MenuItem("Tools/Technical Sword Action/Video/Build Art and Capture Scene")]
    public static void BuildAll()
    {
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Preserve loaded scene changes before building video content.");
        CombatVideoArtBuilder.Build();
        // Reimporting linked animation assets can dirty their generated scene.
        // Persist that generated scene before replacing its capture layout.
        var current=SceneManager.GetActiveScene();
        if(current.path==ScenePath) EditorSceneManager.SaveScene(current);
        Build();
    }
    [MenuItem("Tools/Technical Sword Action/Video/Build Capture Scene")]
    public static void Build()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new System.InvalidOperationException("Preserve the loaded scene changes before building CombatVideo.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var library=AssetDatabase.LoadAssetAtPath<VideoArtLibrary>(CombatVideoArtBuilder.LibraryPath);
        int ground = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
        int enemy = Mathf.Max(0, LayerMask.NameToLayer("Enemy"));
        int playerLayer = Mathf.Max(0, LayerMask.NameToLayer("Player"));
        var floor = new GameObject("CaptureFloor");
        floor.layer = ground;
        floor.transform.position = new Vector3(0, -.25f, 0);
        var floorBody = floor.AddComponent<BoxCollider2D>();
        floorBody.size = new Vector2(24, .5f);
        if(library!=null)
        {
            var backdrop=new GameObject("TrainingGrid").AddComponent<SpriteRenderer>();
            backdrop.sprite=library.background;backdrop.sortingOrder=-20;backdrop.transform.position=new Vector3(0,1.5f,0);
        }
        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 1.05f, -10);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2.109375f;
        camera.backgroundColor = new Color(.98f,.985f,.99f);
        var actor = new GameObject("Player");
        actor.SetActive(false); actor.tag = "Player"; actor.layer = playerLayer;
        actor.transform.position = new Vector3(0, 1, 0);
        var body = actor.AddComponent<Rigidbody2D>();
        body.constraints = RigidbodyConstraints2D.FreezeRotation; body.gravityScale = 3;
        var collider = actor.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(.45f, 2);
        var feet = new GameObject("GroundCheck");
        feet.transform.SetParent(actor.transform, false); feet.transform.localPosition = new Vector3(0,-.97f,0);
        var hit = new GameObject("HitPoint");
        hit.transform.SetParent(actor.transform, false); hit.transform.localPosition = new Vector3(.65f,-.1f,0);
        var render = actor.AddComponent<SpriteRenderer>(); render.sortingOrder = 10;
        var animator = actor.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatVideoArtBuilder.ControllerPath);
        if(library!=null) render.sprite=library.Find("IdleReference").frames[0];
        var motor = actor.AddComponent<PlayerMotor2D>();
        Set(motor, "groundCheck", feet.transform); Set(motor, "groundLayer", 1 << ground);
        Set(motor,"applyRecommendedPhysicsSettings",false);body.interpolation=RigidbodyInterpolation2D.None;
        var attack = actor.AddComponent<PlayerAttack2D>();
        Set(attack, "hitPoint", hit.transform); Set(attack, "targetLayers", 1 << enemy);
        Set(attack, "showHitboxAlways", false);
        Set(attack,"directStatePrefix","PlayerVideo_Attack_");Set(attack,"lightHitstopFrames",2);
        ConfigureAttack(attack,library);
        actor.AddComponent<PlayerParry2D>(); actor.AddComponent<PlayerSpecialGauge>();
        var health=actor.AddComponent<PlayerDamageReceiver2D>();Set(health,"normalParryHitStop",2/60f);Set(health,"justParryHitStop",3/60f);
        var counter=actor.AddComponent<PlayerParryCounter2D>();var kick=library.Find("AirRoundhouse");Set(counter,"airborneKick",true);Set(counter,"hitFrame",kick.hitFrame);Set(counter,"chargeFrame",kick.chargeFrame);Set(counter,"endFrame",kick.recoveryFrame>0?kick.recoveryFrame:kick.TotalFrames);Set(counter,"radius",.5f);Set(counter,"obstacleLayers",1 << ground);
        Set(counter,"chargeReleaseFrame",kick.releaseFrame>0?kick.releaseFrame:24);Set(counter,"landingFrame",kick.landingFrame>0?kick.landingFrame:36);Set(counter,"maxApproach",1.2f);
        Set(counter,"landingRollDistance",0f);
        Set(counter,"blinkApproach",true);Set(counter,"blinkTravelFrames",2);
        actor.AddComponent<PlayerStateMachine>();actor.AddComponent<PlayerVisualRecovery2D>();Set(actor.AddComponent<VideoPlayerAnimationDriver>(),"library",library);
        var a = Enemy("Enemy_A", new Vector3(1.1f,1,0), enemy, playerLayer, actor.transform,library);
        var b = Enemy("Enemy_B", new Vector3(-.85f,1,0), enemy, playerLayer, actor.transform,library);
        var fx=new GameObject("VideoEffects").AddComponent<VideoVfxRenderer>();Set(fx,"library",library);
        string materialPath="Assets/Settings/VideoPixelGlow.mat";var glow=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(glow==null) { glow=new Material(Shader.Find("TechnicalSwordAction/VideoPixelGlow"));AssetDatabase.CreateAsset(glow,materialPath); }
        Set(fx,"glowMaterial",glow);
        var presentation=new GameObject("VideoPresentation").AddComponent<VideoCombatPresentation>();
        Set(presentation,"captureCamera",camera);Set(presentation,"counter",counter);Set(presentation,"vfx",fx);
        Set(presentation,"playerVisual",render);Set(presentation,"library",library);
        var director = new GameObject("CaptureDirector").AddComponent<CombatVideoDirector>();
        Set(director,"player", actor.GetComponent<PlayerStateMachine>());
        Set(director,"frontEnemy",a); Set(director,"rearEnemy",b);
        Set(director,"presentation",presentation);Set(director,"vfx",fx);
        actor.SetActive(true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }
    private static Damageable2D Enemy(string name, Vector3 position, int layer, int playerLayer, Transform target,VideoArtLibrary library)
    {
        var obj = new GameObject(name); obj.SetActive(false); obj.layer = layer; obj.transform.position = position;
        var collider = obj.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.5f,1.6f); collider.isTrigger = true;
        var graphics=new GameObject("TrainingDummyVisual");graphics.transform.SetParent(obj.transform,false);
        var renderer=graphics.AddComponent<SpriteRenderer>();renderer.sortingOrder=8;
        graphics.AddComponent<Animator>().runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatVideoArtBuilder.EnemyControllerPath);
        if(library!=null) renderer.sprite=library.Find("Enemy_Idle").frames[0];
        var damageable = obj.AddComponent<Damageable2D>();
        Set(damageable,"individualId",name); Set(damageable,"deathDisplayDuration",.65f);
        Set(damageable,"knockbackPower",0f); Set(damageable,"lightHitStunDuration",.1f);
        var ai = obj.AddComponent<EnemyMeleeChaser2D>(); Set(ai,"target",target);
        Set(ai,"attackTargetLayers",1 << playerLayer); Set(ai,"attackWindupDuration",.5f);
        Set(ai,"attackRange",1.9f); Set(ai,"moveSpeed",0f);
        var animation=obj.AddComponent<VideoEnemyAnimationDriver>();Set(animation,"library",library);Set(animation,"player",target);
        Set(animation,"hitbackObstacles",1<<LayerMask.NameToLayer("Ground"));
        obj.SetActive(true); return damageable;
    }
    private static void ConfigureAttack(PlayerAttack2D attack,VideoArtLibrary library)
    {
        var data=new SerializedObject(attack);var steps=data.FindProperty("attackSteps");
        var motions=new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash"}.Select(library.Find).ToArray();
        int[] totals=motions.Select(m=>m.TotalFrames).ToArray(),hits=motions.Select(m=>m.hitFrame).ToArray();float[] distance={.12f,.2f,.28f,.4f};
        for(int i=0;i<4;i++)
        {
            var step=steps.GetArrayElementAtIndex(i);step.FindPropertyRelative("fallbackDuration").floatValue=totals[i]/60f;
            step.FindPropertyRelative("groundMotionDistance").floatValue=distance[i];step.FindPropertyRelative("groundMotionDuration").floatValue=hits[i]/60f;
            step.FindPropertyRelative("radius").floatValue=.6f;step.FindPropertyRelative("useGroundMotionCurve").boolValue=true;
            step.FindPropertyRelative("groundMotionCurve").animationCurveValue=AnimationCurve.Linear(0,0,hits[i]/60f,distance[i]);
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    internal static void Set(Object target, string name, Object value)
    { var s = new SerializedObject(target); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    internal static void Set(Object target, string name, float value)
    { var s = new SerializedObject(target); s.FindProperty(name).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    internal static void Set(Object target, string name, int value)
    { var s = new SerializedObject(target); s.FindProperty(name).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    internal static void Set(Object target, string name, bool value)
    { var s = new SerializedObject(target); s.FindProperty(name).boolValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    internal static void Set(Object target, string name, string value)
    { var s = new SerializedObject(target); s.FindProperty(name).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Visual(GameObject obj, Vector2 size, Color color)
    {
        var r = obj.GetComponent<SpriteRenderer>();
        if (r == null) r = obj.AddComponent<SpriteRenderer>();
        r.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        r.drawMode=SpriteDrawMode.Sliced; r.size=size; r.color=color;
    }
}
