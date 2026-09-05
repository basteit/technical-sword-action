using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private Text promptLabel;

    private void Awake()
    {
        if (root == null)
        {
            root = GetComponent<CanvasGroup>();
        }

        ApplyFont();

        Hide();
    }

    public void Show(string message)
    {
        ApplyFont();
        if (promptLabel != null)
        {
            promptLabel.text = message ?? string.Empty;
        }

        SetVisible(true);
    }

    public void Hide()
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
        root.interactable = false;
        root.blocksRaycasts = false;
    }

    private void ApplyFont()
    {
        Font font = DialogueView.ResolveJapaneseFont();
        if (promptLabel != null && font != null)
        {
            promptLabel.font = font;
        }
    }
}
