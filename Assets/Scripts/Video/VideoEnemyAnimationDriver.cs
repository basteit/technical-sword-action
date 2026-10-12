using UnityEngine;

public sealed class VideoEnemyAnimationDriver : MonoBehaviour, ICombatTickListener
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
    public string MotionName=>motion;
    public int CombatTickOrder=>320;
    private void OnEnable()
    {
        health=GetComponent<Damageable2D>();ai=GetComponent<EnemyMeleeChaser2D>();visual=GetComponentInChildren<SpriteRenderer>();
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
    private void OnParried(Damageable2D source,ParryResult result)=>Play("Parried");
    private void OnDeath(Damageable2D source,Vector2 direction)=>Play("Death");
    private void OnAttack()=>Play("Attack");
    private void OnReset(Damageable2D source)
    {
        visual.transform.localPosition=restPosition;visual.transform.localRotation=Quaternion.identity;visual.color=Color.white;
        if(animator!=null) animator.Rebind();Play("Idle");
    }
    public void CombatTick()
    {
        if (health.IsAlive)
        {
            string desired=health.IsStunned?(motion=="Hit"?"Hit":"Parried"):
                ai.IsWindingUp?"Windup":ai.CurrentStateName=="Approach"?"Move":"Idle";
            if (motion=="Attack" && elapsed<16) desired="Attack";
            if (motion=="Hit" && elapsed<6) desired="Hit";
            if(desired!=motion) Play(desired);
            if(player!=null) visual.flipX=player.position.x<transform.position.x;
        }
        elapsed++;Show();
    }
    private void Show()
    {
        if(library==null || visual==null) return;
        var clip=library.Find("Enemy_"+motion);
        if(clip!=null&&(animator==null||animator.runtimeAnimatorController==null)) visual.sprite=clip.At(elapsed,motion=="Idle"||motion=="Move");
        if(motion=="Death")
        {
            float t=Mathf.Clamp01(elapsed/39f);
            visual.transform.localPosition=restPosition+new Vector3(visual.flipX ? .15f : -.15f,.12f*Mathf.Sin(t*Mathf.PI),0);
            visual.color=new Color(1,1,1,1-Mathf.Clamp01((t-.6f)/.4f));
        }
    }
    private void OnDisable()
    {
        if(health!=null) { health.HitReceived-=OnHit;health.Parried-=OnParried;health.Died-=OnDeath;health.ResetPerformed-=OnReset; }
        if(ai!=null) ai.AttackStarted-=OnAttack;CombatTimeController.Unregister(this);CombatTimeController.UnregisterAnimator(animator,this);
    }
}
