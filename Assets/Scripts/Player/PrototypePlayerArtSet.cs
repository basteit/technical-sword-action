using System;
using UnityEngine;

/// <summary>A prototype-only artwork snapshot. It does not own combat timings or hitboxes.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Prototype Player Art Set")]
public sealed class PrototypePlayerArtSet : ScriptableObject
{
    public enum ArtworkStatus { Received, SubmittedProvisional, Rough, Substitute }

    [Serializable] public sealed class Entry
    {
        public string action;
        public string source;
        public ArtworkStatus status;
        public AnimationClip clip;
        public Sprite contactSprite;
        public float visualHitFrame;
    }

    [Serializable] public sealed class DisplayOverride
    {
        public Sprite source;
        public Sprite display;
    }

    public AnimatorOverrideController controller;
    public Entry[] entries = Array.Empty<Entry>();
    [Tooltip("Prototype-only drawing crops. Original sprites remain the Animator/event source and are never edited.")]
    public DisplayOverride[] displayOverrides = Array.Empty<DisplayOverride>();
    [Tooltip("Sprites authored with their pivot on the bottom of the character instead of the common (32,38) origin.")]
    public Sprite[] feetPivotSprites = Array.Empty<Sprite>();
    [Tooltip("Common source art has its feet one unit below its pivot. Only the visual child is offset.")]
    public float commonFeetBelowPivot = 1f;
    public float pixelsPerUnit = 32f;

    public Entry Find(string action) => Array.Find(entries, entry => entry.action == action);
    public bool IsFeetPivot(Sprite sprite) => Array.IndexOf(feetPivotSprites, sprite) >= 0;
    public Sprite DisplaySpriteFor(Sprite sprite)
    {
        if (sprite == null || displayOverrides == null) return sprite;
        foreach (var replacement in displayOverrides)
            if (replacement != null && replacement.source == sprite && replacement.display != null)
                return replacement.display;
        return sprite;
    }
}
