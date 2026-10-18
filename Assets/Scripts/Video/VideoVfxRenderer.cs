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
    { public GameObject root;public SpriteRenderer sprite,glow;public VideoArtLibrary.Motion motion;public float age,duration;public bool permitsGlow,fresh,fade;public Transform follow;public Vector2 offset,drift;public Color tint,glowTint; }
    private readonly List<Effect> pool=new();
    public bool HybridGlow { get=>hybridGlow;set=>hybridGlow=value; }
    public int ActiveCount { get { int n=0;foreach(var e in pool) if(e.root.activeSelf) n++;return n; } }
    public int EmissionCount { get; private set; }
    public void Emit(string kind,Vector2 position,float scale=1,int facing=1,float delay=0)
    { EmitEffect(kind,position,scale,facing,delay); }
    private Effect EmitEffect(string kind,Vector2 position,float scale,int facing,float delay)
    {
        var motion=library.Find("Fx_"+kind);if(motion==null) return null;
        Effect effect=Obtain();
        effect.motion=motion;effect.age=-delay;effect.duration=motion.TotalFrames/60f;effect.permitsGlow=effect.fresh=true;
        effect.follow=null;effect.drift=Vector2.zero;effect.fade=false;effect.sprite.sortingOrder=30;effect.glow.sortingOrder=29;
        effect.root.transform.position=position;effect.root.transform.localScale=new Vector3(scale*facing,scale,1);
        effect.root.SetActive(true);effect.sprite.color=new Color(1,1,1,intensity);
        effect.glow.transform.localScale=Vector3.one*1.08f;
        effect.glow.color=kind=="ElectricKick"?new Color(.6f,.9f,1,.16f*intensity):new Color(1,.8f,.55f,.16f*intensity);
        effect.sprite.enabled=delay<=0;effect.glow.enabled=hybridGlow&&delay<=0;
        effect.sprite.sprite=effect.glow.sprite=motion.frames[0];EmissionCount++;
        return effect;
    }
    public void EmitFollowing(string kind,Transform target,Vector2 offset,float scale,float delay)
    {
        if(target==null) return;
        var effect=EmitEffect(kind,(Vector2)target.position+offset,scale,1,delay);
        if(effect!=null) { effect.follow=target;effect.offset=offset; }
    }
    public void EmitEyeTrail(SpriteRenderer player,int facing)
    {
        var eyes=library.EyesFor(player.sprite);if(eyes==null) return;
        var effect=Obtain();effect.motion=new VideoArtLibrary.Motion{name="EyeTrail",frames=new[]{eyes},durations=new[]{8}};
        effect.age=0;effect.duration=8/60f;effect.permitsGlow=effect.fresh=effect.fade=true;
        effect.follow=null;effect.drift=Vector2.right*(-facing*1.8f);
        effect.tint=new Color(.75f,.95f,1,.8f);effect.glowTint=new Color(.65f,.9f,1,.4f);
        effect.root.transform.position=player.transform.position;effect.root.transform.localScale=new Vector3(facing,1,1);
        effect.sprite.sprite=effect.glow.sprite=eyes;effect.sprite.color=effect.tint;
        effect.glow.color=effect.glowTint;effect.glow.transform.localScale=Vector3.one;
        effect.sprite.sortingOrder=1020;effect.glow.sortingOrder=1019;
        effect.sprite.enabled=true;effect.glow.enabled=hybridGlow;effect.root.SetActive(true);EmissionCount++;
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
        e.follow=null;e.drift=Vector2.zero;e.fade=false;e.sprite.sortingOrder=9;e.glow.sortingOrder=8;
        e.root.transform.position=player.transform.position;e.root.transform.localScale=new Vector3(facing,1,1);
        e.sprite.sprite=player.sprite;e.sprite.color=new Color(.12f,.14f,.22f,.24f*intensity);e.sprite.enabled=true;e.glow.enabled=false;
        e.root.SetActive(true);EmissionCount++;
    }
    public void EmitTransitSilhouette(SpriteRenderer player,int facing,Vector2 position,Vector2 scale,float opacity)
    {
        var e=Obtain();e.motion=new VideoArtLibrary.Motion{name="TransitSilhouette",frames=new[]{player.sprite},durations=new[]{10}};
        e.age=0;e.duration=10/60f;e.permitsGlow=false;e.fresh=e.fade=true;e.follow=null;e.drift=Vector2.zero;
        e.tint=new Color(0,0,0,opacity*intensity);e.glowTint=Color.clear;
        e.root.transform.position=position;e.root.transform.localScale=new Vector3(scale.x*facing,scale.y,1);
        e.sprite.sprite=player.sprite;e.sprite.color=e.tint;e.sprite.sortingOrder=12;e.glow.sortingOrder=11;
        e.sprite.enabled=true;e.glow.enabled=false;e.root.SetActive(true);EmissionCount++;
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
            if(e.follow!=null) e.root.transform.position=(Vector2)e.follow.position+e.offset;
            else e.root.transform.position+=(Vector3)(e.drift*realSeconds);
            if(e.fade)
            {
                float opacity=Mathf.Clamp01(1-e.age/e.duration);
                e.sprite.color=new Color(e.tint.r,e.tint.g,e.tint.b,e.tint.a*opacity);
                e.glow.color=new Color(e.glowTint.r,e.glowTint.g,e.glowTint.b,e.glowTint.a*opacity);
            }
            if(e.age>=0)
            { e.sprite.sprite=e.glow.sprite=e.motion.At(Mathf.FloorToInt(e.age*60)); }
        }
    }
    public void ResetEffects()
    { foreach(var e in pool) if(e.root!=null) e.root.SetActive(false);EmissionCount=0; }
    private void OnDisable()=>ResetEffects();
}
