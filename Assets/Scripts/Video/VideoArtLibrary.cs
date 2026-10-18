using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Technical Sword Action/Video Art Library")]
public sealed class VideoArtLibrary : ScriptableObject
{
    [Serializable] public sealed class Motion
    {
        public string name;
        public bool provisional;
        public bool includesSlashTrail;
        public bool includesCounterTrail;
        public Vector2 counterContactOffset;
        public bool hasTrailHitbox;
        public Vector2 hitboxMin,hitboxMax;
        public bool hasParryContact;
        public Vector2 parryContactOffset;
        public Sprite[] frames;
        public Sprite[] eyeFrames;
        public int[] durations;
        public int hitFrame,branchFrame,chargeFrame,releaseFrame,landingFrame,recoveryFrame;
        public int TotalFrames { get { int n=0; foreach(int duration in durations) n+=duration; return n; } }
        public Sprite At(int gameFrame, bool loop = false)
        {
            if (loop && TotalFrames > 0) gameFrame %= TotalFrames;
            for (int i=0; i<frames.Length; i++) { if (gameFrame < durations[i]) return frames[i]; gameFrame-=durations[i]; }
            return frames[frames.Length-1];
        }
    }
    public bool playerArtworkIsRough = true;
    public bool fourStageArtworkIsRough = true;
    public Sprite background;
    public Motion[] motions;
    public Motion Find(string name) => Array.Find(motions,m => m.name == name);
    public Motion FindParrySuccess(ParryReactionWeight weight, out int startFrame)
    {
        startFrame = 0;
        var motion = Find(weight == ParryReactionWeight.Heavy ? "ParrySuccessHeavy" : "ParrySuccessLight") ?? Find("ParrySuccessLight");
        if (motion != null) return motion;
        // Existing rough art combines two ready cels with the success motion.
        motion = Find("ParryDeflect");
        if (motion != null)
            for (int i = 0; i < Math.Min(2, motion.durations.Length); i++) startFrame += motion.durations[i];
        return motion;
    }
    public Sprite EyesFor(Sprite source)
    {
        if(source==null||motions==null) return null;
        foreach(var motion in motions)
        {
            if(motion.eyeFrames==null) continue;
            for(int i=0;i<motion.frames.Length&&i<motion.eyeFrames.Length;i++)
                if(motion.frames[i]==source) return motion.eyeFrames[i];
        }
        return null;
    }
}
