using System;
using UnityEngine;

public enum CombatFeedbackKind { LightHit, HeavyHit, Counter, Parry, JustParry, PlayerHit, Defeat, ParryMiss }

[CreateAssetMenu(menuName = "Technical Sword Action/Combat Feedback Profile")]
public sealed class CombatFeedbackProfile : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public CombatFeedbackKind kind;
        [Min(0)] public int hitstopFrames;
        [Min(0)] public float shakePixels = 3;
        [Min(0)] public float shakeSeconds = .08f;
        [Min(1)] public int lifetimeFrames = 10;
        [Min(.01f)] public float glyphSize = .25f;
        [Min(0)] public int flashFrames = 3;
        public Color tint = Color.white;
        public AudioClip clip;
        [Range(0, 1)] public float volume = .6f;
        [Range(.5f, 2)] public float pitch = 1;
    }
    public Entry[] entries = new Entry[0];
    public Entry Find(CombatFeedbackKind kind) => entries == null ? null : Array.Find(entries, x => x != null && x.kind == kind);
    public bool IsValid
    {
        get
        {
            foreach (CombatFeedbackKind kind in Enum.GetValues(typeof(CombatFeedbackKind)))
            {
                var entry = Find(kind);
                if (entry == null || entry.hitstopFrames < 0 || entry.lifetimeFrames <= 0 ||
                    !float.IsFinite(entry.glyphSize) || entry.glyphSize <= 0 ||
                    !float.IsFinite(entry.shakePixels) || entry.shakePixels < 0 || entry.flashFrames < 0) return false;
                int count = 0;
                foreach (var value in entries) if (value != null && value.kind == kind) count++;
                if (count != 1) return false;
            }
            return true;
        }
    }
}
