using UnityEditor;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CombatVideoSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/CombatVideo.unity";
    public const string DefenseCancelScenePath = "Assets/Scenes/CombatVideoDefenseCancel.unity";
    // TrainingGrid has a 16px pitch at 32 pixels per unit.
    internal const float BackgroundGridUnit=.5f;
    internal static readonly Vector3 SequentialFrontStart=new Vector3(1.35f+BackgroundGridUnit,1,0);
    internal static readonly Vector3 SequentialRearStart=new Vector3(-1.1f-BackgroundGridUnit,1,0);
    [MenuItem("Tools/Technical Sword Action/Video/Apply Rear Entrance Layout")]
    public static void ApplyRearEntranceLayout()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Exit PlayMode before updating capture layout.");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty)throw new System.InvalidOperationException("Preserve loaded scene edits before updating capture layout.");
        string previous=SceneManager.GetActiveScene().path;
        var scene=EditorSceneManager.OpenScene(ScenePath);
        ConfigureSequentialLayout(Object.FindFirstObjectByType<CombatVideoDirector>());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(previous!=ScenePath&&!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
    }
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
    { BuildCaptureScene(ScenePath,false); }
    [MenuItem("Tools/Technical Sword Action/Video/Build Defense Cancel Showcase Scene")]
    public static void BuildDefenseCancel()
    { BuildCaptureScene(DefenseCancelScenePath,true); }
    private static void BuildCaptureScene(string scenePath,bool defenseCancel)
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
        camera.orthographic = true; camera.orthographicSize = VideoCombatPresentation.DefaultNormalSize;
        camera.backgroundColor = new Color(.98f,.985f,.99f);
        var actor = CreatePlayer(library, ground, enemy, playerLayer, defenseCancel);
        var counter = actor.GetComponent<PlayerParryCounter2D>();
        var render = actor.GetComponent<SpriteRenderer>();
        var a = Enemy("Enemy_A", defenseCancel?new Vector3(1.1f,1,0):SequentialFrontStart, enemy, playerLayer, actor.transform,library);
        var bPosition=defenseCancel?new Vector3(-1.45f,1,0):SequentialRearStart;
        var b = Enemy("Enemy_B", bPosition, enemy, playerLayer, actor.transform,library);
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
        Set(director,"defenseCancelTake",defenseCancel);
        if(defenseCancel) Set(director,"rearStart",bPosition);
        else ConfigureSequentialLayout(director);
        actor.SetActive(true);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
    }
    internal static GameObject CreatePlayer(VideoArtLibrary library, int ground, int enemy, int playerLayer, bool defenseCancel)
    {
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
        if(defenseCancel) Set(motor,"dashSpeed",6f);
        Set(motor, "groundCheck", feet.transform); Set(motor, "groundLayer", 1 << ground);
        Set(motor,"applyRecommendedPhysicsSettings",false);body.interpolation=RigidbodyInterpolation2D.None;
        var attack = actor.AddComponent<PlayerAttack2D>();
        Set(attack, "hitPoint", hit.transform); Set(attack, "targetLayers", 1 << enemy);
        Set(attack, "showHitboxAlways", false);
        Set(attack,"directStatePrefix","PlayerVideo_Attack_");Set(attack,"lightHitstopFrames",2);
        ConfigureAttack(attack,library,!defenseCancel);
        actor.AddComponent<PlayerParry2D>(); actor.AddComponent<PlayerSpecialGauge>();
        var health=actor.AddComponent<PlayerDamageReceiver2D>();
        Set(health,"normalParryHitStop",VideoCombatPresentation.DefaultParryHitstopFrames/60f);
        Set(health,"justParryHitStop",VideoCombatPresentation.DefaultJustParryHitstopFrames/60f);
        var counter=actor.AddComponent<PlayerParryCounter2D>();
        ConfigureCounter(counter,library);Set(counter,"obstacleLayers",1 << ground);
        actor.AddComponent<PlayerStateMachine>();actor.AddComponent<PlayerVisualRecovery2D>();
        var animation=actor.AddComponent<VideoPlayerAnimationDriver>();Set(animation,"library",library);Set(animation,"skipAttackRecovery",true);
        return actor;
    }
    internal static void ConfigureCounter(PlayerParryCounter2D counter,VideoArtLibrary library)
    {
        var submitted=library.Find("CounterKick");var kick=submitted??library.Find("AirRoundhouse");
        int transfer=submitted!=null?(library.Find("CounterTransfer")?.TotalFrames??0):0;
        // Filming-only hold: one extra slow tick on the final tucked cel, then the
        // original one-tick release. Shared source/prototype art timing stays intact.
        int hold=submitted!=null?1:0;
        Set(counter,"airborneKick",true);Set(counter,"invincibleDuringKick",true);
        Set(counter,"hitFrame",transfer+kick.hitFrame+hold);Set(counter,"chargeFrame",transfer+kick.chargeFrame);
        Set(counter,"endFrame",transfer+(kick.recoveryFrame>0?kick.recoveryFrame:kick.TotalFrames)+hold);
        Set(counter,"chargeReleaseFrame",transfer+(kick.releaseFrame>0?kick.releaseFrame:24)+hold);
        Set(counter,"landingFrame",transfer+(kick.landingFrame>0?kick.landingFrame:36)+hold);
        Set(counter,"radius",.5f);Set(counter,"maxApproach",1.2f);Set(counter,"landingRollDistance",0f);
        Set(counter,"blinkApproach",true);Set(counter,"blinkTravelFrames",2);
        Set(counter,"blinkStartFrame",Mathf.Max(0,transfer-2));Set(counter,"behindTarget",submitted!=null);
        Set(counter,"overheadHeelDrop",submitted==null);
        Set(counter,"overheadHeight",submitted!=null?4/32f:1.35f);
        Set(counter,"heelImpactHeight",submitted!=null?2/32f:1.15f);
        Set(counter,"airborneContactOffset",kick.counterContactOffset!=Vector2.zero?kick.counterContactOffset:new Vector2(41/32f,4/32f));
        Set(counter,"heelContactOffset",new Vector2(10/32f,-22/32f));
        Set(counter,"heelTargetHeadOffsetX",7/32f);
    }
    internal static Damageable2D Enemy(string name, Vector3 position, int layer, int playerLayer, Transform target,VideoArtLibrary library)
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
    internal static void ConfigureAttack(PlayerAttack2D attack,VideoArtLibrary library,bool stationarySubmitted=true)
    {
        var data=new SerializedObject(attack);var steps=data.FindProperty("attackSteps");
        var motions=new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash"}.Select(library.Find).ToArray();
        int[] totals=motions.Select(m=>m.TotalFrames).ToArray(),hits=motions.Select(m=>m.hitFrame).ToArray();float[] distance={.12f,.2f,.28f,.4f};
        for(int i=0;i<4;i++)
        {
            // Supplied sword trails define reach; these two poses stay on the spot.
            bool trail=motions[i].hasTrailHitbox;
            float travel=trail&&stationarySubmitted?0:distance[i];
            var step=steps.GetArrayElementAtIndex(i);step.FindPropertyRelative("fallbackDuration").floatValue=totals[i]/60f;
            step.FindPropertyRelative("groundMotionDistance").floatValue=travel;step.FindPropertyRelative("groundMotionDuration").floatValue=hits[i]/60f;
            step.FindPropertyRelative("radius").floatValue=.6f;step.FindPropertyRelative("useGroundMotionCurve").boolValue=true;
            step.FindPropertyRelative("groundMotionCurve").animationCurveValue=AnimationCurve.Linear(0,0,hits[i]/60f,travel);
            step.FindPropertyRelative("useBoxHitbox").boolValue=trail;
            if(trail)
            {
                Vector2 min=motions[i].hitboxMin;min.x=0; // Include the sword between the grip and the outer trail.
                Vector2 max=motions[i].hitboxMax;
                step.FindPropertyRelative("hitboxOffset").vector2Value=(min+max)/2;
                step.FindPropertyRelative("hitboxSize").vector2Value=max-min;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    internal static void ConfigureSequentialLayout(CombatVideoDirector director)
    {
        Set(director,"frontStart",SequentialFrontStart);Set(director,"rearStart",SequentialRearStart);
        Set(director,"useSpacingApproaches",false);
        Set(director,"rearEntranceEnabled",true);Set(director,"twoStageRightShift",1f);
        Set(director,"rearEntranceDistance",1.6f);Set(director,"rearEntranceSpeed",3.6f);Set(director,"rearEntranceReach",1.6f);
        director.Player.transform.position=director.InitialPlayerPosition;
        director.FrontEnemy.transform.position=director.InitialFrontPosition;
        director.RearEnemy.transform.position=director.InitialRearPosition;
        var rearAi=director.RearEnemy.GetComponent<EnemyMeleeChaser2D>();
        // The selected two-stage take temporarily overrides pursuit when the first slash lands.
        // Keep the saved baseline for the retained four-stage comparison and every reset.
        Set(rearAi,"moveSpeed",4f);Set(rearAi,"attackRange",1.9f);Set(rearAi,"preferredMaxDistance",1.35f);
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
    internal static void Set(Object target,string name,Vector3 value)
    { var s=new SerializedObject(target);s.FindProperty(name).vector3Value=value;s.ApplyModifiedPropertiesWithoutUndo(); }
    internal static void Set(Object target,string name,Vector2 value)
    { var s=new SerializedObject(target);s.FindProperty(name).vector2Value=value;s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Visual(GameObject obj, Vector2 size, Color color)
    {
        var r = obj.GetComponent<SpriteRenderer>();
        if (r == null) r = obj.AddComponent<SpriteRenderer>();
        r.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        r.drawMode=SpriteDrawMode.Sliced; r.size=size; r.color=color;
    }
}
