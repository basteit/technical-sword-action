using UnityEngine;

/// <summary>Finite, unscaled presentation clock. Never requests combat time or changes a camera transform.</summary>
public sealed class ParryShockwavePulse
{
    public const float DefaultDuration = .24f;
    private Camera camera;
    private Vector3 center;
    private float strength, duration, pixels;
    public float Elapsed { get; private set; }
    public bool IsActive => camera != null && ParryShockwaveFeature.IsOwnedBy(camera, this);

    public bool Begin(Camera target, Vector3 worldCenter, float intensity, float seconds = DefaultDuration)
        => BeginTuned(target, worldCenter, intensity, seconds, 22);

    public bool BeginTuned(Camera target, Vector3 worldCenter, float intensity, float seconds, float displacementPixels)
    {
        Reset();
        if (target == null || !float.IsFinite(intensity) || intensity <= 0 ||
            !float.IsFinite(seconds) || seconds <= 0 || !float.IsFinite(worldCenter.x) ||
            !float.IsFinite(worldCenter.y) || !float.IsFinite(worldCenter.z) ||
            !float.IsFinite(displacementPixels) || displacementPixels <= 0) return false;
        camera = target; center = worldCenter; strength = Mathf.Clamp01(intensity); duration = seconds;
        pixels = displacementPixels;
        ParryShockwaveFeature.SetTuned(camera, this, center, 0, strength, pixels);
        return true;
    }

    public void Advance(float realSeconds, bool enabled = true)
    {
        if (!enabled || !IsActive) { Reset(); return; }
        if (CombatTimeController.IsPaused || !float.IsFinite(realSeconds) || realSeconds < 0) return;
        Elapsed += realSeconds;
        if (Elapsed >= duration) { Reset(); return; }
        ParryShockwaveFeature.SetTuned(camera, this, center, Elapsed / duration, strength, pixels);
    }

    public void Reset()
    {
        ParryShockwaveFeature.Clear(camera, this);
        camera = null; Elapsed = strength = duration = pixels = 0;
    }
}
