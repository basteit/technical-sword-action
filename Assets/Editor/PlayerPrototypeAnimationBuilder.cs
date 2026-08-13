using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PlayerPrototypeAnimationBuilder
{
    private const int CellSize = 64;
    private const int AtlasHeight = 1536;
    // The stick figure's grounded foot pixels end on source row 57 (top-origin).
    // A 38 px pivot places that row's lower edge exactly one unit below the Player root,
    // matching the bottom of the 2-unit-tall CapsuleCollider2D without moving gameplay objects.
    private const float GroundedPivotYInPixels = 38f;
    private const string AtlasPath = "Assets/Art/Player/Prototype/PlayerPrototype_Motions.png";
    private const string ReferencePath = "Assets/Art/Player/Prototype/PlayerPrototype_Reference.png";
    private const string OutputFolder = "Assets/Animations/Player/Prototype";
    private const string ControllerPath = OutputFolder + "/PlayerPrototypeAnimator.controller";

    private sealed class MotionSpec
    {
        public MotionSpec(string name, int row, bool loop, float[] times)
        {
            Name = name;
            Row = row;
            Loop = loop;
            Times = times;
        }

        public string Name { get; }
        public int Row { get; }
        public bool Loop { get; }
        public float[] Times { get; }
        public int FrameCount => Times.Length - 1;
        public string ClipPath => $"{OutputFolder}/PlayerPrototype_{Name}.anim";
        public string StateName => $"PlayerPrototype_{Name}";
    }

    private static readonly MotionSpec[] Motions =
    {
        new("Idle", 0, true, new[] { 0f, 0.125f, 0.25f, 0.375f, 0.5f }),
        new("Move", 1, true, new[] { 0f, 0.083333f, 0.166667f, 0.25f, 0.333333f, 0.416667f, 0.5f }),
        new("Jump", 2, false, new[] { 0f, 0.08f, 0.18f, 0.3f, 0.4f }),
        new("Fall", 3, true, new[] { 0f, 0.12f, 0.24f, 0.36f }),
        new("DropThrough", 4, false, new[] { 0f, 0.08f, 0.16f, 0.26f, 0.34f }),
        new("Dash", 5, false, new[] { 0f, 0.033333f, 0.066667f, 0.116667f, 0.166667f, 0.183333f }),
        new("Attack_1", 6, false, new[] { 0f, 0.083333f, 0.166667f, 0.25f, 0.333333f, 0.416667f, 0.5f, 0.6f, 0.616667f }),
        new("Attack_2", 7, false, new[] { 0f, 0.066667f, 0.133333f, 0.2f, 0.266667f, 0.333333f, 0.4f, 0.5f, 0.516667f }),
        new("Attack_3", 8, false, new[] { 0f, 0.083333f, 0.166667f, 0.25f, 0.333333f, 0.416667f, 0.5f, 0.6f, 0.616667f }),
        new("Attack_4", 9, false, new[] { 0f, 0.133333f, 0.233333f, 0.333333f, 0.433333f, 0.533333f, 0.633333f, 0.733333f, 0.833333f, 0.933333f, 1f, 1.016667f }),
        new("Parry", 10, false, new[] { 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.216667f }),
        new("ParrySuccess", 11, false, new[] { 0f, 0.05f, 0.1f, 0.15f, 0.166667f }),
        new("ParryFail", 12, false, new[] { 0f, 0.1f, 0.2f, 0.3f, 0.316667f }),
        new("ParryCounter", 13, false, new[] { 0f, 0.066667f, 0.133333f, 0.2f, 0.3f, 0.4f, 0.55f, 0.566667f }),
        new("Special", 14, false, new[] { 0f, 0.08f, 0.16f, 0.24f, 0.32f, 0.42f, 0.52f, 0.65f, 0.78f, 0.79f }),
        new("Hit", 15, false, new[] { 0f, 0.033333f, 0.066667f, 0.1f }),
        new("Heal", 16, false, new[] { 0f, 0.12f, 0.24f, 0.4f, 0.58f, 0.74f, 0.8f }),
        new("Death", 17, false, new[] { 0f, 0.1f, 0.2f, 0.32f, 0.45f, 0.6f, 0.8f, 1f, 1.016667f }),
        new("Respawn", 18, false, new[] { 0f, 0.12f, 0.24f, 0.38f, 0.55f, 0.72f, 0.8f }),
        new("Rest", 19, true, new[] { 0f, 0.18f, 0.36f, 0.54f, 0.72f, 0.9f, 1.08f }),
        new("ComboBranch", 20, false, new[] { 0f, 0.08f, 0.16f, 0.24f, 0.34f, 0.46f, 0.6f, 0.616667f }),
        new("AirRecovery", 21, false, new[] { 0f, 0.08f, 0.16f, 0.24f, 0.32f }),
        new("AirDashSlash", 22, false, new[] { 0f, 0.05f, 0.1f, 0.16f, 0.24f, 0.34f, 0.36f }),
        new("LandingShock", 23, false, new[] { 0f, 0.08f, 0.16f, 0.24f, 0.36f, 0.48f })
    };

    [MenuItem("Tools/Player Prototype/Build Animation Set")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
        {
            throw new InvalidOperationException($"Prototype atlas was not found at {AtlasPath}.");
        }

        EnsureOutputFolder();
        ConfigureAtlasAndSlices();
        ConfigureReferenceTexture();

        Dictionary<string, Sprite> sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(AtlasPath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, sprite => sprite);

        Dictionary<string, AnimationClip> clips = new();
        foreach (MotionSpec motion in Motions)
        {
            clips.Add(motion.Name, CreateOrUpdateClip(motion, sprites));
        }

        AnimatorController controller = CreateOrUpdateController(clips);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = controller;
        Debug.Log($"[PlayerPrototype] Built {Motions.Length} clips from {sprites.Count} sprites: {ControllerPath}");
    }

    [MenuItem("Tools/Player Prototype/Validate Animation Set")]
    public static void Validate()
    {
        List<string> problems = new();
        Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        int expectedSpriteCount = Motions.Sum(motion => motion.FrameCount);
        if (sprites.Length != expectedSpriteCount)
        {
            problems.Add($"Expected {expectedSpriteCount} sprites but found {sprites.Length}.");
        }

        foreach (Sprite sprite in sprites)
        {
            if (!Mathf.Approximately(sprite.pixelsPerUnit, 32f)
                || !Mathf.Approximately(sprite.rect.width, CellSize)
                || !Mathf.Approximately(sprite.rect.height, CellSize)
                || !Mathf.Approximately(sprite.pivot.x, CellSize * 0.5f)
                || !Mathf.Approximately(sprite.pivot.y, GroundedPivotYInPixels))
            {
                problems.Add($"{sprite.name} must be 64x64, 32 PPU, with the grounded pivot at (32, 38) px.");
            }
        }

        foreach (MotionSpec motion in Motions)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motion.ClipPath);
            if (clip == null)
            {
                problems.Add($"Missing clip: {motion.ClipPath}");
                continue;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].propertyName != "m_Sprite")
            {
                problems.Add($"{motion.Name} does not contain exactly one SpriteRenderer sprite curve.");
            }

            if (AnimationUtility.GetCurveBindings(clip).Length != 0)
            {
                problems.Add($"{motion.Name} contains a non-sprite curve that could conflict with gameplay visuals.");
            }

            if (bindings.Length == 1)
            {
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                if (keys.Length != motion.FrameCount + 1)
                {
                    problems.Add($"{motion.Name} expected {motion.FrameCount + 1} sprite keys but found {keys.Length}.");
                }
                else
                {
                    for (int frame = 0; frame < motion.FrameCount; frame++)
                    {
                        string expectedName = GetSpriteName(motion, frame);
                        if (keys[frame].value is not Sprite sprite || sprite.name != expectedName)
                        {
                            problems.Add($"{motion.Name} frame {frame} does not reference {expectedName}.");
                        }
                    }

                    if (keys[^1].value != keys[^2].value || !Mathf.Approximately(keys[^1].time, motion.Times[^1]))
                    {
                        problems.Add($"{motion.Name} does not hold its final sprite through the clip end time.");
                    }
                }
            }

            ValidateAnimationEvents(motion, clip, problems);
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            problems.Add($"Missing controller: {ControllerPath}");
        }
        else
        {
            ValidateController(controller, problems);
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Player prototype validation failed:\n- " + string.Join("\n- ", problems));
        }

        Debug.Log($"[PlayerPrototype] Validation passed: {Motions.Length} clips/states, {expectedSpriteCount} sprites, exact attack events intact.");
    }

    private static void ValidateAnimationEvents(MotionSpec motion, AnimationClip clip, ICollection<string> problems)
    {
        AnimationEvent[] actual = AnimationUtility.GetAnimationEvents(clip);
        AnimationEvent[] expected = GetCombatEvents(motion.Name);
        if (actual.Length != expected.Length)
        {
            problems.Add($"{motion.Name} expected {expected.Length} Animation Events but found {actual.Length}.");
            return;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            AnimationEvent actualEvent = actual[index];
            AnimationEvent expectedEvent = expected[index];
            if (!Mathf.Approximately(actualEvent.time, expectedEvent.time)
                || actualEvent.functionName != expectedEvent.functionName
                || actualEvent.intParameter != expectedEvent.intParameter)
            {
                problems.Add($"{motion.Name} event {index} must be {expectedEvent.functionName}({expectedEvent.intParameter}) at {expectedEvent.time:0.######}s.");
            }
        }
    }

    private static void ValidateController(AnimatorController controller, ICollection<string> problems)
    {
        Dictionary<string, AnimatorControllerParameter> parameters = controller.parameters
            .GroupBy(parameter => parameter.name)
            .ToDictionary(group => group.Key, group => group.First());
        if (parameters.Count != 2
            || !parameters.TryGetValue("ComboStep", out AnimatorControllerParameter comboStep)
            || comboStep.type != AnimatorControllerParameterType.Int
            || !parameters.TryGetValue("AttackTrigger", out AnimatorControllerParameter attackTrigger)
            || attackTrigger.type != AnimatorControllerParameterType.Trigger)
        {
            problems.Add("Animator parameters must be ComboStep (Int) and AttackTrigger (Trigger).");
        }

        if (controller.layers.Length != 1)
        {
            problems.Add($"Animator expected one layer but found {controller.layers.Length}.");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Dictionary<string, AnimatorState> states = stateMachine.states
            .Select(child => child.state)
            .GroupBy(state => state.name)
            .ToDictionary(group => group.Key, group => group.First());
        if (states.Count != Motions.Length)
        {
            problems.Add($"Animator expected {Motions.Length} states but found {states.Count}.");
        }

        foreach (MotionSpec motion in Motions)
        {
            AnimationClip expectedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motion.ClipPath);
            if (!states.TryGetValue(motion.StateName, out AnimatorState state))
            {
                problems.Add($"Animator is missing state {motion.StateName}.");
            }
            else if (state.motion != expectedClip)
            {
                problems.Add($"Animator state {motion.StateName} references the wrong clip.");
            }
        }

        if (stateMachine.defaultState == null || stateMachine.defaultState.name != "PlayerPrototype_Idle")
        {
            problems.Add("Animator default state must be PlayerPrototype_Idle.");
        }
    }

    private static void EnsureOutputFolder()
    {
        const string playerFolder = "Assets/Animations/Player";
        if (!AssetDatabase.IsValidFolder(playerFolder))
        {
            throw new InvalidOperationException($"Expected animation folder was not found: {playerFolder}");
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder(playerFolder, "Prototype");
        }
    }

    private static void ConfigureAtlasAndSlices()
    {
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"TextureImporter was not available for {AtlasPath}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        TextureImporterSettings importerSettings = new();
        importer.ReadTextureSettings(importerSettings);
        importerSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(importerSettings);
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
        {
            throw new InvalidOperationException("The Sprite Editor data provider was not available for the prototype atlas.");
        }

        dataProvider.InitSpriteEditorDataProvider();
        Dictionary<string, GUID> existingIds = dataProvider.GetSpriteRects()
            .GroupBy(rect => rect.name)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        List<SpriteRect> rects = new();
        List<SpriteNameFileIdPair> nameIdPairs = new();
        foreach (MotionSpec motion in Motions)
        {
            for (int frame = 0; frame < motion.FrameCount; frame++)
            {
                string spriteName = GetSpriteName(motion, frame);
                GUID spriteId = existingIds.TryGetValue(spriteName, out GUID existingId) ? existingId : GUID.Generate();
                SpriteRect rect = new()
                {
                    name = spriteName,
                    rect = new Rect(frame * CellSize, AtlasHeight - ((motion.Row + 1) * CellSize), CellSize, CellSize),
                    pivot = new Vector2(0.5f, GroundedPivotYInPixels / CellSize),
                    alignment = SpriteAlignment.Custom,
                    border = Vector4.zero,
                    spriteID = spriteId
                };
                rects.Add(rect);
                nameIdPairs.Add(new SpriteNameFileIdPair(spriteName, spriteId));
            }
        }

        dataProvider.SetSpriteRects(rects.ToArray());
        ISpriteNameFileIdDataProvider nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider?.SetNameFileIdPairs(nameIdPairs);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static void ConfigureReferenceTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ReferencePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static AnimationClip CreateOrUpdateClip(MotionSpec motion, IReadOnlyDictionary<string, Sprite> sprites)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motion.ClipPath);
        bool isNew = clip == null;
        if (isNew)
        {
            clip = new AnimationClip { name = $"PlayerPrototype_{motion.Name}" };
        }

        clip.frameRate = 60f;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[motion.FrameCount + 1];
        for (int frame = 0; frame < motion.FrameCount; frame++)
        {
            string spriteName = GetSpriteName(motion, frame);
            if (!sprites.TryGetValue(spriteName, out Sprite sprite))
            {
                throw new InvalidOperationException($"Missing sliced sprite: {spriteName}");
            }

            keys[frame] = new ObjectReferenceKeyframe
            {
                time = motion.Times[frame],
                value = sprite
            };
        }

        keys[^1] = new ObjectReferenceKeyframe
        {
            time = motion.Times[^1],
            value = keys[^2].value
        };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        SetLoopTime(clip, motion.Loop);
        AnimationUtility.SetAnimationEvents(clip, GetCombatEvents(motion.Name));

        if (isNew)
        {
            AssetDatabase.CreateAsset(clip, motion.ClipPath);
        }
        else
        {
            EditorUtility.SetDirty(clip);
        }

        return clip;
    }

    private static AnimationEvent[] GetCombatEvents(string motionName)
    {
        return motionName switch
        {
            "Attack_1" => CreateAttackEvents(1, 0.25f, 0.366667f, 0.533333f, 0.583333f),
            "Attack_2" => CreateAttackEvents(2, 0.2f, 0.3f, 0.45f, 0.483333f),
            "Attack_3" => CreateAttackEvents(3, 0.25f, 0.366667f, 0.55f, 0.583333f),
            "Attack_4" => CreateAttackEvents(4, 0.633333f, 0.733333f, 0.933333f, 0.983333f),
            _ => Array.Empty<AnimationEvent>()
        };
    }

    private static AnimationEvent[] CreateAttackEvents(int step, float hit, float windowOpen, float windowClose, float end)
    {
        return new[]
        {
            new AnimationEvent { time = hit, functionName = "OnAttackHit", intParameter = step },
            new AnimationEvent { time = windowOpen, functionName = "OnComboWindowOpen", intParameter = step },
            new AnimationEvent { time = windowClose, functionName = "OnComboWindowClose", intParameter = step },
            new AnimationEvent { time = end, functionName = "OnAttackEnd", intParameter = step }
        };
    }

    private static void SetLoopTime(AnimationClip clip, bool loop)
    {
        SerializedObject serializedClip = new(clip);
        SerializedProperty loopProperty = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
        if (loopProperty == null)
        {
            throw new InvalidOperationException($"Could not set loop mode on {clip.name}.");
        }

        loopProperty.boolValue = loop;
        serializedClip.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AnimatorController CreateOrUpdateController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
        {
            controller.RemoveParameter(parameter);
        }
        controller.AddParameter("ComboStep", AnimatorControllerParameterType.Int);
        controller.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(child.state);
        }
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        Dictionary<string, AnimatorState> states = new();
        int columnCount = 4;
        for (int index = 0; index < Motions.Length; index++)
        {
            MotionSpec motion = Motions[index];
            Vector3 position = new(240 + (index % columnCount) * 220, 80 + (index / columnCount) * 80, 0);
            AnimatorState state = stateMachine.AddState(motion.StateName, position);
            state.motion = clips[motion.Name];
            state.writeDefaultValues = true;
            states.Add(motion.Name, state);
        }

        stateMachine.defaultState = states["Idle"];
        for (int step = 1; step <= 4; step++)
        {
            AnimatorState attackState = states[$"Attack_{step}"];
            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(attackState);
            enter.hasExitTime = false;
            enter.duration = 0f;
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "AttackTrigger");
            enter.AddCondition(AnimatorConditionMode.Equals, step, "ComboStep");

            AnimatorStateTransition exit = attackState.AddTransition(states["Idle"]);
            exit.hasExitTime = false;
            exit.duration = 0f;
            exit.AddCondition(AnimatorConditionMode.Equals, 0f, "ComboStep");
        }

        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static string GetSpriteName(MotionSpec motion, int frame)
    {
        return $"PlayerProto_{motion.Name}_{frame:00}";
    }
}
