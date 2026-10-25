using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Camera-local sprite lighting; the source artwork and its yellow spark stay intact.</summary>
public sealed class VideoParryBacklight : MonoBehaviour
{
    private Camera captureCamera;
    private SpriteRenderer player,enemy,litPlayer,litEnemy,backdrop,bloom;
    private Material material;
    private bool ownsMaterial,haloAllowed,keepGold;
    private Sprite white;
    private Vector2 contact;
    private Damageable2D target;
    private MaterialPropertyBlock properties;
    private readonly CombatLightSprites lightSprites=new();
    private static readonly int LightWorld=Shader.PropertyToID("_LightWorld"),Bloom=Shader.PropertyToID("_Bloom"),KeepGold=Shader.PropertyToID("_KeepGold");
    public bool IsVisible {get;private set;}
    public float Intensity {get;set;}=1;
    public bool IsHaloVisible=>IsVisible&&haloAllowed;
    public Damageable2D Target=>IsVisible?target:null;
    private void Awake()=>properties=new MaterialPropertyBlock();
    public void Configure(Camera camera,SpriteRenderer source,Material shared)
    { captureCamera=camera;player=source;material=shared; }
    private void OnEnable()
    { RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera; }
    public void Begin(Damageable2D source,Vector2 spark)
    {
        ResetLight();target=source;contact=spark;
        enemy=source!=null?source.GetComponentInChildren<SpriteRenderer>():null;
        if(captureCamera==null||player==null||enemy==null)return;
        Ensure();
        litEnemy.transform.SetParent(enemy.transform,false);litEnemy.gameObject.layer=enemy.gameObject.layer;
    }
    public void Sample(bool visible,bool halo,bool preserveGold)
    {
        haloAllowed=halo;keepGold=preserveGold;
        IsVisible=visible&&target!=null&&target.IsAlive&&target.gameObject.activeInHierarchy&&player!=null&&enemy!=null;
        if(!IsVisible)ResetLight();
    }
    public void ResetLight()
    { IsVisible=false;target=null;enemy=null;SetRenderers(false); }
    private void Ensure()
    {
        if(material==null)
        {
            material=new Material(Shader.Find("TechnicalSwordAction/VideoParryBacklight")){hideFlags=HideFlags.DontSave};
            ownsMaterial=true;
        }
        if(white==null)
        {
            var texture=Texture2D.whiteTexture;
            white=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,texture.width);
            white.hideFlags=HideFlags.DontSave;white.name="ParryLightPixel";
        }
        if(backdrop==null){backdrop=Create("ParryBacklightBackdrop",captureCamera.transform,1050,false);backdrop.sprite=white;backdrop.color=new Color(0,0,0,.99f);}
        if(bloom==null){bloom=Create("ParryBacklightBloom",captureCamera.transform,1051,true);bloom.sprite=white;}
        if(litEnemy==null)litEnemy=Create("ParryLitEnemy",enemy.transform,1052,true);
        if(litPlayer==null){litPlayer=Create("ParryLitPlayer",player.transform,1053,true);litPlayer.gameObject.layer=player.gameObject.layer;}
    }
    private SpriteRenderer Create(string name,Transform parent,int order,bool shaded)
    {
        var root=new GameObject(name){hideFlags=HideFlags.DontSave};root.transform.SetParent(parent,false);
        var renderer=root.AddComponent<SpriteRenderer>();renderer.sortingOrder=order;renderer.enabled=false;
        if(shaded)renderer.sharedMaterial=material;
        return renderer;
    }
    private void BeforeCamera(ScriptableRenderContext context,Camera camera)
    {
        if(!IsVisible)return;
        bool show=camera==captureCamera&&target!=null&&target.IsAlive&&enemy!=null;
        if(show)UpdatePoses();
        SetRenderers(show);
    }
    private void AfterCamera(ScriptableRenderContext context,Camera camera)=>SetRenderers(false);
    private void UpdatePoses()
    {
        float height=captureCamera.orthographicSize*2;
        backdrop.transform.localPosition=new Vector3(0,0,captureCamera.nearClipPlane+.01f);
        backdrop.transform.localScale=new Vector3(height*Mathf.Max(captureCamera.aspect,16/9f),height,1);
        backdrop.color=new Color(0,0,0,.99f*Mathf.Clamp01(Intensity));
        bloom.transform.position=new Vector3(contact.x,contact.y,0);bloom.transform.rotation=Quaternion.identity;
        bloom.transform.localScale=new Vector3(3.6f,2.4f,1);
        bloom.color=new Color(1,1,1,Mathf.Clamp01(Intensity));
        properties.Clear();properties.SetFloat(Bloom,1);bloom.SetPropertyBlock(properties);
        Match(litPlayer,player);Match(litEnemy,enemy);
    }
    private void Match(SpriteRenderer destination,SpriteRenderer source)
    {
        destination.sprite=lightSprites.For(source);destination.flipX=source.flipX;destination.flipY=source.flipY;
        destination.drawMode=source.drawMode;destination.size=source.size;destination.tileMode=source.tileMode;
        destination.color=new Color(1,1,1,source.color.a*Mathf.Clamp01(Intensity));
        destination.transform.localPosition=Vector3.zero;destination.transform.localRotation=Quaternion.identity;destination.transform.localScale=Vector3.one;
        properties.Clear();properties.SetVector(LightWorld,new Vector4(contact.x,contact.y,0,0));properties.SetFloat(Bloom,0);
        properties.SetFloat(KeepGold,keepGold?1:0);
        destination.SetPropertyBlock(properties);
    }
    private void SetRenderers(bool visible)
    {
        if(backdrop!=null)backdrop.enabled=visible;
        if(bloom!=null)bloom.enabled=visible&&haloAllowed;
        if(litPlayer!=null)litPlayer.enabled=visible&&player!=null&&player.enabled;
        if(litEnemy!=null)litEnemy.enabled=visible&&enemy!=null&&enemy.enabled;
    }
    private void OnDisable()
    { RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;ResetLight(); }
    private void OnDestroy()
    {
        foreach(var renderer in new[]{backdrop,bloom,litPlayer,litEnemy})if(renderer!=null)Destroy(renderer.gameObject);
        if(white!=null)Destroy(white);
        if(ownsMaterial&&material!=null)Destroy(material);
        lightSprites.Clear();
    }
}
