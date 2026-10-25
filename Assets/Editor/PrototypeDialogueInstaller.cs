using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Additive installer: retains the existing stage, filming and authoring samples.</summary>
public static class PrototypeDialogueInstaller
{
    public const string Folder = "Assets/Settings/PrototypeDialogue";
    public const string PrefabPath = "Assets/Prefabs/PrototypeDialogueSystem.prefab";
    public const string SequencePath = Folder + "/Dialogue_PrototypeTest.asset";
    public const string ProfilePath = Folder + "/Presentation.asset";

    [MenuItem("Tools/Technical Sword Action/Prototype/Install Dialogue Test")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        if (scene.path != PrototypeCombatSceneBuilder.ScenePath)
            throw new InvalidOperationException("Open PrototypeCombatValidation first.");
        if (scene.isDirty) throw new InvalidOperationException("Save or review existing scene changes first.");
        InstallIntoScene(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void InstallIntoScene(Scene scene)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit PlayMode first.");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerStateMachine>(true)).Single();
        GameObject target = scene.GetRootGameObjects().Single(r => r.name == "InteractCounter");
        EnsureFolder();
        var profile = LoadOrCreate<DialoguePresentationProfile>(ProfilePath);
        var left = LoadOrCreate<DialogueSpeakerDefinition>(Folder + "/Speaker_PrototypeLeft.asset");
        var right = LoadOrCreate<DialogueSpeakerDefinition>(Folder + "/Speaker_PrototypeRight.asset");
        ConfigureNewSpeaker(left, "主人公（仮）", DialoguePortraitSide.Left);
        ConfigureNewSpeaker(right, "相手（仮）", DialoguePortraitSide.Right);
        var sequence = LoadOrCreate<DialogueSequence>(SequencePath);
        if (sequence.LineCount == 0)
        {
            var data = new SerializedObject(sequence);
            data.FindProperty("authoringTitle").stringValue = "Prototype portrait / dialogue test";
            var lines = data.FindProperty("lines");
            lines.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var line = lines.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("speaker").objectReferenceValue = i == 1 ? right : left;
                line.FindPropertyRelative("body").stringValue = "Test";
                line.FindPropertyRelative("authorNote").stringValue = "[仮置き] 左初登場→右初登場→左の再発言を確認する。";
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            sequence.SynchronizeStableIdsForEditor();
        }
        left.SynchronizeStableIdForEditor();
        right.SynchronizeStableIdForEditor();
        AssetDatabase.SaveAssetIfDirty(left);
        AssetDatabase.SaveAssetIfDirty(right);
        AssetDatabase.SaveAssetIfDirty(sequence);
        AssetDatabase.SaveAssetIfDirty(profile);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = CreatePrefab(profile);
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueController>(true))
            .FirstOrDefault(c => c.gameObject.name == "PrototypeDialogueSystem");
        if (controller == null)
            controller = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<DialogueController>();
        Set(controller, "playerStateMachine", player);
        Set(controller, "playerBody", player.GetComponent<Rigidbody2D>());

        // The unified state gate remains enabled; disabling Motor/Attack would unregister combat listeners.
        var lockData = new SerializedObject(controller);
        lockData.FindProperty("gameplayBehavioursToDisable").arraySize = 0;
        lockData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

        var legacyCounter = target.GetComponent<PrototypeInteractable2D>();
        if (legacyCounter != null) Undo.DestroyObjectImmediate(legacyCounter);
        var interactable = target.GetComponent<DialogueInteractable2D>();
        if (interactable == null) interactable = Undo.AddComponent<DialogueInteractable2D>(target);
        Set(interactable, "sequence", sequence);
        Set(interactable, "controller", controller);
        Set(interactable, "interactionPrompt", "E / B  会話テスト");
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureNewSpeaker(DialogueSpeakerDefinition speaker, string displayName, DialoguePortraitSide side)
    {
        // Never overwrite received portraits or an author's customized speaker on a repeated install.
        if (speaker.DisplayName != "話者") return;
        Set(speaker, "displayName", displayName);
        Set(speaker, "portraitSide", (int)side);
    }

    private static GameObject CreatePrefab(DialoguePresentationProfile profile)
    {
        var host = new GameObject("PrototypeDialogueSystem", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        host.SetActive(false);
        try
        {
            host.layer = 5;
            host.transform.localScale = Vector3.one;
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            canvas.pixelPerfect = true;
            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.matchWidthOrHeight = 0.5f;
            var controller = host.AddComponent<DialogueController>();
            RectTransform panel = Rect("Dialogue", host.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;
            var view = panel.gameObject.AddComponent<DialogueView>();
            var left = Portrait(panel, DialoguePortraitSide.Left);
            var right = Portrait(panel, DialoguePortraitSide.Right);
            var box = Image("ConversationBox", panel, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 12), new Vector2(608, 104), profile.placeholderBoxColor);
            box.rectTransform.pivot = new Vector2(.5f, 0);
            var accent = Image("Accent", box.transform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -1), new Vector2(0, 2), new Color(.45f, .8f, .9f, .75f));
            accent.raycastTarget = false;
            Text speaker = Label("Speaker", box.transform, new Vector2(0, 1), new Vector2(18, -12), new Vector2(360, 18), 12);
            speaker.color = new Color(.65f, .9f, 1f);
            Text body = Label("Body", box.transform, new Vector2(0, 1), new Vector2(18, -35), new Vector2(572, 42), 16);
            Text hint = Label("Continue", box.transform, new Vector2(1, 0), new Vector2(-18, 10), new Vector2(300, 16), 9);
            hint.rectTransform.pivot = new Vector2(1, 0);
            hint.alignment = TextAnchor.MiddleRight;
            Text sample = Label("ArtworkStatus", panel, new Vector2(1, 0), new Vector2(-22, 120), new Vector2(190, 16), 8);
            sample.rectTransform.pivot = new Vector2(1, 0);
            sample.alignment = TextAnchor.MiddleRight;
            sample.text = "PLACEHOLDER ART";
            sample.color = new Color(.2f, .25f, .3f);
            Set(view, "root", group);
            Set(view, "speakerLabel", speaker);
            Set(view, "bodyLabel", body);
            Set(view, "continueLabel", hint);
            Set(view, "presentation", profile);
            Set(view, "boxImage", box);
            Set(view, "leftPortrait", left);
            Set(view, "rightPortrait", right);
            Set(controller, "view", view);
            host.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(host, PrefabPath);
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static DialoguePortraitSlot Portrait(Transform parent, DialoguePortraitSide side)
    {
        bool left = side == DialoguePortraitSide.Left;
        var rect = Rect(left ? "LeftPortrait" : "RightPortrait", parent,
            new Vector2(left ? 0 : 1, 0), new Vector2(left ? 0 : 1, 0),
            new Vector2(left ? 116 : -116, 100), new Vector2(178, 238));
        rect.pivot = new Vector2(.5f, 0);
        rect.gameObject.AddComponent<CanvasGroup>();
        var slot = rect.gameObject.AddComponent<DialoguePortraitSlot>();
        var portrait = Image("Artwork", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        portrait.rectTransform.offsetMin = portrait.rectTransform.offsetMax = Vector2.zero;
        portrait.raycastTarget = false;
        var placeholder = Rect("Placeholder", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        placeholder.offsetMin = placeholder.offsetMax = Vector2.zero;
        Color tint = new(.38f, .44f, .52f, .88f);
        Image("Body", placeholder, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 76), new Vector2(94, 146), tint);
        Image("Head", placeholder, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 185), new Vector2(57, 62), tint);
        Image("LeftArm", placeholder, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-57, 77), new Vector2(16, 114), tint);
        Image("RightArm", placeholder, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(57, 77), new Vector2(16, 114), tint);
        var caption = Label("PlaceholderLabel", placeholder, new Vector2(.5f, 0), new Vector2(0, 145), new Vector2(140, 24), 10);
        caption.rectTransform.pivot = new Vector2(.5f, .5f);
        caption.alignment = TextAnchor.MiddleCenter;
        caption.text = left ? "LEFT SAMPLE" : "RIGHT SAMPLE";
        caption.color = Color.black;
        Set(slot, "side", (int)side);
        Set(slot, "portraitImage", portrait);
        Set(slot, "placeholder", placeholder.gameObject);
        return slot;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, min, max, position, size);
        if (min == max) rect.pivot = new Vector2(.5f, .5f);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect(name, parent, anchor, anchor, position, size);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "PrototypeDialogue");
    }

    private static void Set(Object target, string property, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string property, string value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(property).stringValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string property, int value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(property).intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
