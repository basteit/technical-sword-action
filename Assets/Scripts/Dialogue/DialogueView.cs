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

    [Header("Typography")]
    [SerializeField] private Font dialogueFont;
    [SerializeField] private string continueText = "E / B / A / Enter  次へ";
    [SerializeField] private string closeText = "E / B / A / Enter  閉じる";

    private static Font runtimeJapaneseFont;

    public bool IsVisible => root != null && root.alpha > 0.001f;

    private void Awake()
    {
        ResolveReferences();
        ApplyFont();
        HideImmediate();
    }

    public void Show(DialogueLine line, bool isLastLine)
    {
        ResolveReferences();
        ApplyFont();

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

        if (portraitImage != null)
        {
            Sprite portrait = line?.Portrait;
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }

        if (continueLabel != null)
        {
            continueLabel.text = isLastLine ? closeText : continueText;
        }

        SetVisible(true);
    }

    public void HideImmediate()
    {
        SetVisible(false);
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
