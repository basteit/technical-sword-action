using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageSource2D : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private LayerMask targetLayers;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        if (other.TryGetComponent(out IDamageReceiver2D receiver))
        {
            receiver.TryReceiveHit(damage, transform.position, knockbackForce);
        }
    }
}
