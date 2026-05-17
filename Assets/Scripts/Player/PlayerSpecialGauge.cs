using UnityEngine;

public class PlayerSpecialGauge : MonoBehaviour
{
    [Header("Gauge")]
    [SerializeField] private float maxGauge = 100f;
    [SerializeField] private float startGauge = 0f;

    [Header("Gain")]
    [SerializeField] private float gainOnAttackHit = 6f;
    [SerializeField] private float gainOnDamaged = 10f;
    [SerializeField] private float gainOnParryNormal = 12f;
    [SerializeField] private float gainOnParryJust = 20f;

    public float CurrentGauge { get; private set; }
    public float MaxGauge => maxGauge;
    public float GaugeRate => maxGauge <= 0f ? 0f : CurrentGauge / maxGauge;

    private void Awake()
    {
        CurrentGauge = Mathf.Clamp(startGauge, 0f, maxGauge);
    }

    public void AddOnAttackHit()
    {
        AddGauge(gainOnAttackHit);
    }

    public void AddOnDamaged()
    {
        AddGauge(gainOnDamaged);
    }

    public void AddOnParry(ParryResult result)
    {
        if (result == ParryResult.Just)
        {
            AddGauge(gainOnParryJust);
        }
        else if (result == ParryResult.Normal)
        {
            AddGauge(gainOnParryNormal);
        }
    }

    public bool Consume(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (CurrentGauge < amount)
        {
            return false;
        }

        CurrentGauge -= amount;
        return true;
    }

    private void AddGauge(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CurrentGauge = Mathf.Clamp(CurrentGauge + amount, 0f, maxGauge);
    }
}

