using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Optional shader-only counter lighting, sampled in real time without advancing combat or authored cels.</summary>
public sealed class VideoCounterLighting : MonoBehaviour
{
    public const int FlashFrames=4,FadeFrames=20;
    public const float RimPulsePeriod=.8f;
    private const float ImpactDuration=(FlashFrames+FadeFrames)/60f;
    private Camera captureCamera;
    private SpriteRenderer player,enemy,litPlayer,litEnemy,field,backdrop;
    private Damageable2D target;
    private Material material;
    private MaterialPropertyBlock properties;
    private Sprite white;
    private bool ownsMaterial,fresh,charging;
    private float impactAge=-1,visualAge,progress;
    private Vector2 contact;
    private int facing;
    public bool IsVisible {get;private set;}
    public float ChargeProgress=>charging?progress:0;
    public int ImpactFrame=>impactAge>=0?Mathf.FloorToInt(impactAge*60+.0001f):-1;
    public string Phase=>!IsVisible?"None":charging?"Charge":ImpactFrame<FlashFrames?"Flash":"Fade";
    public float RimPulse=>.15f+.85f*(.5f+.5f*Mathf.Cos((visualAge-.2f)*2*Mathf.PI/RimPulsePeriod));
    public float ImpactWeight=>charging||impactAge<0?0:impactAge*60<FlashFrames?1:1-Mathf.SmoothStep(0,1,(impactAge*60-FlashFrames+1)/FadeFrames);
    public Damageable2D Target=>IsVisible?target:null;
    private void Awake()=>properties=new MaterialPropertyBlock();
    public void Configure(Camera camera,SpriteRenderer source,Material shared)
    {captureCamera=camera;player=source;material=shared;}
    private void OnEnable()
    {RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;}
    public void BeginImpact(Damageable2D source,Vector2 point,int direction)
    {target=source;enemy=source!=null?source.GetComponentInChildren<SpriteRenderer>():null;contact=point;facing=direction;impactAge=0;fresh=true;}
    public void Sample(float realSeconds,bool enabled,bool isCharging,float chargeProgress,Damageable2D source,int direction)
    {
        if(!enabled||captureCamera==null||player==null){ResetLight();return;}
        visualAge+=realSeconds;charging=isCharging;progress=Mathf.Clamp01(chargeProgress);
        if(impactAge>=0){if(fresh)fresh=false;else impactAge+=realSeconds;}
        if(charging)
        {
            target=source;enemy=source!=null?source.GetComponentInChildren<SpriteRenderer>():null;facing=direction;
            // The submitted preparation keeps its electrified ankle just below/behind the pivot.
            contact=player.transform.TransformPoint(new Vector3((player.flipX?1:-1)*.16f,-.24f,0));
        }
        IsVisible=target!=null&&target.gameObject.activeInHierarchy&&enemy!=null&&(charging||impactAge>=0&&impactAge<ImpactDuration-.000001f);
        if(!IsVisible){ResetLight();return;}
        Ensure();
        litEnemy.transform.SetParent(enemy.transform,false);
    }
    public void ResetLight()
    {IsVisible=charging=fresh=false;impactAge=-1;visualAge=progress=0;target=null;enemy=null;SetRenderers(false);}
    private void Ensure()
    {
        if(material==null){material=new Material(Shader.Find("TechnicalSwordAction/VideoCounterLighting")){hideFlags=HideFlags.DontSave};ownsMaterial=true;}
        if(white==null){var texture=Texture2D.whiteTexture;white=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f,texture.width);white.hideFlags=HideFlags.DontSave;}
        if(backdrop==null){backdrop=Create("CounterLightBackdrop",captureCamera.transform,1004,false);backdrop.sprite=white;}
        if(field==null){field=Create("CounterEnergyField",captureCamera.transform,1008,true);field.sprite=white;}
        if(litPlayer==null){litPlayer=Create("CounterLitPlayer",player.transform,1011,true);litPlayer.gameObject.layer=player.gameObject.layer;}
        if(litEnemy==null){litEnemy=Create("CounterLitEnemy",enemy.transform,1012,true);litEnemy.gameObject.layer=enemy.gameObject.layer;}
    }
    private SpriteRenderer Create(string name,Transform parent,int order,bool shaded)
    {
        var root=new GameObject(name){hideFlags=HideFlags.DontSave};root.transform.SetParent(parent,false);
        var renderer=root.AddComponent<SpriteRenderer>();renderer.sortingOrder=order;renderer.enabled=false;
        if(shaded)renderer.sharedMaterial=material;return renderer;
    }
    private void BeforeCamera(ScriptableRenderContext context,Camera camera)
    {
        bool visible=IsVisible&&camera==captureCamera&&target!=null&&target.gameObject.activeInHierarchy&&enemy!=null;
        if(visible)UpdatePoses();SetRenderers(visible);
    }
    private void AfterCamera(ScriptableRenderContext context,Camera camera)=>SetRenderers(false);
    private void ApplyProperties(SpriteRenderer renderer,bool actor)
    {
        properties.Clear();properties.SetFloat("_Actor",actor?1:0);properties.SetFloat("_Charge",charging?progress:0);
        properties.SetFloat("_ImpactFrame",charging?-1:ImpactFrame);properties.SetFloat("_Age",visualAge);
        properties.SetFloat("_RimPulse",RimPulse);properties.SetFloat("_ImpactWeight",ImpactWeight);
        properties.SetFloat("_Facing",facing);properties.SetVector("_LightWorld",new Vector4(contact.x,contact.y,0,0));renderer.SetPropertyBlock(properties);
    }
    private void UpdatePoses()
    {
        float height=captureCamera.orthographicSize*2;
        backdrop.transform.localPosition=new Vector3(0,0,captureCamera.nearClipPlane+.015f);
        backdrop.transform.localScale=new Vector3(height*Mathf.Max(captureCamera.aspect,16/9f),height,1);
        float darkness=.99f*ImpactWeight;
        backdrop.color=new Color(0,.001f,.004f,darkness);
        field.transform.position=new Vector3(contact.x,contact.y,0);field.transform.rotation=Quaternion.identity;
        field.transform.localScale=new Vector3(8,6,1);ApplyProperties(field,false);
        Match(litPlayer,player);Match(litEnemy,enemy);
    }
    private void Match(SpriteRenderer destination,SpriteRenderer source)
    {
        destination.sprite=source.sprite;destination.flipX=source.flipX;destination.flipY=source.flipY;
        destination.color=new Color(1,1,1,source.color.a);destination.transform.localPosition=Vector3.zero;
        destination.transform.localRotation=Quaternion.identity;destination.transform.localScale=Vector3.one;ApplyProperties(destination,true);
    }
    private void SetRenderers(bool visible)
    {
        if(backdrop!=null)backdrop.enabled=visible&&!charging&&ImpactWeight>0;
        if(field!=null)field.enabled=visible;
        if(litPlayer!=null)litPlayer.enabled=visible&&player!=null&&player.enabled;
        if(litEnemy!=null)litEnemy.enabled=visible&&enemy!=null&&enemy.enabled;
    }
    private void OnDisable()
    {RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;ResetLight();}
    private void OnDestroy()
    {
        foreach(var renderer in new[]{backdrop,field,litPlayer,litEnemy})if(renderer!=null)Destroy(renderer.gameObject);
        if(white!=null)Destroy(white);if(ownsMaterial&&material!=null)Destroy(material);
    }
}
