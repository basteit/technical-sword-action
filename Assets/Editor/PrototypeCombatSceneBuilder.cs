using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PrototypeCombatSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/PrototypeCombatValidation.unity";
    private const string SettingsPath = "Assets/Settings/PrototypeCombat";

    [MenuItem("Tools/Technical Sword Action/Prototype/Build Combat Validation Scene")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Validation scene already exists; open it instead of overwriting it.");
        if (!AssetDatabase.IsValidFolder(SettingsPath)) AssetDatabase.CreateFolder("Assets/Settings", "PrototypeCombat");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            int ground = LayerMask.NameToLayer("Ground"); if (ground < 0) ground = 0;
            int playerLayer = LayerMask.NameToLayer("Player"); if (playerLayer < 0) playerLayer = 0;
            int enemyLayer = LayerMask.NameToLayer("Enemy"); if (enemyLayer < 0) enemyLayer = 0;
            int groundMask = 1 << ground, playerMask = 1 << playerLayer, enemyMask = 1 << enemyLayer;
            var cameraObject = new GameObject("PrototypeCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 7.5f;
            camera.transform.position = new Vector3(0, 2, -10); camera.backgroundColor = new Color(.16f, .18f, .2f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var floor = Box("Ground", new Vector3(0, -.3f), new Vector2(30, .6f), new Color(.3f, .35f, .4f));
            floor.layer = ground; floor.AddComponent<BoxCollider2D>().size = new Vector2(30, .6f);
            foreach (float x in new[] { -12f, 12f })
            {
                var wall = Box("ArenaWall", new Vector3(x, 2), new Vector2(.5f, 4), Color.gray);
                wall.layer = ground; wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 4);
            }
            var platform = Box("OneWayFloor", new Vector3(-5, 1.5f), new Vector2(4, .15f), Color.gray);
            platform.layer = ground; var platformCollider = platform.AddComponent<BoxCollider2D>();
            platformCollider.size = new Vector2(4, .15f); platformCollider.usedByEffector = true;
            platform.AddComponent<PlatformEffector2D>().useOneWay = true;

            var player = new GameObject("PrototypePlayer"); player.tag = "Player"; player.layer = playerLayer;
            player.SetActive(false); player.transform.position = new Vector3(-3, .85f);
            var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.AddComponent<BoxCollider2D>().size = new Vector2(.65f, 1.6f);
            var renderer = player.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = GetUnlitMaterial();
            var animator = player.AddComponent<Animator>();
            string controllerPath = "Assets/Animations/Player/Prototype/PlayerPrototypeAnimator.controller";
            string[] controllers = AssetDatabase.FindAssets("PlayerPrototypeAnimator t:AnimatorController");
            if (controllers.Length > 0) controllerPath = AssetDatabase.GUIDToAssetPath(controllers[0]);
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (animator.runtimeAnimatorController == null) throw new InvalidOperationException("Prototype Animator missing.");
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "PlayerPrototype_Idle")
                {
                    var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), propertyName = "m_Sprite", path = "" };
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keys != null && keys.Length > 0) renderer.sprite = keys[0].value as Sprite;
                }
            var motor = player.AddComponent<PlayerMotor2D>(); var attack = player.AddComponent<PlayerAttack2D>();
            var gauge = player.AddComponent<PlayerSpecialGauge>(); var special = player.AddComponent<PlayerSpecialSkill2D>();
            var health = player.AddComponent<PlayerDamageReceiver2D>(); var parry = player.AddComponent<PlayerParry2D>();
            player.AddComponent<PlayerInteractor2D>(); var heal = player.AddComponent<PlayerHeal2D>();
            player.AddComponent<PlayerRespawn2D>(); var counter = player.AddComponent<PlayerParryCounter2D>();
            var state = player.AddComponent<PlayerStateMachine>(); player.AddComponent<PlayerAnimationDriver2D>();
            player.AddComponent<PlayerCombatTelemetry>();
            Set(player.AddComponent<PlayerDebugOverlay>(), "visible", false);
            var feet = new GameObject("GroundCheck"); feet.transform.SetParent(player.transform, false); feet.transform.localPosition = Vector3.down * .8f;
            var hit = new GameObject("HitPoint"); hit.transform.SetParent(player.transform, false); hit.transform.localPosition = Vector3.right * .9f;
            Set(motor, "groundCheck", feet.transform); Set(motor, "groundLayer", groundMask);
            Set(motor, "dashDuration", 20f / 60f); Set(motor, "dashCooldown", 34f / 60f); Set(motor, "ignoreCollisionLayersWhileDashing", enemyMask);
            Set(motor, "dashMoveFrames", 12); Set(motor, "preserveAirDashVertical", true);
            Set(health, "maxHp", 100); Set(health, "hitStopMoveDuration", .3f); Set(health, "ignoreCollisionLayersWhileInvincible", enemyMask);
            Set(health, "normalParryHitStop", 3f / 60f); Set(health, "justParryHitStop", 5f / 60f);
            Set(heal, "healAmount", 40); Set(heal, "applyFrame", 36); Set(heal, "endFrame", 54);
            Set(gauge, "startGauge", 100f); Set(gauge, "gainOnDamaged", 8f); Set(gauge, "gainOnParryNormal", 10f); Set(gauge, "gainOnParryJust", 15f);
            Set(special, "requiredGauge", 50f); Set(special, "skillDamage", 60); Set(special, "startupLock", 15f / 60f); Set(special, "recoveryLock", 34f / 60f);
            Set(special, "skillPoint", hit.transform); Set(special, "targetLayers", enemyMask);
            Set(attack, "hitPoint", hit.transform); Set(attack, "targetLayers", enemyMask);
            Set(attack, "animator", animator); Set(attack, "lightHitstopFrames", 3);

            var timing = LoadOrCreate<ParryTimingDefinition>("ParryTiming"); Set(parry, "timing", timing);
            var counterData = LoadOrCreate<ParryCounterDefinition>("ParryCounter"); counterData.obstacleLayers = groundMask;
            EditorUtility.SetDirty(counterData); Set(counter, "definition", counterData);
            var attackData = LoadOrCreate<PlayerAttackDefinition>("FourStepAttack");
            for (int i = 0; i < 4; i++)
            {
                int step = i + 1;
                var clip = Array.Find(animator.runtimeAnimatorController.animationClips, c => c.name == "PlayerPrototype_Attack_" + step);
                if (clip == null) throw new InvalidOperationException("Attack clip missing: " + step);
                var hitEvent = Array.Find(AnimationUtility.GetAnimationEvents(clip), e => e.functionName == "OnAttackHit");
                var endEvent = Array.Find(AnimationUtility.GetAnimationEvents(clip), e => e.functionName == "OnAttackEnd");
                int active = Mathf.RoundToInt(hitEvent.time * 60);
                attackData.steps[i] = new PlayerAttackDefinition.Step {
                    clip = clip, animatorState = clip.name, damage = new[] { 10, 10, 15, 25 }[i],
                    activeStart = active, activeEnd = active + 1, cancelStart = active + 2,
                    recoveryStart = Mathf.Max(active + 4, Mathf.RoundToInt(endEvent.time * 60)),
                    endFrame = Mathf.Max(active + 6, Mathf.RoundToInt(clip.length * 60)),
                    radius = .8f, forwardExtension = i == 0 ? .6125f : i == 1 ? .95625f : 0,
                    gaugeGain = i == 3 ? 5 : 2, motionDistance = .1f + i * .04f
                };
            }
            EditorUtility.SetDirty(attackData); Set(attack, "definition", attackData);

            var enemyData = LoadOrCreate<EnemyCombatDefinition>("NormalMelee");
            enemyData.targetLayers = playerMask; enemyData.obstacleLayers = groundMask; EditorUtility.SetDirty(enemyData);
            var enemies = new EnemyMeleeChaser2D[2];
            for (int i = 0; i < 2; i++)
            {
                var enemy = new GameObject("Melee_" + (i + 1)); enemy.layer = enemyLayer;
                enemy.transform.position = new Vector3(i == 0 ? 3 : 5, .8f);
                enemy.AddComponent<BoxCollider2D>().size = new Vector2(.7f, 1.6f);
                var hp = enemy.AddComponent<Damageable2D>(); Set(hp, "individualId", "prototype-melee-" + (i + 1));
                Set(hp, "maxHp", 30); Set(hp, "knockbackPower", 0f); Set(hp, "lightHitStunDuration", .1f);
                Set(hp, "normalParryStunDuration", 32f / 60f); Set(hp, "justParryStunDuration", 44f / 60f);
                Set(hp, "playerBodyLayers", playerMask);
                enemies[i] = enemy.AddComponent<EnemyMeleeChaser2D>(); Set(enemies[i], "target", player.transform); Set(enemies[i], "combatDefinition", enemyData);
                var silhouette = Box("Silhouette", Vector3.zero, new Vector2(.7f, 1.4f), new Color(.8f, .65f, .3f)); silhouette.transform.SetParent(enemy.transform, false);
                var weapon = Box("Weapon", Vector3.zero, new Vector2(1.1f, .12f), Color.white); weapon.transform.SetParent(enemy.transform, false);
                var cueObject = Box("Tell", new Vector3(0, 1.1f), new Vector2(.2f, .2f), Color.yellow); cueObject.transform.SetParent(enemy.transform, false); cueObject.transform.localPosition = Vector3.up * 1.1f;
                var cue = enemy.AddComponent<EnemyCombatCue2D>(); Set(cue, "silhouette", silhouette.transform); Set(cue, "weapon", weapon.transform); Set(cue, "cue", cueObject.GetComponent<SpriteRenderer>());
                enemy.SetActive(false);
            }
            var interact = Box("InteractCounter", new Vector3(-6, .5f), new Vector2(.6f, 1), Color.cyan);
            var trigger = interact.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(2, 2); trigger.isTrigger = true;
            interact.AddComponent<PrototypeInteractable2D>();
            BasicActionValidationSceneBuilder.EnsureInteractValidation(player);
            var arena = new GameObject("PrototypeArena").AddComponent<PrototypeCombatArena>();
            Set(arena, "player", state);
            var arenaData = new SerializedObject(arena); var list = arenaData.FindProperty("enemies"); list.arraySize = 2;
            for (int i = 0; i < 2; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            arenaData.ApplyModifiedPropertiesWithoutUndo();
            player.SetActive(true);
            PrototypeActionStageInstaller.InstallIntoScene(scene);
            PrototypeDialogueInstaller.InstallIntoScene(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
    private static T LoadOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = SettingsPath + "/" + name + ".asset";
        T data = AssetDatabase.LoadAssetAtPath<T>(path);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(data, path); return data;
    }
    private static GameObject Box(string name, Vector3 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name); obj.transform.position = position;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = GetUnlitMaterial();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size; renderer.color = color;
        return obj;
    }

    private static Material GetUnlitMaterial()
    {
        string path = SettingsPath + "/PrototypeUnlit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static void ApplyValidationView()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Open the generated validation scene in EditMode first.");
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = GetUnlitMaterial();
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.orthographicSize = PrototypeActionStageInstaller.CameraSize;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
    }
    private static void Set(Object target, string name, object value)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(name);
        if (property == null) throw new InvalidOperationException(target.name + ": missing " + name);
        if (value is Object obj) property.objectReferenceValue = obj;
        else if (value is int integer) property.intValue = integer;
        else if (value is float number) property.floatValue = number;
        else if (value is bool flag) property.boolValue = flag;
        else if (value is string text) property.stringValue = text;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
