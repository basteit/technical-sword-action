using UnityEngine;

/// <summary>Opt-in reset support for hand-placed content under CustomContent.</summary>
public interface IStudioResettable { void ResetStudioState(); }

[DisallowMultipleComponent]
public sealed class StudioResetAnchor : MonoBehaviour
{
    private Vector3 position, scale;
    private Quaternion rotation;
    private bool active, captured;
    private float gravity;
    private int hp;
    private Rigidbody2D body;
    private Collider2D[] colliders;
    private bool[] colliderStates;
    private void Awake() { if (!captured) Capture(); }
    public void Capture()
    {
        position = transform.localPosition; rotation = transform.localRotation; scale = transform.localScale;
        active = gameObject.activeSelf; body = GetComponent<Rigidbody2D>();
        if (body != null) gravity = body.gravityScale;
        var health = GetComponent<Damageable2D>(); if (health != null) hp = health.MaxHp;
        colliders=GetComponentsInChildren<Collider2D>(true);colliderStates=new bool[colliders.Length];
        for(int i=0;i<colliders.Length;i++)colliderStates[i]=colliders[i].enabled;
        captured = true;
    }
    public void Restore(Transform player)
    {
        if (!captured) Capture();
        gameObject.SetActive(active);
        transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale;
        if (body != null) { body.position = transform.position; body.linearVelocity = Vector2.zero; body.angularVelocity=0; body.gravityScale=gravity; }
        GetComponent<Damageable2D>()?.ResetForCapture(transform.position, hp);
        GetComponent<EnemyMeleeChaser2D>()?.ResetForCapture(player, false);
        for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=colliderStates[i];
        foreach (var component in GetComponents<MonoBehaviour>())
            if (component is IStudioResettable resettable) resettable.ResetStudioState();
        gameObject.SetActive(active);
    }
}
