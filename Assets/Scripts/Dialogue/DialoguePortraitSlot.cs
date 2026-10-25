using UnityEngine;
using UnityEngine.UI;

/// <summary>One persistent left/right portrait. Artwork changes do not restart an entrance.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class DialoguePortraitSlot : MonoBehaviour
{
    [SerializeField] private DialoguePortraitSide side;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject placeholder;

    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 home;
    private bool initialized;
    private float elapsed;
    private float duration;
    private float distance;
    private Graphic[] placeholderGraphics;
    private Color[] placeholderColors;

    public DialogueSpeakerDefinition DisplayedSpeaker { get; private set; }
    public bool HasCharacter { get; private set; }
    public bool IsEntering => HasCharacter && elapsed < duration;
    public int EntranceCount { get; private set; }
    public DialoguePortraitSide Side => side;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        home = rect.anchoredPosition;
        placeholderGraphics = placeholder != null ? placeholder.GetComponentsInChildren<Graphic>(true) : new Graphic[0];
        placeholderColors = new Color[placeholderGraphics.Length];
        for (int i = 0; i < placeholderGraphics.Length; i++) placeholderColors[i] = placeholderGraphics[i].color;
        initialized = true;
    }

    public void Show(DialogueSpeakerDefinition speaker, Sprite portrait, float seconds, float slideDistance)
    {
        Initialize();
        bool entering = !HasCharacter || DisplayedSpeaker != speaker;
        DisplayedSpeaker = speaker;
        HasCharacter = true;
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.preserveAspect = true;
            portraitImage.gameObject.SetActive(portrait != null);
        }
        if (placeholder != null) placeholder.SetActive(portrait == null);

        if (entering)
        {
            elapsed = 0f;
            duration = Mathf.Max(0f, seconds);
            distance = Mathf.Max(0f, slideDistance);
            EntranceCount++;
        }
        ApplyEntrance();
    }

    public void SetSpeaking(bool speaking, float inactiveBrightness)
    {
        Initialize();
        float brightness = speaking ? 1f : Mathf.Clamp01(inactiveBrightness);
        if (portraitImage != null) portraitImage.color = new Color(brightness, brightness, brightness, 1f);
        // The placeholder is deliberately simple UI geometry, not delivered character art.
        for (int i = 0; i < placeholderGraphics.Length; i++)
        {
            Color color = placeholderColors[i];
            placeholderGraphics[i].color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }
    }

    public void AdvanceVisuals(float unscaledDeltaTime)
    {
        if (!HasCharacter) return;
        elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, unscaledDeltaTime));
        ApplyEntrance();
    }

    public void HideImmediate()
    {
        Initialize();
        HasCharacter = false;
        DisplayedSpeaker = null;
        elapsed = duration = distance = 0f;
        rect.anchoredPosition = home;
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.gameObject.SetActive(false);
        }
        if (placeholder != null) placeholder.SetActive(false);
    }

    private void ApplyEntrance()
    {
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
        float ease = 1f - Mathf.Pow(1f - progress, 3f);
        float direction = side == DialoguePortraitSide.Left ? -1f : 1f;
        rect.anchoredPosition = home + Vector2.right * (direction * distance * (1f - ease));
        group.alpha = progress;
        group.interactable = group.blocksRaycasts = false;
    }
}
