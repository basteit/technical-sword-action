using UnityEngine;

/// <summary>Single camera transform owner. Shake, pan and zoom are composed before pixel snapping.</summary>
[DefaultExecutionOrder(11000)]
public sealed class VideoCombatPresentation : MonoBehaviour
{
    [SerializeField] private Camera captureCamera;
    [SerializeField] private PlayerParryCounter2D counter;
    [SerializeField] private VideoVfxRenderer vfx;
    [SerializeField, Range(.1f,1f)] private float chargeScale=.25f;
    [SerializeField, Range(.5f,1f)] private float chargeRealSeconds=.7f;
    [SerializeField] private float normalSize=2.8125f;
    [SerializeField] private float closeSize=2.109375f;
    [SerializeField,Range(0,.3f)] private float slowDimStrength=.14f;
    [SerializeField,Min(.001f)] private float dimFadeInSeconds=5/60f;
    [SerializeField,Min(.001f)] private float dimFadeOutSeconds=6/60f;
    private Vector3 restPosition;
    private bool charged;
    private float zoom,shakeRemaining,shakeStrength;
    private float dimAlpha,dimBlend;
    private SpriteRenderer dimRenderer;
    private Sprite dimSprite;
    private Vector3 focus;
    private float phaseTime;
    public bool IsCharged=>charged;
    public float Zoom=>zoom;
    public float DimAlpha=>dimAlpha;
    public bool IsDimVisible=>dimRenderer!=null&&dimRenderer.enabled;
    public float ChargeRealElapsed { get; private set; }
    private void Awake()
    {
        if(captureCamera!=null) { restPosition=captureCamera.transform.position;EnsureDimmer(); }
    }
    private void OnEnable()
    { counter.ChargeStarted+=OnCharge;counter.HitConfirmed+=OnHit;counter.HitMissed+=OnMiss;counter.Finished+=OnFinished; }
    private void OnCharge()
    {
        charged=true;ChargeRealElapsed=0;
        focus=(counter.transform.position+counter.SelectedTarget.transform.position)*.5f;
        focus.y=1.35f;focus.z=restPosition.z;
        CombatTimeController.RequestSlow(counter,chargeScale,chargeRealSeconds);
    }
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
    public void ResetPresentation()
    {
        charged=false;zoom=shakeRemaining=shakeStrength=phaseTime=ChargeRealElapsed=dimAlpha=dimBlend=0;
        if(captureCamera!=null) { captureCamera.transform.position=restPosition;captureCamera.orthographicSize=normalSize; }
        UpdateDimmer();
        CombatTimeController.ReleaseOwner(counter);
    }
    private void OnDisable()
    { counter.ChargeStarted-=OnCharge;counter.HitConfirmed-=OnHit;counter.HitMissed-=OnMiss;counter.Finished-=OnFinished;ResetPresentation(); }
    private void OnDestroy()
    {
        if(dimRenderer!=null) Destroy(dimRenderer.gameObject);
        if(dimSprite!=null) Destroy(dimSprite);
    }
}
