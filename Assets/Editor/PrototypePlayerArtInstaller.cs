using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Status = PrototypePlayerArtSet.ArtworkStatus;
using Object = UnityEngine.Object;

/// <summary>Opt-in artwork only: do not rebuild video assets or rewrite the combat definitions.</summary>
public static class PrototypePlayerArtInstaller
{
    public const string Folder = "Assets/Animations/Player/Submitted";
    public const string ControllerPath = Folder + "/PlayerSubmitted.overrideController";
    public const string PresentationControllerPath = Folder + "/PlayerSubmittedBase.controller";
    public const string SetPath = "Assets/Settings/PrototypeCombat/PrototypePlayerArtSet.asset";
    public const string BaseControllerPath = "Assets/Animations/Player/Prototype/PlayerPrototypeAnimator.controller";
    private static readonly EditorCurveBinding SpriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

    [MenuItem("Tools/Technical Sword Action/Prototype/Refresh Walking Display Crops")]
    public static void RefreshWalkingDisplayCrops()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before updating artwork.");
        var set = AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(SetPath);
        var library = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>("Assets/Settings/CombatVideoArt.asset");
        if (set == null || library == null) throw new InvalidOperationException("Install the prototype artwork before refreshing its walking display.");
        // Do not regenerate other clips from a video library that may have changed independently.
        BuildWalkDisplayOverrides(set, library.Find("WalkReference"));
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
        ValidateOrThrow();
    }

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Submitted Player Art")]
    public static void Install()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode before installing artwork.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Prototype scene has unsaved changes; review/save them first.");
            var player = scene.GetRootGameObjects().Select(root => root.GetComponent<PlayerStateMachine>()).Single(p => p != null);
            var set = BuildAssets(player);
            var animator = player.GetComponent<Animator>();
            animator.runtimeAnimatorController = set.controller;
            var source = player.GetComponent<SpriteRenderer>();
            source.sprite = Sample(set.Find("Idle").clip, 0);
            var child = player.transform.Find("PlayerArtwork");
            if (child == null)
            {
                child = new GameObject("PlayerArtwork").transform;
                child.SetParent(player.transform, false);
            }
            child.gameObject.layer = player.gameObject.layer;
            // Unity can return a fake-null built-in component in EditMode; ?? is not Unity-null aware.
            var visual = child.GetComponent<SpriteRenderer>();
            if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
            var presentation = player.GetComponent<PrototypePlayerArtPresentation>();
            if (presentation == null) presentation = player.gameObject.AddComponent<PrototypePlayerArtPresentation>();
            presentation.Configure(source, visual, player.GetComponent<BoxCollider2D>(), set);
            if (presentation.Artwork == null) throw new InvalidOperationException("Artwork renderer was not connected.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateOrThrow();
            Debug.Log("[PrototypeArt] Connected 24 received/rough/substitute motions; combat data and video assets unchanged.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static PrototypePlayerArtSet BuildAssets(PlayerStateMachine player)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations/Player", "Submitted");
        var library = AssetDatabase.LoadAssetAtPath<VideoArtLibrary>("Assets/Settings/CombatVideoArt.asset");
        if (library == null) throw new InvalidOperationException("Existing video artwork library is missing.");
        var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
        if (baseController == null) throw new InvalidOperationException("Prototype base controller is missing.");
        var presentationController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PresentationControllerPath);
        if (presentationController == null)
        {
            if (!AssetDatabase.CopyAsset(BaseControllerPath, PresentationControllerPath))
                throw new InvalidOperationException("Could not make the prototype-only presentation controller.");
            presentationController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PresentationControllerPath);
        }
        // Configured attacks already call Animator.Play before emitting the legacy trigger.
        // A self-transition restarts that same state two ticks late; do not restart a visual already playing.
        foreach (var transition in presentationController.layers[0].stateMachine.anyStateTransitions)
        {
            if (transition.destinationState == null || !transition.destinationState.name.StartsWith("PlayerPrototype_Attack_")) continue;
            transition.canTransitionToSelf = false;
            EditorUtility.SetDirty(transition);
        }
        EditorUtility.SetDirty(presentationController); AssetDatabase.SaveAssetIfDirty(presentationController);
        var set = AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<PrototypePlayerArtSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        BuildWalkDisplayOverrides(set, library.Find("WalkReference"));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
        if (controller == null) { controller = new AnimatorOverrideController(); AssetDatabase.CreateAsset(controller, ControllerPath); }
        controller.runtimeAnimatorController = presentationController;

        var jumpStart = LoadSamples("Assets/Art/Player_jamp_start_sample.aseprite");
        var jumpUp = LoadSamples("Assets/Art/Player_jamp_up_sample.aseprite");
        set.feetPivotSprites = jumpStart.Concat(jumpUp).ToArray();
        var entries = new List<PrototypePlayerArtSet.Entry>();
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        var attack = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>("Assets/Settings/PrototypeCombat/FourStepAttack.asset");
        var counter = AssetDatabase.LoadAssetAtPath<ParryCounterDefinition>("Assets/Settings/PrototypeCombat/ParryCounter.asset");
        if (attack == null || !attack.IsValid || counter == null || !counter.IsValid)
            throw new InvalidOperationException("Valid existing combat definitions are required.");

        foreach (var original in baseController.animationClips.OrderBy(clip => clip.name))
        {
            string action = original.name.Replace("PlayerPrototype_", "");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + original.name + ".anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Folder + "/" + original.name + ".anim"); }
            EditorUtility.CopySerialized(original, clip);
            clip.name = original.name;
            clip.frameRate = 60;
            var entry = new PrototypePlayerArtSet.Entry { action = action, clip = clip, status = Status.Substitute };
            var keys = new List<ObjectReferenceKeyframe>();
            float end = original.length;
            bool loop = AnimationUtility.GetAnimationClipSettings(original).loopTime;
            VideoArtLibrary.Motion Motion(string name)
            {
                var motion = library.Find(name);
                if (motion == null || motion.frames == null || motion.frames.Length == 0 ||
                    motion.durations == null || motion.frames.Length != motion.durations.Length ||
                    motion.frames.Any(s => s == null) || motion.durations.Any(d => d < 1))
                    throw new InvalidOperationException("Incomplete source motion: " + name);
                entry.source = name; return motion;
            }
            void Key(float time, Sprite sprite) => keys.Add(new ObjectReferenceKeyframe { time = time, value = sprite });
            void Uniform(VideoArtLibrary.Motion motion, float seconds)
            {
                int frame = 0;
                for (int i = 0; i < motion.frames.Length; i++)
                { Key(seconds * frame / motion.TotalFrames, motion.frames[i]); frame += motion.durations[i]; }
                Key(seconds, motion.frames[^1]);
            }
            void Retimed(VideoArtLibrary.Motion motion, float hitTime, float? recoveryTime = null)
            {
                // Animator accumulation may be microscopically below an exact 60 Hz boundary.
                // A sub-tick epsilon prevents freezing the preceding cel on the damage tick.
                float visibleHitTime = Mathf.Max(0, hitTime - .00001f);
                // A hit marker may fall inside a held cel. Anchor the cel's START,
                // otherwise stretching a long startup can reveal impact many ticks too soon.
                int contactStart = 0;
                for (int i = 0; i < motion.frames.Length; i++)
                {
                    if (contactStart + motion.durations[i] > motion.hitFrame) break;
                    contactStart += motion.durations[i];
                }
                float recoverySource = motion.recoveryFrame > motion.hitFrame ? motion.recoveryFrame : motion.TotalFrames;
                var allFrames = motion.frames.ToList(); var durations = motion.durations.ToList();
                if (recoveryTime.HasValue && recoverySource >= motion.TotalFrames)
                {
                    var tail = library.Find("Recovery_" + motion.name);
                    if (tail != null) { allFrames.AddRange(tail.frames); durations.AddRange(tail.durations); }
                }
                int total = durations.Sum(); int frame = 0;
                float Map(float sourceFrame)
                {
                    if (sourceFrame <= contactStart) return visibleHitTime * sourceFrame / Mathf.Max(1, contactStart);
                    if (recoveryTime.HasValue && recoverySource < total)
                    {
                        if (sourceFrame < recoverySource)
                            return Mathf.Lerp(visibleHitTime, recoveryTime.Value, (sourceFrame - contactStart) / (recoverySource - contactStart));
                        return Mathf.Lerp(recoveryTime.Value, end, (sourceFrame - recoverySource) / (total - recoverySource));
                    }
                    return Mathf.Lerp(visibleHitTime, end, (sourceFrame - contactStart) / Mathf.Max(1, total - contactStart));
                }
                for (int i = 0; i < allFrames.Count; i++) { Key(Map(frame), allFrames[i]); frame += durations[i]; }
                Key(end, allFrames[^1]);
                entry.contactSprite = motion.At(motion.hitFrame);
                entry.visualHitFrame = hitTime * 60;
            }

            if (action.StartsWith("Attack_"))
            {
                int index = int.Parse(action.Substring("Attack_".Length)) - 1;
                string[] names = { "VerticalSlash", "HorizontalSlash", "RapidSheathe", "IaiSlash" };
                var step = attack.steps[index];
                entry.status = index < 2 ? Status.SubmittedProvisional : Status.Rough;
                // PlayerAttack2D scales its Animator from the ORIGINAL definition clip length.
                Retimed(Motion(names[index]), end * step.activeStart / step.endFrame, end * step.recoveryStart / step.endFrame);
            }
            else switch (action)
            {
                case "Idle": case "Move":
                {
                    var motion = Motion(action == "Idle" ? "IdleReference" : "WalkReference");
                    float celSeconds = action == "Idle" ? .1f : .08f;
                    end = celSeconds * motion.frames.Length;
                    for (int i = 0; i < motion.frames.Length; i++) Key(i * celSeconds, motion.frames[i]);
                    Key(end, motion.frames[0]); entry.status = Status.Received; loop = true; break;
                }
                case "Jump":
                    entry.source = "Player_jamp_start_sample + Player_jamp_up_sample";
                    for (int i = 0; i < jumpStart.Length; i++) Key(i * .05f, jumpStart[i]);
                    Key(jumpStart.Length * .05f, jumpUp[0]); Key(end, jumpUp[0]); entry.status = Status.SubmittedProvisional; break;
                case "Fall": case "DropThrough": case "AirRecovery":
                    entry.source = "Player_jamp_up_sample"; Key(0, jumpUp[0]); Key(end, jumpUp[0]); break;
                case "Dash":
                    Uniform(Motion("Dash"), end); entry.status = Status.Rough; break;
                case "Parry":
                {
                    var ready = Motion("ParryReady"); end = .45f;
                    Key(0, ready.frames[0]); Key(2f / 60, ready.frames[1]); Key(4f / 60, ready.frames[2]); Key(end, ready.frames[2]);
                    entry.status = Status.Received; break;
                }
                case "ParrySuccess":
                    Uniform(Motion("ParrySuccessLight"), end); entry.status = Status.SubmittedProvisional; break;
                case "ParryFail":
                {
                    var ready = Motion("ParryReady");
                    for (int i = 3; i < ready.frames.Length; i++) Key(end * (i - 3) / (ready.frames.Length - 3), ready.frames[i]);
                    Key(end, ready.frames[^1]); break;
                }
                case "ParryCounter":
                    // The extended-state animation driver enters this state one visual tick
                    // after its gameplay timer begins. Compensate in the artwork, not in damage.
                    Retimed(Motion("CounterKick"), Mathf.Max(0, counter.hitFrame - 1) / 60f);
                    entry.status = Status.SubmittedProvisional; break;
                case "Special":
                {
                    var skill = new SerializedObject(player.GetComponent<PlayerSpecialSkill2D>());
                    float hit = skill.FindProperty("startupLock").floatValue;
                    Retimed(Motion("IaiSlash"), hit); break;
                }
                case "ComboBranch": case "AirDashSlash":
                    Uniform(Motion("HorizontalSlash"), end); break;
                case "Hit":
                {
                    var motion = Motion("ParryDeflect"); Key(0, motion.frames[2]); Key(end, motion.frames[2]); break;
                }
                case "Death": case "LandingShock":
                {
                    var motion = Motion("AirRoundhouse"); Key(0, motion.frames[^1]); Key(end, motion.frames[^1]); break;
                }
                default:
                    Uniform(Motion("IdleReference"), end); break;
            }

            // Equal-time keys can arise when a contact cel is deliberately held. Keep the last one.
            // Sprite curves include one sample AFTER their final key in clip.length.
            // Place the terminal hold one sample before the desired end, including loops.
            float lastSample = Mathf.Max(0, end - 1f / clip.frameRate);
            Object finalSprite = loop ? keys.Last(key => key.time < end).value : keys[^1].value;
            var curve = keys.Where(key => key.time < lastSample).ToList();
            curve.Add(new ObjectReferenceKeyframe { time = lastSample, value = finalSprite });
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding,
                curve.GroupBy(key => key.time).Select(group => group.Last()).OrderBy(key => key.time).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = end; settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            entries.Add(entry); overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, clip));
        }
        controller.ApplyOverrides(overrides);
        set.controller = controller; set.entries = entries.ToArray();
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssetIfDirty(set);
        return set;
    }

    private static void BuildWalkDisplayOverrides(PrototypePlayerArtSet set, VideoArtLibrary.Motion walk)
    {
        if (walk == null || walk.frames == null || walk.frames.Length != 8 || walk.frames.Any(sprite => sprite == null))
            throw new InvalidOperationException("The reviewed eight-cel walking source is required for its display crop.");
        var existing = AssetDatabase.LoadAllAssetsAtPath(SetPath).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        var replacements = new List<PrototypePlayerArtSet.DisplayOverride>();
        foreach (var source in walk.frames)
        {
            // This received 64px source has a floor guide at row 5 from the bottom.
            // Rows 0..4 are transparent; all character pixels start at row 6 or higher.
            // Crop UVs/mesh only, keeping the shared texture and world-space pixel positions.
            if (source.rect.size != new Vector2(64, 64) || source.pivot != new Vector2(32, 38))
                throw new InvalidOperationException("Review the walking guide crop after source dimensions/pivot change: " + source.name);
            var rect = new Rect(source.rect.x, source.rect.y + 6, source.rect.width, source.rect.height - 6);
            var pivotPixels = source.pivot - Vector2.up * 6;
            string name = source.name + "_PrototypeDisplay";
            existing.TryGetValue(name, out var display);
            if (display == null || display.texture != source.texture || display.rect != rect ||
                display.pivot != pivotPixels || !Mathf.Approximately(display.pixelsPerUnit, source.pixelsPerUnit))
            {
                var created = Sprite.Create(source.texture, rect, new Vector2(pivotPixels.x / rect.width, pivotPixels.y / rect.height),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                created.name = name;
                if (display == null)
                {
                    display = created;
                    AssetDatabase.AddObjectToAsset(display, set);
                }
                else
                {
                    EditorUtility.CopySerialized(created, display);
                    Object.DestroyImmediate(created);
                }
                EditorUtility.SetDirty(display);
            }
            replacements.Add(new PrototypePlayerArtSet.DisplayOverride { source = source, display = display });
        }
        set.displayOverrides = replacements.ToArray();
    }

    private static Sprite[] LoadSamples(string path)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        if (sprites.Length == 0) throw new InvalidOperationException("Missing submitted jump sprites: " + path);
        return sprites;
    }

    public static Sprite Sample(AnimationClip clip, float time)
    {
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding);
        return keys.Last(key => key.time <= time + .00001f).value as Sprite;
    }

    [MenuItem("Tools/Technical Sword Action/Prototype/Validate Submitted Player Art")]
    public static void ValidateOrThrow()
    {
        var errors = Validate();
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
    }

    public static string[] Validate()
    {
        var errors = new List<string>();
        var set = AssetDatabase.LoadAssetAtPath<PrototypePlayerArtSet>(SetPath);
        if (set == null || set.controller == null || set.entries.Length != 24) return new[] { "Missing prototype art set or 24 clip overrides." };
        if (set.displayOverrides == null || set.displayOverrides.Length != 8)
            errors.Add("Missing eight prototype-only walking display crops.");
        else foreach (var replacement in set.displayOverrides)
        {
            if (replacement == null || replacement.source == null || replacement.display == null)
            { errors.Add("Missing walking display source/crop."); continue; }
            var source = replacement.source; var display = replacement.display;
            if (display.texture != source.texture || AssetDatabase.GetAssetPath(display) != SetPath ||
                display.rect != new Rect(source.rect.x, source.rect.y + 6, source.rect.width, source.rect.height - 6) ||
                display.pivot + Vector2.up * 6 != source.pivot || !Mathf.Approximately(display.pixelsPerUnit, source.pixelsPerUnit))
                errors.Add(source.name + ": walking display crop changes pixels, pivot, scale or source ownership.");
        }
        if (AssetDatabase.GetAssetPath(set.controller.runtimeAnimatorController) != PresentationControllerPath)
            errors.Add("Artwork must preserve the prototype controller/state contract.");
        var presentationController = set.controller.runtimeAnimatorController as AnimatorController;
        if (presentationController == null || presentationController.layers[0].stateMachine.anyStateTransitions.Any(t =>
            t.destinationState != null && t.destinationState.name.StartsWith("PlayerPrototype_Attack_") && t.canTransitionToSelf))
            errors.Add("Configured attacks must not restart their own presentation state.");
        var attack = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>("Assets/Settings/PrototypeCombat/FourStepAttack.asset");
        foreach (var entry in set.entries)
        {
            if (entry.clip == null) { errors.Add(entry.action + ": missing clip."); continue; }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(entry.clip))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(entry.clip, binding))
                    if (key.value == null || AssetDatabase.GetAssetPath(key.value).Contains("/Player/Prototype/"))
                        errors.Add(entry.action + ": missing or legacy stick-figure sprite.");
            if (AnimationUtility.GetCurveBindings(entry.clip).Length != 0)
                errors.Add(entry.action + ": artwork must not animate gameplay transforms/physics.");
            if (entry.contactSprite != null && Sample(entry.clip, entry.visualHitFrame / 60) != entry.contactSprite)
                errors.Add(entry.action + ": contact pose does not match its visual marker.");
            if (entry.action.StartsWith("Attack_"))
            {
                int index = int.Parse(entry.action.Substring(7)) - 1;
                var definition = attack.steps[index];
                if (Mathf.Abs(entry.clip.length - definition.clip.length) > .00001f)
                    errors.Add(entry.action + ": changed attack clip duration.");
                float scaledHit = entry.visualHitFrame * definition.endFrame / (definition.clip.length * 60);
                if (Mathf.Abs(scaledHit - definition.activeStart) > 2.001f)
                    errors.Add(entry.action + ": visual hit is more than 2F from configured damage.");
                var contactKeys = AnimationUtility.GetObjectReferenceCurve(entry.clip, SpriteBinding)
                    .Where(key => key.value == entry.contactSprite).ToArray();
                float firstContact = contactKeys.Length > 0
                    ? contactKeys[0].time * definition.endFrame / definition.clip.length : float.NegativeInfinity;
                if (Mathf.Abs(firstContact - definition.activeStart) > 2.001f)
                    errors.Add(entry.action + ": contact cel starts more than 2F from damage.");
                var originalEvents = AnimationUtility.GetAnimationEvents(definition.clip);
                var events = AnimationUtility.GetAnimationEvents(entry.clip);
                if (events.Length != originalEvents.Length || events.Where((e, i) =>
                    e.functionName != originalEvents[i].functionName || e.intParameter != originalEvents[i].intParameter ||
                    Mathf.Abs(e.time - originalEvents[i].time) > .00001f).Any())
                    errors.Add(entry.action + ": changed AnimationEvent contract.");
            }
        }
        return errors.ToArray();
    }
}
