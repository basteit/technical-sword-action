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

    public AnimatorOverrideController controller;
    public Entry[] entries = Array.Empty<Entry>();
    [Tooltip("Sprites authored with their pivot on the bottom of the character instead of the common (32,38) origin.")]
    public Sprite[] feetPivotSprites = Array.Empty<Sprite>();
    [Tooltip("Common source art has its feet one unit below its pivot. Only the visual child is offset.")]
    public float commonFeetBelowPivot = 1f;
    public float pixelsPerUnit = 32f;

    public Entry Find(string action) => Array.Find(entries, entry => entry.action == action);
    public bool IsFeetPivot(Sprite sprite) => Array.IndexOf(feetPivotSprites, sprite) >= 0;
}
