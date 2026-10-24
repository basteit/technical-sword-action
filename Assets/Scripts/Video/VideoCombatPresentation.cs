using System.Collections.Generic;
using UnityEngine;

/// <summary>Single camera transform owner. Shake, pan and zoom are composed before pixel snapping.</summary>
[DefaultExecutionOrder(11000)]
public sealed class VideoCombatPresentation : MonoBehaviour
{
    // Apparent midpoint of the 6/7px close take and the 4/5px game-distance take.
    public const float DefaultNormalSize=1080f/(2*32*5);
    public const float DefaultCloseSize=1080f/(2*32*6);
    public const int DefaultParryHitstopFrames=9;
    public const int DefaultJustParryHitstopFrames=10;
    public const int DefaultKickHitstopFrames=16;
    [SerializeField] private Camera captureCamera;
    [SerializeField] private PlayerParryCounter2D counter;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private SpriteRenderer playerVisual;
    [SerializeField] private VideoArtLibrary library;
    [SerializeField, Range(.1f,1f)] private float chargeScale=.15f;
    // Nine preparation ticks at .15 speed, including the final tucked cel.
    [SerializeField, Range(.5f,1.5f)] private float chargeRealSeconds=1.1f;
    [SerializeField,Min(0)] private int kickHitstopFrames=DefaultKickHitstopFrames;
    [SerializeField,Range(0,4)] private int parryInversionFrames=4;
    [SerializeField] private bool parryBacklight=true;
    [SerializeField] private bool preserveParrySparkColor=true;
    [SerializeField] private Material parryBacklightMaterial;
    [SerializeField] private bool counterLightingEnabled=true;
    [SerializeField] private Material counterLightingMaterial;
    [SerializeField,Range(0,.2f)] private float impactZoomAmount=.06f;
    [SerializeField] private float normalSize=DefaultNormalSize;
    [SerializeField] private float closeSize=DefaultCloseSize;
    [SerializeField] private bool adaptiveFocusHeight;
    [SerializeField,Range(0,1)] private float slowDimStrength=.965f;
    [SerializeField,Range(0,1)] private float heroLiftStrength=.6f;
    [SerializeField,Min(.001f)] private float dimFadeInSeconds=3/60f;
    [SerializeField,Min(.001f)] private float dimFadeOutSeconds=3/60f;
    private Vector3 restPosition;
    private bool charged;
    private float zoom;
    private float impactZoom,impactZoomAge=-1;
    private const float ImpactZoomIn=2/60f,ImpactZoomHold=6/60f,ImpactZoomOut=12/60f;
    private sealed class Impulse { public float age,duration,strength,cycles;public Vector2 direction; }
    private readonly List<Impulse> impulses=new();
    private Vector2 shakeOffset;
    private float flashRemaining,flashStrength,flashDuration;
    private Color flashTint=Color.white;
    private float parryInversionAge=-1,parryInversionDuration;
    private VideoParryBacklight backlight;
    private VideoCounterLighting counterLighting;
    private bool counterLightReady;
    private SpriteRenderer flashRenderer;
    private float dimAlpha,dimBlend;
    private SpriteRenderer dimRenderer;
    private Sprite dimSprite;
    private SpriteRenderer heroLift,eyeLight,eyeGlow;
    private Material eyeGlowMaterial;
    private Material subjectLiftMaterial;
    private GameObject heroFocus;
    private float eyeTrailTimer;
    private SpriteRenderer transitShadow;
    private bool ownsHiddenBody;
    private float shadowGroundY;
    private Vector3 focus;
    public bool IsCharged=>charged;
    public float Zoom=>Mathf.Max(zoom,impactZoom);
    public float ImpactZoom=>impactZoom;
    public float DimAlpha=>dimAlpha;
    public Vector2 ShakeOffset=>shakeOffset;
    public int ActiveShakeCount=>impulses.Count;
    public float FlashAlpha=>flashRenderer!=null&&flashRenderer.enabled?flashRenderer.color.a:0;
    public bool IsParryInverted=>VideoParryInversionFeature.IsVisible(captureCamera);
    public bool IsParryBacklit=>backlight!=null&&backlight.IsVisible;
    public bool IsParryImpactVisible=>IsParryBacklit||IsParryInverted;
    public Damageable2D BacklitEnemy=>backlight!=null?backlight.Target:null;
    public bool IsCounterLightVisible=>counterLighting!=null&&counterLighting.IsVisible;
    public string CounterLightPhase=>counterLighting!=null?counterLighting.Phase:"None";
    public int CounterLightFrame=>counterLighting!=null?counterLighting.ImpactFrame:-1;
    public bool IsDimVisible=>dimRenderer!=null&&dimRenderer.enabled;
    public bool IsSpotlightVisible=>heroLift!=null&&heroLift.enabled;
    public bool AreEyesVisible=>eyeLight!=null&&eyeLight.enabled;
    public bool IsTransitShadowVisible=>transitShadow!=null&&transitShadow.enabled;
    public bool IsPlayerBodyVisible=>playerVisual!=null&&playerVisual.enabled;
    public float ChargeRealElapsed { get; private set; }
    private void Awake()
    {
        if(captureCamera!=null) { restPosition=captureCamera.transform.position;EnsureDimmer(); }
        if(playerVisual==null&&counter!=null) playerVisual=counter.GetComponent<SpriteRenderer>();
        if(captureCamera!=null)
        {
            backlight=captureCamera.gameObject.AddComponent<VideoParryBacklight>();backlight.hideFlags=HideFlags.DontSave;
            backlight.Configure(captureCamera,playerVisual,parryBacklightMaterial);
            counterLighting=captureCamera.gameObject.AddComponent<VideoCounterLighting>();counterLighting.hideFlags=HideFlags.DontSave;
            counterLighting.Configure(captureCamera,playerVisual,counterLightingMaterial);
        }
        EnsureSpotlight();
    }
    private void OnEnable()
    { CombatTimeController.SessionReset+=ResetParryInversion;counter.Started+=OnCounterStarted;counter.BlinkStarted+=OnBlinkStarted;counter.BlinkArrived+=OnBlinkArrived;counter.ChargeStarted+=OnCharge;counter.ChargeReleased+=OnRelease;counter.HitConfirmed+=OnHit;counter.HitMissed+=OnMiss;counter.Finished+=OnFinished;counter.Landed+=OnLanding; }
    private void OnCounterStarted()
    {
        if(!counter.HasAuthoredTransfer) BeginTransit();
    }
    private void OnBlinkStarted() { if(counter.HasAuthoredTransfer) BeginTransit(); }
    private void OnBlinkArrived() { if(counter.HasAuthoredTransfer) RestorePlayerBody(); }
    private void BeginTransit()
    {
        if(!counter.UsesBlinkApproach||playerVisual==null) return;
        int facing=counter.GetComponent<PlayerMotor2D>().FacingSign;
        Vector2 from=playerVisual.transform.position;
        Vector2 to=counter.IsOverheadHeelDrop||counter.AttacksFromBehind?counter.HoverPosition:(Vector2)counter.SelectedTarget.transform.position-new Vector2(facing*.85f,0);
        vfx.EmitEyeTrail(playerVisual,facing);
        vfx.EmitTransitSilhouette(playerVisual,facing,from,Vector2.one,.5f);
        vfx.EmitTransitSilhouette(playerVisual,facing,Vector2.Lerp(from,to,.45f)+Vector2.up*.2f,new Vector2(1.7f,.45f),.28f);
        vfx.EmitTransitSilhouette(playerVisual,facing,to+Vector2.up*.45f,new Vector2(1.4f,.25f),.18f);
        shadowGroundY=from.y-1+.03f;
        ownsHiddenBody=playerVisual.enabled;
        if(ownsHiddenBody) playerVisual.enabled=false;
        if(transitShadow!=null) { transitShadow.sprite=playerVisual.sprite;transitShadow.flipX=playerVisual.flipX;transitShadow.enabled=true; }
    }
    private void RestorePlayerBody()
    {
        if(ownsHiddenBody&&playerVisual!=null) playerVisual.enabled=true;
        ownsHiddenBody=false;if(transitShadow!=null) transitShadow.enabled=false;
    }
    private void OnCharge()
    {
        RestorePlayerBody();charged=true;counterLightReady=true;ChargeRealElapsed=0;
        focus=(counter.transform.position+counter.SelectedTarget.transform.position)*.5f;
        focus.y=counter.IsOverheadHeelDrop?focus.y-.15f:adaptiveFocusHeight?counter.transform.position.y-.5f:1.05f;focus.z=restPosition.z;
        CombatTimeController.RequestSlow(counter,chargeScale,chargeRealSeconds);
    }
    private void OnRelease() { charged=false;CombatTimeController.ReleaseSlow(counter); }
    private void OnHit(Damageable2D enemy,Vector2 foot)
    {
        charged=false;CombatTimeController.ReleaseSlow(counter);
        CombatTimeController.RequestHitstop(counter,kickHitstopFrames*CombatTimeController.StepSeconds);
        if(counterLightReady&&counterLightingEnabled&&vfx.HybridGlow)counterLighting?.BeginImpact(enemy,foot,counter.KickFacingSign);
        impactZoomAge=0;impactZoom=0;
        ShakeDirectional(.30f,vfx.ReadableImpacts?.38f:.36f,counter.AttacksFromBehind?new Vector2(counter.KickFacingSign,-.25f):new Vector2(counter.KickFacingSign*.35f,-1),vfx.ReadableImpacts?1.75f:2.25f);
        flashRemaining=flashDuration=(vfx.ReadableImpacts?2:3)/60f;flashStrength=vfx.ReadableImpacts?.20f:.12f;flashTint=Color.white;
        bool authored=library!=null&&library.Find("CounterKick")?.includesCounterTrail==true;
        vfx.Emit(counter.IsOverheadHeelDrop?"HeelLightning":authored?"ElectricKick":"Kick",foot,counter.IsOverheadHeelDrop?1f:authored?1.15f:1.55f,counter.KickFacingSign);
    }
    private void OnLanding()=>ShakeDirectional(.11f,.065f,Vector2.down,1.5f);
    private void OnFinished(bool hit)
    { RestorePlayerBody();charged=false;counterLightReady=false;counterLighting?.ResetLight();CombatTimeController.ReleaseOwner(counter);if(!hit) ResetPresentation(); }
    private void OnMiss() { charged=false;counterLighting?.ResetLight();CombatTimeController.ReleaseSlow(counter); }
    public void Shake(float realSeconds,float strength)
    { ShakeDirectional(realSeconds,strength,new Vector2(1,.35f),3); }
    public void SetCameraFrame(Vector3 position,float size)
    {
        restPosition=position;normalSize=Mathf.Max(.5f,size);closeSize=normalSize*(5f/6f);
        if(Zoom==0&&captureCamera!=null)
        { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
    }
    public void PresentLightParry(int facing,Damageable2D source,Vector2 contact)
    {
        // A single beat and return carries the impact, while the hero keeps a stable base.
        ShakeDirectional(9/60f,vfx.ReadableImpacts?.22f:.21f,new Vector2(-facing,-.3f),vfx.ReadableImpacts?1f:1.25f);
        if(parryInversionFrames>0)
        {
            // Keep the four-frame impact pulse independent of the combat stop.
            flashRemaining=0;if(flashRenderer!=null)flashRenderer.enabled=false;
            parryInversionAge=0;parryInversionDuration=parryInversionFrames/60f;
            if(parryBacklight)backlight?.Begin(source,contact);
            VideoParryInversionFeature.SetVisible(captureCamera,!parryBacklight);
        }
        else
        {
            ResetParryInversion();
            flashRemaining=flashDuration=3/60f;flashStrength=.18f;flashTint=Color.black;
        }
    }
    private void AdvanceParryInversion(float realSeconds)
    {
        bool visible=parryInversionAge>=0&&parryInversionAge<parryInversionDuration-.000001f;
        VideoParryInversionFeature.SetVisible(captureCamera,visible&&!parryBacklight);
        backlight?.Sample(visible&&parryBacklight,vfx.HybridGlow,preserveParrySparkColor);
        parryInversionAge=visible?parryInversionAge+realSeconds:-1;
    }
    private void ResetParryInversion()
    { parryInversionAge=-1;parryInversionDuration=0;VideoParryInversionFeature.SetVisible(captureCamera,false);backlight?.ResetLight();counterLighting?.ResetLight();counterLightReady=false; }
    public void ShakeDirectional(float realSeconds,float strength,Vector2 direction,float cycles=3)
    {
        if(realSeconds<=0||strength<=0) return;
        impulses.Add(new Impulse{duration=realSeconds,strength=strength,direction=direction.normalized,cycles=cycles});
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if(CombatTimeController.IsPaused || captureCamera==null) return;
        AdvanceParryInversion(realSeconds);
        if(charged && !CombatTimeController.IsHitStopped) ChargeRealElapsed+=realSeconds;
        zoom=Mathf.MoveTowards(zoom,charged ? 1 : 0,realSeconds/(charged ? .1f : .22f));
        if(impactZoomAge>=0)
        {
            impactZoomAge+=realSeconds;
            impactZoom=impactZoomAge<ImpactZoomIn?impactZoomAge/ImpactZoomIn:
                1-Mathf.Clamp01((impactZoomAge-ImpactZoomIn-ImpactZoomHold)/ImpactZoomOut);
            if(impactZoomAge>=ImpactZoomIn+ImpactZoomHold+ImpactZoomOut){impactZoomAge=-1;impactZoom=0;}
        }
        Vector3 position=Vector3.Lerp(restPosition,focus,Zoom);
        shakeOffset=Vector2.zero;
        for(int i=impulses.Count-1;i>=0;i--)
        {
            var impulse=impulses[i];float t=Mathf.Clamp01(impulse.age/impulse.duration);
            shakeOffset+=impulse.direction*impulse.strength*(1-t)*(1-t)*Mathf.Cos(t*Mathf.PI*2*impulse.cycles);
            impulse.age+=realSeconds;if(impulse.age>=impulse.duration) impulses.RemoveAt(i);
        }
        shakeOffset=Vector2.ClampMagnitude(shakeOffset,.42f);position+=(Vector3)shakeOffset;
        position.x=Mathf.Round(position.x*32)/32;position.y=Mathf.Round(position.y*32)/32;
        captureCamera.transform.position=position;
        captureCamera.orthographicSize=Mathf.Lerp(Mathf.Lerp(normalSize,closeSize,zoom),closeSize*(1-impactZoomAmount),impactZoom);
        bool dimming=charged&&CombatTimeController.SlowScale<1;
        dimBlend=Mathf.MoveTowards(dimBlend,dimming?1:0,
            realSeconds/Mathf.Max(.001f,dimming?dimFadeInSeconds:dimFadeOutSeconds));
        dimAlpha=dimBlend*slowDimStrength;
        UpdateDimmer();
        flashRemaining=Mathf.Max(0,flashRemaining-realSeconds);
        if(flashRenderer!=null) { flashRenderer.color=new Color(flashTint.r,flashTint.g,flashTint.b,flashStrength*flashRemaining/Mathf.Max(.001f,flashDuration));flashRenderer.enabled=flashRemaining>0; }
        UpdateSpotlight();
        bool showCounter=counter!=null&&counter.IsActive&&counter.HasAuthoredTransfer&&counter.ElapsedFrames>=counter.BlinkArrivalFrame&&playerVisual!=null&&playerVisual.enabled;
        var animation=counter!=null?counter.GetComponent<VideoPlayerAnimationDriver>():null;
        int effectFrame=animation!=null?animation.CounterArtFrame:counter!=null?counter.ElapsedFrames-counter.BlinkArrivalFrame:-1;
        vfx.SampleCounterEffect(playerVisual,showCounter?effectFrame:-1,showCounter);
        if(transitShadow!=null&&transitShadow.enabled&&counter!=null)
            transitShadow.transform.localPosition=new Vector3(0,shadowGroundY-counter.transform.position.y,0);
        if(charged&&dimAlpha>0&&playerVisual!=null)
        {
            eyeTrailTimer+=realSeconds;
            if(eyeTrailTimer>=.1f) { eyeTrailTimer%=.1f;vfx.EmitEyeTrail(playerVisual,counter.GetComponent<PlayerMotor2D>().FacingSign); }
        }
        else eyeTrailTimer=0;
        float chargeSeconds=Mathf.Max(CombatTimeController.StepSeconds,(counter.ChargeReleaseFrame-counter.ChargeFrame)*CombatTimeController.StepSeconds/chargeScale);
        if(counter.SelectedTarget==null||!counter.SelectedTarget.gameObject.activeInHierarchy)counterLightReady=false;
        counterLighting?.Sample(realSeconds,counterLightReady&&counterLightingEnabled&&vfx.HybridGlow,charged,ChargeRealElapsed/chargeSeconds,counter.SelectedTarget,counter.KickFacingSign);
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
        var flash=new GameObject("VideoImpactFlash");flash.hideFlags=HideFlags.DontSave;flash.transform.SetParent(captureCamera.transform,false);
        flashRenderer=flash.AddComponent<SpriteRenderer>();flashRenderer.sprite=dimSprite;flashRenderer.sortingOrder=1001;flashRenderer.enabled=false;
        UpdateDimmer();
    }
    private void UpdateDimmer()
    {
        if(dimRenderer==null||captureCamera==null) return;
        dimRenderer.transform.localPosition=new Vector3(0,0,captureCamera.nearClipPlane+.01f);
        float height=captureCamera.orthographicSize*2;
        dimRenderer.transform.localScale=new Vector3(height*Mathf.Max(captureCamera.aspect,16/9f),height,1);
        dimRenderer.color=new Color(0,0,0,dimAlpha);dimRenderer.enabled=dimAlpha>0;
        if(flashRenderer!=null) { flashRenderer.transform.localPosition=dimRenderer.transform.localPosition;flashRenderer.transform.localScale=dimRenderer.transform.localScale; }
    }
    private void EnsureSpotlight()
    {
        if(heroFocus!=null||playerVisual==null) return;
        heroFocus=new GameObject("VideoHeroFocus");heroFocus.hideFlags=HideFlags.DontSave;
        heroFocus.transform.SetParent(playerVisual.transform,false);
        heroLift=heroFocus.AddComponent<SpriteRenderer>();heroLift.sortingOrder=1010;
        var liftShader=Shader.Find("TechnicalSwordAction/VideoSubjectLift");
        if(liftShader!=null) { subjectLiftMaterial=new Material(liftShader){hideFlags=HideFlags.DontSave};heroLift.sharedMaterial=subjectLiftMaterial; }
        var eyes=new GameObject("EyesOnly");eyes.transform.SetParent(heroFocus.transform,false);
        eyeLight=eyes.AddComponent<SpriteRenderer>();eyeLight.sortingOrder=1013;
        var glow=new GameObject("EyeGlowOnly");glow.transform.SetParent(heroFocus.transform,false);
        eyeGlow=glow.AddComponent<SpriteRenderer>();eyeGlow.sortingOrder=1012;
        var shader=Shader.Find("TechnicalSwordAction/VideoPixelGlow");
        if(shader!=null) { eyeGlowMaterial=new Material(shader){hideFlags=HideFlags.DontSave};eyeGlow.sharedMaterial=eyeGlowMaterial; }
        var shadow=new GameObject("BlinkGroundShadow");shadow.transform.SetParent(heroFocus.transform,false);
        shadow.transform.localScale=new Vector3(1.6f,.08f,1);
        transitShadow=shadow.AddComponent<SpriteRenderer>();transitShadow.sortingOrder=9;
        transitShadow.color=new Color(0,0,0,.25f);transitShadow.enabled=false;
        UpdateSpotlight();
    }
    private void UpdateSpotlight()
    {
        if(heroLift==null) return;
        bool visible=dimAlpha>0&&playerVisual!=null&&playerVisual.enabled&&playerVisual.gameObject.activeInHierarchy;
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
        ResetParryInversion();
        RestorePlayerBody();
        charged=false;zoom=ChargeRealElapsed=dimAlpha=dimBlend=eyeTrailTimer=flashRemaining=flashStrength=flashDuration=0;
        impactZoom=0;impactZoomAge=-1;
        flashTint=Color.white;
        impulses.Clear();shakeOffset=Vector2.zero;if(flashRenderer!=null) flashRenderer.enabled=false;
        if(captureCamera!=null) { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
        UpdateDimmer();
        UpdateSpotlight();
        CombatTimeController.ReleaseOwner(counter);
    }
    private void OnDisable()
    { CombatTimeController.SessionReset-=ResetParryInversion;counter.Started-=OnCounterStarted;counter.BlinkStarted-=OnBlinkStarted;counter.BlinkArrived-=OnBlinkArrived;counter.ChargeStarted-=OnCharge;counter.ChargeReleased-=OnRelease;counter.HitConfirmed-=OnHit;counter.HitMissed-=OnMiss;counter.Finished-=OnFinished;counter.Landed-=OnLanding;ResetPresentation(); }
    private void OnDestroy()
    {
        if(dimRenderer!=null) Destroy(dimRenderer.gameObject);
        if(flashRenderer!=null) Destroy(flashRenderer.gameObject);
        if(dimSprite!=null) Destroy(dimSprite);
        if(eyeGlowMaterial!=null) Destroy(eyeGlowMaterial);
        if(subjectLiftMaterial!=null) Destroy(subjectLiftMaterial);
        if(heroFocus!=null) Destroy(heroFocus);
        if(backlight!=null)Destroy(backlight);
        if(counterLighting!=null)Destroy(counterLighting);
    }
}
