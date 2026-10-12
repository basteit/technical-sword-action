using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable pixel sprites; optional additive glow and small pixel particles use real time.</summary>
public sealed class VideoVfxRenderer : MonoBehaviour
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private bool hybridGlow=true;
    [SerializeField,Range(0,1)] private float intensity=.8f;
    private sealed class Effect
    { public GameObject root;public SpriteRenderer sprite,glow;public VideoArtLibrary.Motion motion;public float age,duration;public bool permitsGlow,fresh; }
    private readonly List<Effect> pool=new();
    public bool HybridGlow { get=>hybridGlow;set=>hybridGlow=value; }
    public int ActiveCount { get { int n=0;foreach(var e in pool) if(e.root.activeSelf) n++;return n; } }
    public int EmissionCount { get; private set; }
    public void Emit(string kind,Vector2 position,float scale=1,int facing=1,float delay=0)
    {
        var motion=library.Find("Fx_"+kind);if(motion==null) return;
        Effect effect=Obtain();
        effect.motion=motion;effect.age=-delay;effect.duration=motion.TotalFrames/60f;effect.permitsGlow=effect.fresh=true;
        effect.root.transform.position=position;effect.root.transform.localScale=new Vector3(scale*facing,scale,1);
        effect.root.SetActive(true);effect.sprite.color=new Color(1,1,1,intensity);
        effect.glow.transform.localScale=Vector3.one*1.08f;
        effect.glow.color=new Color(1,.8f,.55f,.16f*intensity);
        effect.sprite.enabled=delay<=0;effect.glow.enabled=hybridGlow&&delay<=0;
        effect.sprite.sprite=effect.glow.sprite=motion.frames[0];EmissionCount++;
    }
    private Effect Obtain()
    {
        Effect effect=pool.Find(e=>!e.root.activeSelf);
        if(effect==null)
        {
            effect=new Effect();effect.root=new GameObject("PooledVideoVfx");effect.root.transform.SetParent(transform);
            effect.sprite=effect.root.AddComponent<SpriteRenderer>();effect.sprite.sortingOrder=30;
            var glow=new GameObject("OptionalGlow");glow.transform.SetParent(effect.root.transform,false);
            effect.glow=glow.AddComponent<SpriteRenderer>();effect.glow.sortingOrder=29;effect.glow.sharedMaterial=glowMaterial;
            pool.Add(effect);
        }
        return effect;
    }
    public void EmitAfterimage(SpriteRenderer player,int facing)
    {
        var e=Obtain();e.motion=new VideoArtLibrary.Motion{name="Afterimage",frames=new[]{player.sprite},durations=new[]{6}};
        e.age=0;e.duration=.1f;e.permitsGlow=false;e.fresh=true;
        e.root.transform.position=player.transform.position;e.root.transform.localScale=new Vector3(facing,1,1);
        e.sprite.sprite=player.sprite;e.sprite.color=new Color(.12f,.14f,.22f,.24f*intensity);e.sprite.enabled=true;e.glow.enabled=false;
        e.root.SetActive(true);EmissionCount++;
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if(CombatTimeController.IsPaused) return;
        foreach(var e in pool)
        {
            if(!e.root.activeSelf) continue;
            if(e.fresh) { e.fresh=false;continue; }
            e.age+=realSeconds;
            if(e.age>=e.duration) { e.root.SetActive(false);continue; }
            e.sprite.enabled=e.age>=0;e.glow.enabled=e.permitsGlow&&hybridGlow&&e.age>=0&&e.age<e.duration*.6f;
            if(e.age>=0)
            { e.sprite.sprite=e.glow.sprite=e.motion.At(Mathf.FloorToInt(e.age*60)); }
        }
    }
    public void ResetEffects()
    { foreach(var e in pool) if(e.root!=null) e.root.SetActive(false);EmissionCount=0; }
    private void OnDisable()=>ResetEffects();
}
