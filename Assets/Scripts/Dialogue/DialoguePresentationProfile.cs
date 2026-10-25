using UnityEngine;

[CreateAssetMenu(menuName = "Keraunos/Dialogue/Presentation Profile", fileName = "DialoguePresentation")]
public sealed class DialoguePresentationProfile : ScriptableObject
{
    [Header("Artwork (optional until supplied)")]
    public Sprite boxSprite;
    public Color boxTint = Color.white;
    public Color placeholderBoxColor = new(0.055f, 0.075f, 0.12f, 0.96f);

    [Header("Layout (640 x 360 reference pixels)")]
    public Vector2 boxSize = new(608f, 104f);
    public Vector2 boxPosition = new(0f, 12f);

    [Header("Entrance (unscaled UI seconds)")]
    [Min(0f)] public float fadeInSeconds = 0.35f;
    [Min(0f)] public float portraitSlideSeconds = 0.3f;
    [Min(0f)] public float portraitSlideDistance = 80f;
    [Range(0f, 1f)] public float inactivePortraitBrightness = 0.65f;
}
