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
    private bool deathFacingLeft;
    private const int DefeatFrames=39;
    private const int DefeatKnockbackFrames=18;
    public const float DefaultSwordDefeatDistance=.4f;
    private int restSortingOrder;
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
        { animator.speed=PlaybackSpeed(name);animator.Play("EnemyVideo_"+name,0,0);animator.Update(0); }
        Show();
    }
    private float PlaybackSpeed(string name)
    {
        if(name=="Parried"||name=="Death") return 0;
        var clip=library!=null?library.Find("Enemy_"+name):null;
        return name=="Windup"&&clip!=null&&ai!=null&&ai.WindupDuration>0?
            clip.TotalFrames*CombatTimeController.StepSeconds/ai.WindupDuration:1f;
    }
    private void OnHit(Damageable2D source,int damage,Vector2 direction) { if(source.IsAlive) Play("Hit"); }
    private void OnParried(Damageable2D source,ParryResult result)
    {
        Play("Parried");
    }
    private void OnDeath(Damageable2D source,Vector2 direction)
    {
        // Use the same authored reaction from contact onward. Animator evaluation may
        // temporarily restore its old cel before this event, so do not capture that cel.
        deathContactSprite=library?.Find("Enemy_Death")?.At(0)??visual.sprite;
        deathContactTick=CombatTimeController.TickCount;
        deathContactPosition=restPosition+Vector3.down*(2f/32);deathContactRotation=Quaternion.identity;
        deathFacingLeft=player!=null?player.position.x<transform.position.x:direction.x>0;
        visual.flipX=deathFacingLeft;
        Play("Death");RestoreDeathContact();
    }
    private void OnAttack()=>Play("Attack");
    private void OnReset(Damageable2D source)
    {
        hitbackFrame=hitbackFrames=0;hitbackStartTick=-1;LastHitbackDistance=launchHeight=LastLaunchHeight=0;
        deathContactSprite=null;deathContactTick=-1;deathFacingLeft=false;
        visual.sortingOrder=restSortingOrder;
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
            if (motion=="Attack" && elapsed<(library?.Find("Enemy_Attack")?.TotalFrames??16)) desired="Attack";
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
    public void ApplySwordHitback(int step,Vector2 direction)
    {
        step=Mathf.Clamp(step,1,4);float distance=new[]{.10f,.18f,.30f,.58f}[step-1];int frames=6+step;
        // Retain the first enemy's small hop, with a little more backward travel.
        if(!health.IsAlive) ApplyDefeatKnockback(distance+.22f,.12f,direction);
        else ApplyHitback(distance,frames,direction);
    }
    public void ApplyDefeatKnockback(float distance,float height,Vector2 direction)
    {
        ApplyLaunch(distance,height,DefeatKnockbackFrames,new Vector2(direction.x<0?-1:1,0));
    }
    public void CombatTickTimers()
    {
        // Animator evaluation runs after CombatTick; keep both reactions on the down cel.
        if(motion=="Parried"||motion=="Death") Show();
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
        // Keep attack trails in front; contact pose/position still hold during hitstop.
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
    }
    private void Show()
    {
        if(library==null || visual==null) return;
        bool heldReaction=motion=="Parried"||motion=="Death";
        var clip=library.Find("Enemy_"+(heldReaction?"Death":motion));
        if(clip!=null&&(heldReaction||animator==null||animator.runtimeAnimatorController==null))
            visual.sprite=clip.At(heldReaction?0:Mathf.FloorToInt(elapsed*PlaybackSpeed(motion)),motion=="Idle"||motion=="Move");
        if(heldReaction)
        {
            if(motion=="Death"&&deathContactTick==CombatTimeController.TickCount){RestoreDeathContact();return;}
            // The submitted down cel's lowest pixel is two pixels above idle/chance.
            visual.transform.localPosition=restPosition+Vector3.down*(2f/32);
            visual.transform.localRotation=Quaternion.identity;
        }
        else
        {
            visual.transform.localPosition=restPosition;visual.transform.localRotation=Quaternion.identity;
        }
        if(motion=="Death")
        {
            float t=Mathf.Clamp01(elapsed/(float)DefeatFrames);
            visual.flipX=deathFacingLeft;
            visual.color=new Color(1,1,1,1-Mathf.Clamp01((t-.6f)/.4f));
        }
    }
    private void OnDisable()
    {
        if(animator!=null)animator.speed=1;
        hitbackFrame=hitbackFrames=0;hitbackStartTick=-1;launchHeight=0;
        if(visual!=null){visual.transform.localRotation=Quaternion.identity;visual.transform.localPosition=restPosition;visual.sortingOrder=restSortingOrder;}
        if(health!=null) { health.HitReceived-=OnHit;health.Parried-=OnParried;health.Died-=OnDeath;health.ResetPerformed-=OnReset; }
        if(ai!=null) ai.AttackStarted-=OnAttack;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this);
    }
}
