using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
        EnsureInteractValidation(player.gameObject);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/BasicActionValidation.unity");
    }

    public static void EnsureInteractValidation(GameObject player)
    {
        if (player == null) throw new System.ArgumentNullException(nameof(player));
        var interactor = player.GetComponent<PlayerInteractor2D>();
        if (interactor == null) interactor = player.AddComponent<PlayerInteractor2D>();
        var prompt = Object.FindFirstObjectByType<InteractionPromptView>();
        if (prompt == null) prompt = CreatePrompt();
        var interactorData = new SerializedObject(interactor);
        interactorData.FindProperty("promptView").objectReferenceValue = prompt;
        interactorData.ApplyModifiedProperties();
        var stateData = new SerializedObject(player.GetComponent<PlayerStateMachine>());
        stateData.FindProperty("interactor").objectReferenceValue = interactor;
        stateData.ApplyModifiedProperties();
    }

    private static InteractionPromptView CreatePrompt()
    {
        var canvasObject = new GameObject("BasicActionPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.localScale = Vector3.one;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("InteractPrompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(InteractionPromptView));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(canvasObject.transform, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.38f);
        panelRect.sizeDelta = new Vector2(380f, 52f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
        panel.GetComponent<Image>().raycastTarget = false;

        var labelObject = new GameObject("PromptLabel", typeof(RectTransform), typeof(Text));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(panel.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<Text>();
        label.font = DialogueView.ResolveJapaneseFont();
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        var prompt = panel.GetComponent<InteractionPromptView>();
        var promptData = new SerializedObject(prompt);
        promptData.FindProperty("root").objectReferenceValue = panel.GetComponent<CanvasGroup>();
        promptData.FindProperty("promptLabel").objectReferenceValue = label;
        promptData.ApplyModifiedProperties();
        return prompt;
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
