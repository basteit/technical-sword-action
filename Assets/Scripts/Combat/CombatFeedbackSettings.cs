using UnityEngine;

/// <summary>Presentation only: never changes combat timing, damage, telegraphs or essential glyphs.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Combat Feedback Settings")]
public sealed class CombatFeedbackSettings : ScriptableObject
{
    [Range(0, 1)] public float shake = 1;
    [Range(0, 1)] public float flash = 1;
    [Range(0, 1)] public float vfx = 1;
    [Range(0, 1)] public float masterVolume = 1;
    [Range(0, 1)] public float sfxVolume = 1;
}
