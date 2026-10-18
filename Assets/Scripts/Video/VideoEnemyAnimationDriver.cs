using UnityEngine;

public sealed class VideoEnemyAnimationDriver : MonoBehaviour, ICombatTickListener, ICombatTimerListener
{
    [SerializeField] private VideoArtLibrary library;
    [SerializeField] private Transform player;
    private Damageable2D health;
    private EnemyMeleeChaser2D ai;
    private SpriteRenderer visual;
    private Animator animator;
    private string motion="Idle";
    private int elapsed;
    [SerializeField] private Vector3 restPosition;
    [SerializeField] private LayerMask hitbackObstacles;
    private int hitbackFrame,hitbackFrames;
    private long hitbackStartTick=-1;
    private float hitbackDistance;
    private float launchHeight;
    private Vector3 hitbackOrigin;
    private Vector2 hitbackDirection;
    private BoxCollider2D bodyCollider;
    private Sprite deathContactSprite;
    private long deathContactTick=-1;
    private Vector3 deathContactPosition;
    private Quaternion deathContactRotation;
    private float parryRecoilSign;
    private int restSortingOrder;
    private long contactDepthTick=-1;
    public bool IsHitBacking=>hitbackFrames>0&&hitbackFrame<hitbackFrames;
    public float LastHitbackDistance {get;private set;}
    public bool IsLaunched=>IsHitBacking&&launchHeight>0;
    public float LastLaunchHeight {get;private set;}
    public string MotionName=>motion;
    public int CombatTickOrder=>320;
    public void BindPlayer(Transform target) => player=target;
    private void OnEnable()
    {
        health=GetComponent<Damageable2D>();ai=GetComponent<EnemyMeleeChaser2D>();visual=GetComponentInChildren<SpriteRenderer>();
        restSortingOrder=visual.sortingOrder;
        bodyCollider=GetComponent<BoxCollider2D>();
        animator=visual.GetComponent<Animator>();CombatTimeController.RegisterAnimator(animator,this);
        health.HitReceived+=OnHit;health.Parried+=OnParried;health.Died+=OnDeath;health.ResetPerformed+=OnReset;
        ai.AttackStarted+=OnAttack;CombatTimeController.Register(this);
        OnReset(health);
    }
    private void Play(string name)
    {
        motion=name;elapsed=0;
        if(animator!=null&&animator.runtimeAnimatorController!=null)
        { animator.Play("EnemyVideo_"+name,0,0);animator.Update(0); }
        Show();
    }
    private void OnHit(Damageable2D source,int damage,Vector2 direction) { if(source.IsAlive) Play("Hit"); }
    private void OnParried(Damageable2D source,ParryResult result)
    {
        parryRecoilSign=player!=null&&transform.position.x<player.position.x?-1:1;
        Play("Parried");
    }
    private void OnDeath(Damageable2D source,Vector2 direction)
    {
        // Keep the body at the actual contact during hitstop. Collapsing immediately
        // moves the head away from the heel before the viewer can read the hit.
        deathContactSprite=visual.sprite;deathContactTick=CombatTimeController.TickCount;
        deathContactPosition=visual.transform.localPosition;deathContactRotation=visual.transform.localRotation;
        Play("Death");RestoreDeathContact();
    }
    private void OnAttack()=>Play("Attack");
    private void OnReset(Damageable2D source)
    {
        hitbackFrame=hitbackFrames=0;hitbackStartTick=-1;LastHitbackDistance=launchHeight=LastLaunchHeight=0;
        deathContactSprite=null;deathContactTick=-1;parryRecoilSign=0;
        contactDepthTick=-1;visual.sortingOrder=restSortingOrder;
        visual.transform.localPosition=restPosition;visual.transform.localRotation=Quaternion.identity;visual.color=Color.white;
        if(animator!=null) animator.Rebind();Play("Idle");
    }
    public void CombatTick()
    {
        AdvanceHitback();
        if (health.IsAlive)
        {
            string desired=health.IsStunned?(motion=="Hit"?"Hit":"Parried"):
                ai.IsWindingUp?"Windup":ai.CurrentStateName=="Approach"?"Move":"Idle";
            if (motion=="Attack" && elapsed<16) desired="Attack";
            if (motion=="Hit" && elapsed<6) desired="Hit";
            if(desired!=motion) Play(desired);
            if(player!=null&&!health.IsStunned) visual.flipX=player.position.x<transform.position.x;
        }
        elapsed++;Show();
    }
    public void ApplyHitback(float distance,int frames,Vector2 direction)
    {
        if(!isActiveAndEnabled) return;
        hitbackOrigin=transform.position;hitbackDistance=Mathf.Max(0,distance);
        hitbackDirection=direction.sqrMagnitude>0?direction.normalized:Vector2.right;
        hitbackFrame=0;hitbackFrames=Mathf.Max(1,frames);hitbackStartTick=CombatTimeController.TickCount;LastHitbackDistance=hitbackDistance;
        launchHeight=LastLaunchHeight=0;if(deathContactTick!=CombatTimeController.TickCount)visual.transform.localRotation=Quaternion.identity;
    }
    public void CombatTickTimers()
    {
        if(contactDepthTick>=0&&CombatTimeController.TickCount>contactDepthTick)
        {visual.sortingOrder=restSortingOrder;contactDepthTick=-1;}
        if(deathContactSprite==null) return;
        if(CombatTimeController.TickCount==deathContactTick) RestoreDeathContact();
        else { deathContactSprite=null;deathContactTick=-1; }
    }
    private void RestoreDeathContact()
    {
        if(deathContactSprite==null)return;
        visual.sprite=deathContactSprite;visual.transform.localPosition=deathContactPosition;
        visual.transform.localRotation=deathContactRotation;
    }
    public void ApplyLaunch(float distance,float height,int frames,Vector2 direction)
    {
        ApplyHitback(distance,frames,direction);
        launchHeight=LastLaunchHeight=Mathf.Max(0,height);
        // The authored kick has a broad foreground trail. Keep the struck body readable
        // over that trail only for the contact tick and its hitstop, then restore depth.
        var attacker=player!=null?player.GetComponent<SpriteRenderer>():null;
        if(attacker!=null){contactDepthTick=CombatTimeController.TickCount;visual.sortingOrder=Mathf.Max(restSortingOrder,attacker.sortingOrder+1);}
    }
    private void AdvanceHitback()
    {
        if(!IsHitBacking||CombatTimeController.TickCount<=hitbackStartTick) return;
        hitbackFrame++;
        float t=hitbackFrame/(float)hitbackFrames;
        float travel=launchHeight>0?1-Mathf.Pow(1-t,3):1-(1-t)*(1-t);
        Vector2 desired=(Vector2)hitbackOrigin+hitbackDirection*hitbackDistance*travel+Vector2.up*launchHeight*Mathf.Sin(t*Mathf.PI);
        Vector2 delta=desired-(Vector2)transform.position;
        if(bodyCollider!=null&&hitbackObstacles.value!=0&&delta.sqrMagnitude>.000001f)
        {
            Vector2 size=Vector2.Scale(bodyCollider.size,transform.lossyScale);
            var hit=Physics2D.BoxCast((Vector2)transform.position+bodyCollider.offset,size,0,delta.normalized,delta.magnitude+.01f,hitbackObstacles);
            if(hit.collider!=null&&Vector2.Dot(delta.normalized,hit.normal)<-.001f)
                delta=delta.normalized*Mathf.Max(0,hit.distance-.01f);
        }
        transform.position+=(Vector3)delta;
        if(launchHeight>0) visual.transform.localRotation=Quaternion.Euler(0,0,-hitbackDirection.x*95*Mathf.Sin(t*Mathf.PI*.8f));
    }
    private void Show()
    {
        if(library==null || visual==null) return;
        var clip=library.Find("Enemy_"+motion);
        if(clip!=null&&(animator==null||animator.runtimeAnimatorController==null)) visual.sprite=clip.At(elapsed,motion=="Idle"||motion=="Move");
        if(motion=="Parried")
        {
            // Contact stays planted during hitstop; recoil peaks over the next three ticks.
            float amount=elapsed<3?elapsed/3f:Mathf.Clamp01(1-(elapsed-3)/12f);
            visual.transform.localPosition=restPosition+new Vector3(parryRecoilSign*.10f*amount,-.02f*amount,0);
            visual.transform.localRotation=Quaternion.Euler(0,0,-parryRecoilSign*8*amount);
        }
        else if(motion!="Death"&&parryRecoilSign!=0)
        {
            visual.transform.localPosition=restPosition;if(!IsLaunched)visual.transform.localRotation=Quaternion.identity;
            parryRecoilSign=0;
        }
        if(motion=="Death")
        {
            if(deathContactTick==CombatTimeController.TickCount){RestoreDeathContact();return;}
            float t=Mathf.Clamp01(elapsed/39f);
            visual.transform.localPosition=restPosition+new Vector3(visual.flipX ? .15f : -.15f,.12f*Mathf.Sin(t*Mathf.PI),0);
            visual.color=new Color(1,1,1,1-Mathf.Clamp01((t-.6f)/.4f));
        }
    }
    private void OnDisable()
    {
        hitbackFrame=hitbackFrames=0;hitbackStartTick=-1;launchHeight=0;
        if(visual!=null){visual.transform.localRotation=Quaternion.identity;visual.transform.localPosition=restPosition;visual.sortingOrder=restSortingOrder;}
        contactDepthTick=-1;
        parryRecoilSign=0;
        if(health!=null) { health.HitReceived-=OnHit;health.Parried-=OnParried;health.Died-=OnDeath;health.ResetPerformed-=OnReset; }
        if(ai!=null) ai.AttackStarted-=OnAttack;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this);
    }
}
