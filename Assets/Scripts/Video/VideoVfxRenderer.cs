using System.Collections.Generic;
using UnityEngine;

/// <summary>Reusable pixel sprites; optional additive glow and small pixel particles use real time.</summary>
public sealed class VideoVfxRenderer : MonoBehaviour
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private bool hybridGlow=true;
    [SerializeField,Range(0,1)] private float intensity=.8f;
    [SerializeField] private bool readableImpacts=true;
    [SerializeField] private int impactSortingOrder=30;
    private VideoArtLibrary.Motion electricBurst,parryBurst;
    private readonly List<Sprite> generatedSprites=new();
    private readonly List<Texture2D> generatedTextures=new();
    private sealed class Effect
    { public GameObject root;public SpriteRenderer sprite,glow;public VideoArtLibrary.Motion motion;public float age,duration,glowSeconds;public bool permitsGlow,fresh,fade;public Transform follow;public Vector2 offset,drift;public Color tint,glowTint;public Vector3 position,worldScale;public Quaternion rotation; }
    private readonly List<Effect> pool=new();
    public bool ReadableImpacts { get=>readableImpacts;set=>readableImpacts=value; }
    public bool HybridGlow { get=>hybridGlow;set=>hybridGlow=value; }
    public int ActiveCount { get { int n=0;foreach(var e in pool) if(e.root.activeSelf) n++;return n; } }
    public int EmissionCount { get; private set; }
    public void Emit(string kind,Vector2 position,float scale=1,int facing=1,float delay=0)
    {
        var effect=EmitEffect(kind,position,scale,facing,delay);
        if(effect==null||!readableImpacts) return;
        if(kind=="ElectricKick")
        {
            // Attack effects stay in front of the struck enemy.
            effect.sprite.sortingOrder=impactSortingOrder;effect.glow.sortingOrder=impactSortingOrder-1;
            effect.sprite.color=new Color(1,1,1,intensity*.65f);
            EmitImpactBurst(true,position,scale,facing,delay);
        }
        else if(kind=="Parry")
        {
            effect.worldScale*=.75f;ApplyPose(effect);
            EmitImpactBurst(false,position,scale,facing,delay);
        }
    }
    private Effect EmitEffect(string kind,Vector2 position,float scale,int facing,float delay)
    {
        var motion=library.Find("Fx_"+kind);if(motion==null) return null;
        Effect effect=Obtain();
        effect.glowSeconds=readableImpacts&&(kind=="ElectricKick"||kind=="Parry")?2/60f:motion.TotalFrames/60f*.6f;
        effect.motion=motion;effect.age=-delay;effect.duration=motion.TotalFrames/60f;effect.permitsGlow=effect.fresh=true;
        effect.follow=null;effect.drift=Vector2.zero;effect.fade=false;effect.sprite.sortingOrder=30;effect.glow.sortingOrder=29;
        SetPose(effect,position,new Vector3(scale*facing,scale,1),Quaternion.identity);
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
        var effect=Obtain();effect.motion=new VideoArtLibrary.Motion{name="EyeTrail",frames=new[]{eyes},durations=new[]{readableImpacts?6:8}};
        effect.age=0;effect.duration=(readableImpacts?6:8)/60f;effect.glowSeconds=effect.duration*.6f;effect.permitsGlow=effect.fresh=effect.fade=true;
        effect.follow=null;effect.drift=Vector2.right*(-facing*1.8f);
        effect.tint=new Color(.75f,.95f,1,readableImpacts?.45f:.8f);effect.glowTint=new Color(.65f,.9f,1,readableImpacts?.18f:.4f);
        SetSpritePose(effect,player,player.transform.position,Vector2.one);
        effect.sprite.sprite=effect.glow.sprite=eyes;effect.sprite.color=effect.tint;
        effect.glow.color=effect.glowTint;effect.glow.transform.localScale=Vector3.one;
        effect.sprite.sortingOrder=1020;effect.glow.sortingOrder=1019;
        effect.sprite.enabled=true;effect.glow.enabled=hybridGlow;effect.root.SetActive(true);EmissionCount++;
    }
    private void EmitImpactBurst(bool electric,Vector2 position,float scale,int facing,float delay)
    {
        var motion=electric?(electricBurst??=CreateImpactBurst(true)):(parryBurst??=CreateImpactBurst(false));
        var effect=Obtain();effect.motion=motion;effect.age=-delay;effect.duration=motion.TotalFrames/60f;
        effect.glowSeconds=2/60f;effect.permitsGlow=effect.fresh=true;
        effect.fade=false;effect.follow=null;effect.drift=Vector2.zero;
        SetPose(effect,position,new Vector3(scale*facing,scale,1),Quaternion.identity);
        effect.sprite.sortingOrder=electric?impactSortingOrder+1:31;effect.glow.sortingOrder=electric?impactSortingOrder:30;
        effect.sprite.sprite=effect.glow.sprite=motion.frames[0];effect.sprite.color=new Color(1,1,1,intensity);
        effect.glow.color=electric?new Color(.5f,.85f,1,.16f*intensity):new Color(1,.88f,.6f,.12f*intensity);
        effect.glow.transform.localScale=Vector3.one;
        effect.sprite.enabled=delay<=0;effect.glow.enabled=hybridGlow&&delay<=0;effect.root.SetActive(true);EmissionCount++;
    }
    private VideoArtLibrary.Motion CreateImpactBurst(bool electric)
    {
        int size=electric?128:96,count=electric?6:4;
        float[] angles=electric?new float[]{-160,-116,-72,-28,14,56,106,150}:new float[]{-70,-28,8,38,72,130,168};
        var frames=new Sprite[count];
        for(int frame=0;frame<count;frame++)
        {
            var pixels=new Color32[size*size];
            for(int ray=0;ray<angles.Length;ray++)
            {
                float angle=angles[ray]*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                var normal=new Vector2(-direction.y,direction.x);var center=Vector2.one*(size/2);
                float inner=electric?13+frame*4:3+frame*6;
                float outer=electric?30+frame*3+(ray*7)%9:18+frame*5+(ray%3)*3;
                Vector2 a=center+direction*inner,b=center+direction*Mathf.Lerp(inner,outer,.35f)+normal*((ray%2==0?1:-1)*(electric?5:2));
                Vector2 c=center+direction*Mathf.Lerp(inner,outer,.65f)-normal*(electric?3:1),d=center+direction*outer;
                Color32 edge=electric?new Color32(66,83,184,220):new Color32(113,83,52,210);
                Color32 core=electric?new Color32(172,242,255,255):new Color32(255,241,198,255);
                DrawPixelLine(pixels,size,a,b,edge,3);DrawPixelLine(pixels,size,b,c,edge,3);DrawPixelLine(pixels,size,c,d,edge,3);
                DrawPixelLine(pixels,size,a,b,core,1);DrawPixelLine(pixels,size,b,c,core,1);DrawPixelLine(pixels,size,c,d,core,1);
                if(frame<2)DrawPixelLine(pixels,size,b,c,new Color32(244,253,255,255),1);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            texture.SetPixels32(pixels);texture.Apply(false,false);generatedTextures.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,32,0,SpriteMeshType.FullRect);
            sprite.name=$"Video_{(electric?"ElectricDischarge":"DeflectSparks")}_{frame+1:00}";sprite.hideFlags=HideFlags.DontSave;
            generatedSprites.Add(sprite);frames[frame]=sprite;
        }
        return new VideoArtLibrary.Motion{name=electric?"ElectricDischarge":"DeflectSparks",frames=frames,durations=electric?new[]{1,1,2,2,3,3}:new[]{1,1,2,3}};
    }
    private static void DrawPixelLine(Color32[] pixels,int size,Vector2 from,Vector2 to,Color32 color,int width)
    {
        int x=Mathf.RoundToInt(from.x),y=Mathf.RoundToInt(from.y),tx=Mathf.RoundToInt(to.x),ty=Mathf.RoundToInt(to.y);
        int dx=Mathf.Abs(tx-x),dy=-Mathf.Abs(ty-y),sx=x<tx?1:-1,sy=y<ty?1:-1,error=dx+dy;
        while(true)
        {
            for(int oy=-width/2;oy<=width/2;oy++)for(int ox=-width/2;ox<=width/2;ox++)
            {int px=x+ox,py=y+oy;if(px>=0&&py>=0&&px<size&&py<size)pixels[py*size+px]=color;}
            if(x==tx&&y==ty)break;
            int twice=2*error;if(twice>=dy){error+=dy;x+=sx;}if(twice<=dx){error+=dx;y+=sy;}
        }
    }
    private void OnDestroy()
    {
        foreach(var sprite in generatedSprites)if(sprite!=null)Destroy(sprite);
        foreach(var texture in generatedTextures)if(texture!=null)Destroy(texture);
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
        SetSpritePose(e,player,player.transform.position,Vector2.one);
        e.sprite.sprite=player.sprite;e.sprite.color=new Color(.12f,.14f,.22f,.24f*intensity);e.sprite.enabled=true;e.glow.enabled=false;
        e.root.SetActive(true);EmissionCount++;
    }
    public void EmitTransitSilhouette(SpriteRenderer player,int facing,Vector2 position,Vector2 scale,float opacity)
    {
        var e=Obtain();e.motion=new VideoArtLibrary.Motion{name="TransitSilhouette",frames=new[]{player.sprite},durations=new[]{10}};
        e.age=0;e.duration=10/60f;e.permitsGlow=false;e.fresh=e.fade=true;e.follow=null;e.drift=Vector2.zero;
        e.tint=new Color(0,0,0,opacity*intensity);e.glowTint=Color.clear;
        SetSpritePose(e,player,position,scale);
        e.sprite.sprite=player.sprite;e.sprite.color=e.tint;e.sprite.sortingOrder=12;e.glow.sortingOrder=11;
        e.sprite.enabled=true;e.glow.enabled=false;e.root.SetActive(true);EmissionCount++;
    }
    private static void SetPose(Effect effect,Vector3 position,Vector3 scale,Quaternion rotation)
    {
        effect.position=position;effect.worldScale=scale;effect.rotation=rotation;
        effect.sprite.flipX=effect.glow.flipX=effect.sprite.flipY=effect.glow.flipY=false;
        ApplyPose(effect);
    }
    private static void SetSpritePose(Effect effect,SpriteRenderer source,Vector3 position,Vector2 scale)
    {
        SetPose(effect,position,Vector3.Scale(source.transform.lossyScale,new Vector3(scale.x,scale.y,1)),source.transform.rotation);
        effect.sprite.flipX=effect.glow.flipX=source.flipX;
        effect.sprite.flipY=effect.glow.flipY=source.flipY;
    }
    private static void ApplyPose(Effect effect)
    {
        // The pool can live under a moving, mirrored player. Keep emitted art
        // in its captured world pose rather than inheriting that transform twice.
        var parent=effect.root.transform.parent;
        var scale=parent!=null?parent.lossyScale:Vector3.one;
        effect.root.transform.SetPositionAndRotation(effect.position,effect.rotation);
        effect.root.transform.localScale=new Vector3(
            effect.worldScale.x/(Mathf.Abs(scale.x)>.00001f?scale.x:1),
            effect.worldScale.y/(Mathf.Abs(scale.y)>.00001f?scale.y:1),
            effect.worldScale.z/(Mathf.Abs(scale.z)>.00001f?scale.z:1));
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if(CombatTimeController.IsPaused) return;
        foreach(var e in pool)
        {
            if(!e.root.activeSelf) continue;
            if(e.fresh)
            {
                e.fresh=false;
                if(e.follow!=null) e.position=(Vector2)e.follow.position+e.offset;
                ApplyPose(e);continue;
            }
            e.age+=realSeconds;
            if(e.age>=e.duration) { e.root.SetActive(false);continue; }
            e.sprite.enabled=e.age>=0;e.glow.enabled=e.permitsGlow&&hybridGlow&&e.age>=0&&e.age<e.glowSeconds;
            if(e.follow!=null) e.position=(Vector2)e.follow.position+e.offset;
            else e.position+=(Vector3)(e.drift*realSeconds);
            ApplyPose(e);
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
