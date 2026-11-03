using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Content")]
    [SerializeField] private Text speakerLabel;
    [SerializeField] private Text bodyLabel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Text continueLabel;

    [Header("Two-character presentation (legacy single portrait remains supported)")]
    [SerializeField] private DialoguePresentationProfile presentation;
    [SerializeField] private Image boxImage;
    [SerializeField] private DialoguePortraitSlot leftPortrait;
    [SerializeField] private DialoguePortraitSlot rightPortrait;

    [Header("Typography")]
    [SerializeField] private Font dialogueFont;
    [SerializeField] private string continueText = "E / B / A / Enter  次へ";
    [SerializeField] private string closeText = "E / B / A / Enter  閉じる";

    private static Font runtimeJapaneseFont;
    private bool sessionVisible;
    private float fadeElapsed;

    public bool IsVisible => root != null && root.alpha > 0.001f;
    public bool IsAppearing => sessionVisible && root != null && root.alpha < 1f;
    public float Opacity => root != null ? root.alpha : 0f;

    private void Awake()
    {
        ResolveReferences();
        ApplyFont();
        HideImmediate();
    }

    private void Update() => AdvanceVisuals(Time.unscaledDeltaTime);

    public void AdvanceVisuals(float unscaledDeltaTime)
    {
        if (!sessionVisible) return;
        float delta = Mathf.Max(0f, unscaledDeltaTime);
        fadeElapsed += delta;
        float seconds = presentation != null ? presentation.fadeInSeconds : 0f;
        if (root != null) root.alpha = seconds <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / seconds);
        leftPortrait?.AdvanceVisuals(delta);
        rightPortrait?.AdvanceVisuals(delta);
    }

    public void Show(DialogueLine line, bool isLastLine)
    {
        ResolveReferences();
        ApplyFont();
        ApplyPresentation();

        if (speakerLabel != null)
        {
            string speakerName = line?.Speaker != null ? line.Speaker.DisplayName : string.Empty;
            speakerLabel.text = speakerName;
            speakerLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(speakerName));
        }

        if (bodyLabel != null)
        {
            bodyLabel.text = line?.Body ?? string.Empty;
        }

        if (leftPortrait != null && rightPortrait != null)
        {
            bool isLeft = line?.Speaker != null && line.Speaker.PortraitSide == DialoguePortraitSide.Left;
            if (line?.Speaker != null || line?.Portrait != null)
            {
                DialoguePortraitSlot slot = isLeft ? leftPortrait : rightPortrait;
                slot.Show(line.Speaker, line.Portrait,
                    presentation != null ? presentation.portraitSlideSeconds : 0.3f,
                    presentation != null ? presentation.portraitSlideDistance : 80f);
            }
            float inactive = presentation != null ? presentation.inactivePortraitBrightness : 0.65f;
            leftPortrait.SetSpeaking(isLeft, inactive);
            rightPortrait.SetSpeaking(!isLeft, inactive);
            if (portraitImage != null) portraitImage.gameObject.SetActive(false);
        }
        else if (portraitImage != null)
        {
            Sprite portrait = line?.Portrait;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        if (continueLabel != null)
        {
            continueLabel.text = isLastLine ? closeText : continueText;
        }

        if (!sessionVisible)
        {
            sessionVisible = true;
            fadeElapsed = 0f;
            if (root != null)
            {
                root.alpha = presentation == null || presentation.fadeInSeconds <= 0f ? 1f : 0f;
                root.interactable = root.blocksRaycasts = true;
            }
        }
    }

    public void HideImmediate()
    {
        sessionVisible = false;
        fadeElapsed = 0f;
        leftPortrait?.HideImmediate();
        rightPortrait?.HideImmediate();
        SetVisible(false);
    }

    private void OnDisable()
    {
        bool wasVisible = sessionVisible;
        HideImmediate();
        if (wasVisible) GetComponentInParent<DialogueController>()?.Interrupt();
    }

    private void ApplyPresentation()
    {
        if (boxImage == null || presentation == null) return;
        boxImage.sprite = presentation.boxSprite;
        boxImage.color = presentation.boxSprite != null ? presentation.boxTint : presentation.placeholderBoxColor;
        boxImage.type = presentation.boxSprite != null && presentation.boxSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced : Image.Type.Simple;
        boxImage.rectTransform.sizeDelta = presentation.boxSize;
        boxImage.rectTransform.anchoredPosition = presentation.boxPosition;
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
        {
            return;
        }

        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = visible;
    }

    private void ResolveReferences()
    {
        if (root == null)
        {
            root = GetComponent<CanvasGroup>();
        }
    }

    private void ApplyFont()
    {
        Font font = dialogueFont != null ? dialogueFont : ResolveJapaneseFont();
        if (font == null)
        {
            return;
        }

        if (speakerLabel != null) speakerLabel.font = font;
        if (bodyLabel != null) bodyLabel.font = font;
        if (continueLabel != null) continueLabel.font = font;
    }

    public static Font ResolveJapaneseFont()
    {
        if (runtimeJapaneseFont != null)
        {
            return runtimeJapaneseFont;
        }

        string[] candidates =
        {
            "Yu Gothic UI",
            "Yu Gothic",
            "Meiryo UI",
            "Meiryo",
            "Noto Sans CJK JP",
            "Noto Sans JP"
        };

        runtimeJapaneseFont = Font.CreateDynamicFontFromOSFont(candidates, 24);
        if (runtimeJapaneseFont == null)
        {
            runtimeJapaneseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return runtimeJapaneseFont;
    }
}
