using System.Collections.Generic;
using UnityEngine;

/// <summary>Single camera transform owner. Shake, pan and zoom are composed before pixel snapping.</summary>
[DefaultExecutionOrder(11000)]
public sealed class VideoCombatPresentation : MonoBehaviour
{
    // 1080p at 32 PPU: seven screen pixels per art pixel normally, eight in the close-up.
    public const float DefaultNormalSize=1080f/(2*32*7);
    public const float DefaultCloseSize=1080f/(2*32*8);
    [SerializeField] private Camera captureCamera;
    [SerializeField] private PlayerParryCounter2D counter;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField] private SpriteRenderer playerVisual;
    [SerializeField] private VideoArtLibrary library;
    [SerializeField, Range(.1f,1f)] private float chargeScale=.15f;
    // Eight preparation ticks at .15 speed, with a small real-time lease margin.
    [SerializeField, Range(.5f,1f)] private float chargeRealSeconds=.95f;
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
    private sealed class Impulse { public float age,duration,strength,cycles;public Vector2 direction; }
    private readonly List<Impulse> impulses=new();
    private Vector2 shakeOffset;
    private float flashRemaining,flashStrength,flashDuration;
    private Color flashTint=Color.white;
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
    public float Zoom=>zoom;
    public float DimAlpha=>dimAlpha;
    public Vector2 ShakeOffset=>shakeOffset;
    public int ActiveShakeCount=>impulses.Count;
    public float FlashAlpha=>flashRenderer!=null&&flashRenderer.enabled?flashRenderer.color.a:0;
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
        EnsureSpotlight();
    }
    private void OnEnable()
    { counter.Started+=OnCounterStarted;counter.BlinkStarted+=OnBlinkStarted;counter.BlinkArrived+=OnBlinkArrived;counter.ChargeStarted+=OnCharge;counter.ChargeReleased+=OnRelease;counter.HitConfirmed+=OnHit;counter.HitMissed+=OnMiss;counter.Finished+=OnFinished;counter.Landed+=OnLanding; }
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
        RestorePlayerBody();charged=true;ChargeRealElapsed=0;
        focus=(counter.transform.position+counter.SelectedTarget.transform.position)*.5f;
        focus.y=counter.IsOverheadHeelDrop?focus.y-.15f:adaptiveFocusHeight?counter.transform.position.y-.5f:1.05f;focus.z=restPosition.z;
        CombatTimeController.RequestSlow(counter,chargeScale,chargeRealSeconds);
    }
    private void OnRelease() { charged=false;CombatTimeController.ReleaseSlow(counter); }
    private void OnHit(Damageable2D enemy,Vector2 foot)
    {
        charged=false;CombatTimeController.ReleaseSlow(counter);
        CombatTimeController.RequestHitstop(counter,8*CombatTimeController.StepSeconds);
        ShakeDirectional(vfx.ReadableImpacts?.22f:.25f,vfx.ReadableImpacts?.26f:.24f,counter.AttacksFromBehind?new Vector2(counter.KickFacingSign,-.25f):new Vector2(counter.KickFacingSign*.35f,-1),vfx.ReadableImpacts?1.75f:2.75f);
        flashRemaining=flashDuration=(vfx.ReadableImpacts?2:3)/60f;flashStrength=vfx.ReadableImpacts?.20f:.12f;flashTint=Color.white;
        bool authored=library!=null&&library.Find("CounterKick")?.includesCounterTrail==true;
        vfx.Emit(counter.IsOverheadHeelDrop?"HeelLightning":authored?"ElectricKick":"Kick",foot,counter.IsOverheadHeelDrop?1f:authored?1.15f:1.55f,counter.KickFacingSign);
    }
    private void OnLanding()=>ShakeDirectional(.11f,.065f,Vector2.down,1.5f);
    private void OnFinished(bool hit)
    { RestorePlayerBody();charged=false;CombatTimeController.ReleaseOwner(counter);if(!hit) ResetPresentation(); }
    private void OnMiss() { charged=false;CombatTimeController.ReleaseSlow(counter); }
    public void Shake(float realSeconds,float strength)
    { ShakeDirectional(realSeconds,strength,new Vector2(1,.35f),3); }
    public void SetCameraFrame(Vector3 position,float size)
    {
        restPosition=position;normalSize=Mathf.Max(.5f,size);closeSize=normalSize*(7f/8f);
        if(zoom==0&&captureCamera!=null)
        { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
    }
    public void PresentLightParry(int facing)
    {
        // A single beat and return carries the impact, while the hero keeps a stable base.
        ShakeDirectional((vfx.ReadableImpacts?6:7)/60f,vfx.ReadableImpacts?.145f:.135f,new Vector2(-facing,-.3f),vfx.ReadableImpacts?1f:1.25f);
        flashRemaining=flashDuration=3/60f;flashStrength=.18f;flashTint=Color.black;
    }
    public void ShakeDirectional(float realSeconds,float strength,Vector2 direction,float cycles=3)
    {
        if(realSeconds<=0||strength<=0) return;
        impulses.Add(new Impulse{duration=realSeconds,strength=strength,direction=direction.normalized,cycles=cycles});
    }
    public void AdvanceVisuals(float realSeconds)
    {
        if(CombatTimeController.IsPaused || captureCamera==null) return;
        if(charged && !CombatTimeController.IsHitStopped) ChargeRealElapsed+=realSeconds;
        zoom=Mathf.MoveTowards(zoom,charged ? 1 : 0,realSeconds/(charged ? .1f : .22f));
        Vector3 position=Vector3.Lerp(restPosition,focus,zoom);
        shakeOffset=Vector2.zero;
        for(int i=impulses.Count-1;i>=0;i--)
        {
            var impulse=impulses[i];float t=Mathf.Clamp01(impulse.age/impulse.duration);
            shakeOffset+=impulse.direction*impulse.strength*(1-t)*(1-t)*Mathf.Cos(t*Mathf.PI*2*impulse.cycles);
            impulse.age+=realSeconds;if(impulse.age>=impulse.duration) impulses.RemoveAt(i);
        }
        shakeOffset=Vector2.ClampMagnitude(shakeOffset,.28f);position+=(Vector3)shakeOffset;
        position.x=Mathf.Round(position.x*32)/32;position.y=Mathf.Round(position.y*32)/32;
        captureCamera.transform.position=position;captureCamera.orthographicSize=Mathf.Lerp(normalSize,closeSize,zoom);
        bool dimming=charged&&CombatTimeController.SlowScale<1;
        dimBlend=Mathf.MoveTowards(dimBlend,dimming?1:0,
            realSeconds/Mathf.Max(.001f,dimming?dimFadeInSeconds:dimFadeOutSeconds));
        dimAlpha=dimBlend*slowDimStrength;
        UpdateDimmer();
        flashRemaining=Mathf.Max(0,flashRemaining-realSeconds);
        if(flashRenderer!=null) { flashRenderer.color=new Color(flashTint.r,flashTint.g,flashTint.b,flashStrength*flashRemaining/Mathf.Max(.001f,flashDuration));flashRenderer.enabled=flashRemaining>0; }
        UpdateSpotlight();
        bool showCounter=counter!=null&&counter.IsActive&&counter.HasAuthoredTransfer&&counter.ElapsedFrames>=counter.BlinkArrivalFrame&&playerVisual!=null&&playerVisual.enabled;
        vfx.SampleCounterEffect(playerVisual,showCounter?counter.ElapsedFrames-counter.BlinkArrivalFrame:-1,showCounter);
        if(transitShadow!=null&&transitShadow.enabled&&counter!=null)
            transitShadow.transform.localPosition=new Vector3(0,shadowGroundY-counter.transform.position.y,0);
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
        RestorePlayerBody();
        charged=false;zoom=ChargeRealElapsed=dimAlpha=dimBlend=eyeTrailTimer=flashRemaining=flashStrength=flashDuration=0;
        flashTint=Color.white;
        impulses.Clear();shakeOffset=Vector2.zero;if(flashRenderer!=null) flashRenderer.enabled=false;
        if(captureCamera!=null) { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
        UpdateDimmer();
        UpdateSpotlight();
        CombatTimeController.ReleaseOwner(counter);
    }
    private void OnDisable()
    { counter.Started-=OnCounterStarted;counter.BlinkStarted-=OnBlinkStarted;counter.BlinkArrived-=OnBlinkArrived;counter.ChargeStarted-=OnCharge;counter.ChargeReleased-=OnRelease;counter.HitConfirmed-=OnHit;counter.HitMissed-=OnMiss;counter.Finished-=OnFinished;counter.Landed-=OnLanding;ResetPresentation(); }
    private void OnDestroy()
    {
        if(dimRenderer!=null) Destroy(dimRenderer.gameObject);
        if(flashRenderer!=null) Destroy(flashRenderer.gameObject);
        if(dimSprite!=null) Destroy(dimSprite);
        if(eyeGlowMaterial!=null) Destroy(eyeGlowMaterial);
        if(subjectLiftMaterial!=null) Destroy(subjectLiftMaterial);
        if(heroFocus!=null) Destroy(heroFocus);
    }
}
