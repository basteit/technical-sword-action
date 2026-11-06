using System;
using UnityEngine;

public enum PrototypeSfxCue
{
    Footstep, Jump, Land, Dash, Swing1, Swing2, Swing3, Swing4, Sheathe,
    ParryReady, Parry, JustParry, ParryMiss, LightHit, HeavyHit,
    CounterTransfer, CounterCharge, CounterRelease, CounterHit,
    PlayerHit, PlayerDeath, EnemyWindup, EnemyDanger, EnemyAttack, EnemyDefeat,
    HealStart, HealApply, SpecialCast, SpecialHit, Interact
}

/// <summary>Replaceable, presentation-only licensed sound palette. See StreamingAssets/AudioCredits.txt.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Prototype SFX Profile")]
public sealed class PrototypeSfxProfile : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public PrototypeSfxCue cue;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float volume = .4f;
        [Range(.5f, 2)] public float pitch = 1;
        [Range(0, .1f)] public float pitchVariation = .025f;
        [Range(0, 100)] public int priority = 30;
    }

    public Entry[] entries = Array.Empty<Entry>();
    [Min(.1f)] public float footstepDistance = 1.3f;
    [Range(0, 6)] public int swingLeadFrames = 2;
    [Range(0, 1)] public float sheatheRecoveryProgress = .65f;
    public Entry Find(PrototypeSfxCue cue) => Array.Find(entries, e => e != null && e.cue == cue);
    public bool IsValid
    {
        get
        {
            if (entries == null || !float.IsFinite(footstepDistance) || footstepDistance < .1f ||
                swingLeadFrames < 0 || swingLeadFrames > 6 || !float.IsFinite(sheatheRecoveryProgress) ||
                sheatheRecoveryProgress < 0 || sheatheRecoveryProgress > 1) return false;
            foreach (PrototypeSfxCue cue in Enum.GetValues(typeof(PrototypeSfxCue)))
            {
                int count = 0;
                foreach (var entry in entries) if (entry != null && entry.cue == cue) count++;
                var value = Find(cue);
                if (count != 1 || value.clips == null || value.clips.Length == 0 ||
                    !float.IsFinite(value.volume) || value.volume < 0 || value.volume > 1 ||
                    !float.IsFinite(value.pitch) || value.pitch < .5f || value.pitch > 2 ||
                    !float.IsFinite(value.pitchVariation) || value.pitchVariation < 0 || value.pitchVariation > .1f ||
                    value.priority < 0 || value.priority > 100) return false;
                foreach (var clip in value.clips) if (clip == null) return false;
            }
            return true;
        }
    }
}
