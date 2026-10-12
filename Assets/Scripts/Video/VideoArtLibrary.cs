using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Technical Sword Action/Video Art Library")]
public sealed class VideoArtLibrary : ScriptableObject
{
    [Serializable] public sealed class Motion
    {
        public string name;
        public Sprite[] frames;
        public Sprite[] eyeFrames;
        public int[] durations;
        public int hitFrame,branchFrame,chargeFrame,releaseFrame,landingFrame;
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
