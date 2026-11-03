using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Only the camera owning a live pulse pays for the screen-color copy and distortion pass.</summary>
public sealed class ParryShockwaveFeature : FullScreenPassRendererFeature
{
    public readonly struct Sample
    {
        public readonly object owner;
        public readonly Vector3 worldCenter;
        public readonly float progress, strength, displacementPixels;
        public Sample(object owner, Vector3 center, float progress, float strength, float displacementPixels = 22)
        { this.owner = owner; worldCenter = center; this.progress = progress; this.strength = strength; this.displacementPixels = displacementPixels; }
    }
    private static readonly Dictionary<Camera, Sample> samples = new();
    private readonly Dictionary<Camera, Material> cameraMaterials = new();
    private readonly List<Camera> expiredCameras = new();
    private static readonly int CenterId = Shader.PropertyToID("_ShockwaveCenter");
    private static readonly int PulseId = Shader.PropertyToID("_ShockwavePulse");
    private static readonly int PixelsId = Shader.PropertyToID("_ShockwavePixels");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSamples() => samples.Clear();

    public static bool IsVisible(Camera camera) => camera != null && samples.ContainsKey(camera);
    public static bool IsOwnedBy(Camera camera, object owner) => camera != null &&
        samples.TryGetValue(camera, out var value) && ReferenceEquals(value.owner, owner);
    public static Sample GetSample(Camera camera) => camera != null && samples.TryGetValue(camera, out var value) ? value : default;
    public static void Set(Camera camera, object owner, Vector3 center, float progress, float strength)
        => SetTuned(camera, owner, center, progress, strength, 22);
    public static void SetTuned(Camera camera, object owner, Vector3 center, float progress, float strength, float pixels)
    {
        if (camera == null || owner == null) return;
        samples[camera] = new Sample(owner, center, Mathf.Clamp01(progress), Mathf.Clamp01(strength), pixels);
    }
    public static void Clear(Camera camera, object owner)
    {
        if (ReferenceEquals(camera, null)) return;
        if (samples.TryGetValue(camera, out var value) && ReferenceEquals(value.owner, owner)) samples.Remove(camera);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        foreach(var pair in cameraMaterials) if(pair.Key == null) expiredCameras.Add(pair.Key);
        foreach(var expired in expiredCameras)
        {
            CoreUtils.Destroy(cameraMaterials[expired]);cameraMaterials.Remove(expired);samples.Remove(expired);
        }
        expiredCameras.Clear();
        var camera = renderingData.cameraData.camera;
        if (renderingData.cameraData.cameraType != CameraType.Game || passMaterial == null ||
            !samples.TryGetValue(camera, out var sample) || sample.strength <= 0) return;
        var viewport = camera.WorldToViewportPoint(sample.worldCenter);
        if (viewport.z <= 0) return;
        if (!cameraMaterials.TryGetValue(camera, out var material) || material == null)
        {
            material = new Material(passMaterial) { name = "ParryShockwave (Camera)", hideFlags = HideFlags.HideAndDontSave };
            cameraMaterials[camera] = material;
        }
        material.SetVector(CenterId, new Vector4(viewport.x, viewport.y, 0, 0));
        material.SetVector(PulseId, new Vector4(sample.progress, sample.strength, .065f, .60f));
        material.SetFloat(PixelsId, sample.displacementPixels);
        // Keep render parameters camera-local; never mutate the saved material or global shader state.
        var saved = passMaterial;
        try { passMaterial = material; base.AddRenderPasses(renderer, ref renderingData); }
        finally { passMaterial = saved; }
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var material in cameraMaterials.Values) CoreUtils.Destroy(material);
        cameraMaterials.Clear();
        base.Dispose(disposing);
    }
}
