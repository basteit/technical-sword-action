using UnityEngine;

/// <summary>Single camera transform owner. Shake, pan and zoom are composed before pixel snapping.</summary>
[DefaultExecutionOrder(11000)]
public sealed class VideoCombatPresentation : MonoBehaviour
{
    [SerializeField] private Camera captureCamera;
    [SerializeField] private PlayerParryCounter2D counter;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private SpriteRenderer playerVisual;
    [SerializeField] private VideoArtLibrary library;
    [SerializeField, Range(.1f,1f)] private float chargeScale=.15f;
    [SerializeField, Range(.5f,1f)] private float chargeRealSeconds=.7f;
    [SerializeField] private float normalSize=2.8125f;
    [SerializeField] private float closeSize=2.109375f;
    [SerializeField,Range(0,1)] private float slowDimStrength=.82f;
    [SerializeField,Range(0,1)] private float heroLiftStrength=.6f;
    [SerializeField,Min(.001f)] private float dimFadeInSeconds=3/60f;
    [SerializeField,Min(.001f)] private float dimFadeOutSeconds=3/60f;
    private Vector3 restPosition;
    private bool charged;
    private float zoom,shakeRemaining,shakeStrength;
    private float dimAlpha,dimBlend;
    private SpriteRenderer dimRenderer;
    private Sprite dimSprite;
    private SpriteRenderer heroLift,eyeLight,eyeGlow;
    private Material eyeGlowMaterial;
    private GameObject heroFocus;
    private float eyeTrailTimer;
    private Vector3 focus;
    private float phaseTime;
    public bool IsCharged=>charged;
    public float Zoom=>zoom;
    public float DimAlpha=>dimAlpha;
    public bool IsDimVisible=>dimRenderer!=null&&dimRenderer.enabled;
    public bool IsSpotlightVisible=>heroLift!=null&&heroLift.enabled;
    public bool AreEyesVisible=>eyeLight!=null&&eyeLight.enabled;
    public float ChargeRealElapsed { get; private set; }
    private void Awake()
    {
        if(captureCamera!=null) { restPosition=captureCamera.transform.position;EnsureDimmer(); }
        if(playerVisual==null&&counter!=null) playerVisual=counter.GetComponent<SpriteRenderer>();
        EnsureSpotlight();
    }
    private void OnEnable()
    { counter.ChargeStarted+=OnCharge;counter.ChargeReleased+=OnRelease;counter.HitConfirmed+=OnHit;counter.HitMissed+=OnMiss;counter.Finished+=OnFinished; }
    private void OnCharge()
    {
        charged=true;ChargeRealElapsed=0;
        focus=(counter.transform.position+counter.SelectedTarget.transform.position)*.5f;
        focus.y=1.35f;focus.z=restPosition.z;
        CombatTimeController.RequestSlow(counter,chargeScale,chargeRealSeconds);
    }
    private void OnRelease() { charged=false;CombatTimeController.ReleaseSlow(counter); }
    private void OnHit(Damageable2D enemy,Vector2 foot)
    {
        charged=false;CombatTimeController.ReleaseSlow(counter);
        CombatTimeController.RequestHitstop(counter,8*CombatTimeController.StepSeconds);
        Shake(.16f,.11f);vfx.Emit("Kick",foot,1.1f,1);
    }
    private void OnFinished(bool hit) { charged=false;CombatTimeController.ReleaseOwner(counter); }
    private void OnMiss() { charged=false;CombatTimeController.ReleaseSlow(counter); }
    public void Shake(float realSeconds,float strength)
    { shakeRemaining=Mathf.Max(shakeRemaining,realSeconds);shakeStrength=Mathf.Max(shakeStrength,strength); }
    public void AdvanceVisuals(float realSeconds)
    {
        if(CombatTimeController.IsPaused || captureCamera==null) return;
        phaseTime+=realSeconds;
        if(charged && !CombatTimeController.IsHitStopped) ChargeRealElapsed+=realSeconds;
        zoom=Mathf.MoveTowards(zoom,charged ? 1 : 0,realSeconds/(charged ? .1f : .22f));
        Vector3 position=Vector3.Lerp(restPosition,focus,zoom);
        if(shakeRemaining>0)
        {
            shakeRemaining=Mathf.Max(0,shakeRemaining-realSeconds);
            position+=new Vector3(Mathf.Sin(phaseTime*113),Mathf.Cos(phaseTime*137),0)*shakeStrength*(shakeRemaining/.16f);
            if(shakeRemaining==0) shakeStrength=0;
        }
        position.x=Mathf.Round(position.x*32)/32;position.y=Mathf.Round(position.y*32)/32;
        captureCamera.transform.position=position;captureCamera.orthographicSize=Mathf.Lerp(normalSize,closeSize,zoom);
        bool dimming=charged&&CombatTimeController.SlowScale<1;
        dimBlend=Mathf.MoveTowards(dimBlend,dimming?1:0,
            realSeconds/Mathf.Max(.001f,dimming?dimFadeInSeconds:dimFadeOutSeconds));
        dimAlpha=dimBlend*slowDimStrength;
        UpdateDimmer();
        UpdateSpotlight();
        if(charged&&dimAlpha>0&&playerVisual!=null)
        {
            eyeTrailTimer+=realSeconds;
            if(eyeTrailTimer>=.1f) { eyeTrailTimer%=.1f;vfx.EmitEyeTrail(playerVisual,counter.GetComponent<PlayerMotor2D>().FacingSign); }
        }
        else eyeTrailTimer=0;
    }
    private void EnsureDimmer()
    {
        if(dimRenderer!=null||captureCamera==null) return;
        var white=Texture2D.whiteTexture;
        dimSprite=Sprite.Create(white,new Rect(0,0,white.width,white.height),Vector2.one*.5f,white.width);
        dimSprite.name="VideoSlowDimPixel";dimSprite.hideFlags=HideFlags.DontSave;
        var overlay=new GameObject("VideoSlowDimmer");overlay.hideFlags=HideFlags.DontSave;
        overlay.transform.SetParent(captureCamera.transform,false);
        dimRenderer=overlay.AddComponent<SpriteRenderer>();dimRenderer.sprite=dimSprite;
        dimRenderer.sortingOrder=1000;dimRenderer.enabled=false;
        UpdateDimmer();
    }
    private void UpdateDimmer()
    {
        if(dimRenderer==null||captureCamera==null) return;
        dimRenderer.transform.localPosition=new Vector3(0,0,captureCamera.nearClipPlane+.01f);
        float height=captureCamera.orthographicSize*2;
        dimRenderer.transform.localScale=new Vector3(height*Mathf.Max(captureCamera.aspect,16/9f),height,1);
        dimRenderer.color=new Color(0,0,0,dimAlpha);dimRenderer.enabled=dimAlpha>0;
    }
    private void EnsureSpotlight()
    {
        if(heroFocus!=null||playerVisual==null) return;
        heroFocus=new GameObject("VideoHeroFocus");heroFocus.hideFlags=HideFlags.DontSave;
        heroFocus.transform.SetParent(playerVisual.transform,false);
        heroLift=heroFocus.AddComponent<SpriteRenderer>();heroLift.sortingOrder=1010;
        var eyes=new GameObject("EyesOnly");eyes.transform.SetParent(heroFocus.transform,false);
        eyeLight=eyes.AddComponent<SpriteRenderer>();eyeLight.sortingOrder=1013;
        var glow=new GameObject("EyeGlowOnly");glow.transform.SetParent(heroFocus.transform,false);
        eyeGlow=glow.AddComponent<SpriteRenderer>();eyeGlow.sortingOrder=1012;
        var shader=Shader.Find("TechnicalSwordAction/VideoPixelGlow");
        if(shader!=null) { eyeGlowMaterial=new Material(shader){hideFlags=HideFlags.DontSave};eyeGlow.sharedMaterial=eyeGlowMaterial; }
        UpdateSpotlight();
    }
    private void UpdateSpotlight()
    {
        if(heroLift==null) return;
        bool visible=dimAlpha>0&&playerVisual!=null&&playerVisual.gameObject.activeInHierarchy;
        heroLift.enabled=visible;
        if(playerVisual==null) { eyeLight.enabled=eyeGlow.enabled=false;return; }
        heroLift.sprite=playerVisual.sprite;heroLift.flipX=eyeLight.flipX=eyeGlow.flipX=playerVisual.flipX;
        var tint=playerVisual.color;heroLift.color=new Color(tint.r,tint.g,tint.b,tint.a*heroLiftStrength*dimBlend);
        var mask=library!=null?library.EyesFor(playerVisual.sprite):null;
        eyeLight.sprite=eyeGlow.sprite=mask;eyeLight.enabled=visible&&mask!=null;eyeGlow.enabled=eyeLight.enabled&&vfx.HybridGlow;
        eyeLight.color=new Color(.85f,.96f,1,dimBlend);eyeGlow.color=new Color(.65f,.9f,1,.5f*dimBlend);
    }
    public void ResetPresentation()
    {
        charged=false;zoom=shakeRemaining=shakeStrength=phaseTime=ChargeRealElapsed=dimAlpha=dimBlend=eyeTrailTimer=0;
        if(captureCamera!=null) { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
        UpdateDimmer();
        UpdateSpotlight();
        CombatTimeController.ReleaseOwner(counter);
    }
    private void OnDisable()
    { counter.ChargeStarted-=OnCharge;counter.ChargeReleased-=OnRelease;counter.HitConfirmed-=OnHit;counter.HitMissed-=OnMiss;counter.Finished-=OnFinished;ResetPresentation(); }
    private void OnDestroy()
    {
        if(dimRenderer!=null) Destroy(dimRenderer.gameObject);
        if(dimSprite!=null) Destroy(dimSprite);
        if(eyeGlowMaterial!=null) Destroy(eyeGlowMaterial);
        if(heroFocus!=null) Destroy(heroFocus);
    }
}
