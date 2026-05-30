using System.Collections.Generic;
using UnityEngine;

public class DamageSource2D : MonoBehaviour
{
    private enum HitWindowMode
    {
        ManualWindow,
        AlwaysOn
    }

    [SerializeField] private HitWindowMode hitWindowMode = HitWindowMode.ManualWindow;
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float repeatInterval = 0.15f;

    private readonly Dictionary<int, float> lastHitTimeByTarget = new();
    private bool isHitWindowOpen;

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ProcessHit(other);
    }

    public void OpenHitWindow()
    {
        isHitWindowOpen = true;
    }

    public void CloseHitWindow()
    {
        isHitWindowOpen = false;
    }

    public void SetHitWindow(bool open)
    {
        isHitWindowOpen = open;
    }

    private void ProcessHit(Collider2D other)
    {
        if (hitWindowMode == HitWindowMode.ManualWindow && !isHitWindowOpen)
        {
            return;
        }

        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        int id = other.GetInstanceID();
        if (lastHitTimeByTarget.TryGetValue(id, out float lastTime))
        {
            if (Time.time - lastTime < repeatInterval)
            {
                return;
            }
        }

        if (other.TryGetComponent(out IDamageReceiver2D receiver))
        {
            bool applied = receiver.TryReceiveHit(damage, transform.position, knockbackForce);
            if (applied)
            {
                lastHitTimeByTarget[id] = Time.time;
            }
        }
    }
}
