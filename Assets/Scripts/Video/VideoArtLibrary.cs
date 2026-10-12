using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Technical Sword Action/Video Art Library")]
public sealed class VideoArtLibrary : ScriptableObject
{
    [Serializable] public sealed class Motion
    {
        public string name;
        public Sprite[] frames;
        public int[] durations;
        public int hitFrame,branchFrame,chargeFrame;
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
}
