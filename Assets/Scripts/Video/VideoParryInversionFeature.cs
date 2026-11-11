using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in, camera-local impact pulse; inactive cameras enqueue no extra render pass.</summary>
public sealed class VideoParryInversionFeature : FullScreenPassRendererFeature
{
    private static readonly HashSet<Camera> activeCameras=new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearCameras()=>activeCameras.Clear();

    public static void SetVisible(Camera camera,bool visible)
    {
        if(ReferenceEquals(camera,null))return;
        if(visible&&camera!=null)activeCameras.Add(camera);
        else activeCameras.Remove(camera);
    }

    public static bool IsVisible(Camera camera)=>camera!=null&&activeCameras.Contains(camera);

    public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
    {
        if(renderingData.cameraData.cameraType!=CameraType.Game||!IsVisible(renderingData.cameraData.camera))return;
        base.AddRenderPasses(renderer,ref renderingData);
    }
}
