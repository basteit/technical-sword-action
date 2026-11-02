using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Records the real prototype AI twice; only refraction strength differs between the takes.</summary>
public static class ParryShockwaveCaptureUtility
{
    public static string Prepare() => PrototypeImpactCaptureUtility.Prepare();
    public static string Capture(float strength = .65f, string revision = "20261102-v2")
    {
        if (!Application.isPlaying || !float.IsFinite(strength) || strength < 0 || strength > 1)
            throw new InvalidOperationException("PlayMode and a strength in 0..1 are required.");
        var feedback = UnityEngine.Object.FindFirstObjectByType<CombatFeedback2D>();
        if (feedback == null) throw new InvalidOperationException("Open the prototype scene.");
        string key = "strength-" + Mathf.RoundToInt(strength * 100).ToString("D3");
        if(string.IsNullOrEmpty(revision) || Array.Exists(revision.ToCharArray(),c=>!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-')))
            throw new InvalidOperationException("Use an ASCII revision name.");
        string folder = Path.GetFullPath(".uloop/outputs/video/parry-shockwave-" + revision + "/" + key);
        if (Directory.Exists(folder)) throw new InvalidOperationException("Preserve earlier captures; move the owned output aside before recording again.");
        float saved = feedback.Settings.parryShockwave;
        try
        {
            feedback.Settings.parryShockwave = strength;
            string source = PrototypeImpactCaptureUtility.Capture();
            Directory.CreateDirectory(folder);
            var recording = JsonUtility.FromJson<Recording>(File.ReadAllText(Path.Combine(source,"events.json")));
            for(int i=0;i<recording.frameCount;i++)
            {
                string file=$"frame-{i:D4}.png";
                File.Copy(Path.Combine(source,file),Path.Combine(folder,file),false);
            }
            foreach(string file in new[]{"events.json","trace.csv"}) File.Copy(Path.Combine(source,file),Path.Combine(folder,file),false);
            File.WriteAllText(Path.Combine(folder,"shockwave.json"),JsonUtility.ToJson(new Report{strength=strength,duration=ParryShockwavePulse.DefaultDuration},true));
            return folder;
        }
        finally { feedback.Settings.parryShockwave = saved; feedback.ResetPresentation(); }
    }
    [Serializable] private sealed class Report
    { public float strength,duration; public string source="prototype-real-ai-parry-and-counter"; public bool changesCombatRules=false; }
    [Serializable] private sealed class Recording { public int frameCount; }

    public static string ValidateProjection()
    {
        if(!Application.isPlaying) throw new InvalidOperationException("PlayMode required.");
        var camera=Camera.main;
        var clock=UnityEngine.Object.FindFirstObjectByType<CombatTimeController>();
        bool manual=clock.ManualAdvanceOnly;clock.ManualAdvanceOnly=true;
        var pulse=new ParryShockwavePulse();
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        GameObject otherObject=null;
        try
        {
            var baseline=ReadCamera(camera,target,image);
            var contact=camera.ViewportToWorldPoint(new Vector3(.58f,.30f,10));
            pulse.Begin(camera,contact,.65f);pulse.Advance(.02f);
            var warped=ReadCamera(camera,target,image);
            otherObject=new GameObject("UnaffectedShockwaveCamera");
            var other=otherObject.AddComponent<Camera>();other.CopyFrom(camera);other.enabled=false;
            other.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
            var unaffected=ReadCamera(other,target,image);int otherCameraChanged=0;
            for(int i=0;i<baseline.Length;i++)if(!baseline[i].Equals(unaffected[i]))otherCameraChanged++;
            Vector2 center=new Vector2(.58f*1920,.30f*1080),mirrored=new Vector2(center.x,1080-center.y);
            int correct=0,wrong=0,changed=0;float radius=194*194;
            for(int y=0;y<1080;y++)for(int x=0;x<1920;x++)
            {
                int i=y*1920+x;
                if(Mathf.Abs(baseline[i].r-warped[i].r)+Mathf.Abs(baseline[i].g-warped[i].g)+Mathf.Abs(baseline[i].b-warped[i].b)<8)continue;
                changed++;if((new Vector2(x,y)-center).sqrMagnitude<radius)correct++;
                if((new Vector2(x,y)-mirrored).sqrMagnitude<radius)wrong++;
            }
            pulse.Reset();var restored=ReadCamera(camera,target,image);int residue=0;
            for(int i=0;i<baseline.Length;i++)if(!baseline[i].Equals(restored[i]))residue++;
            if(correct<100 || correct<wrong*3 || residue!=0 || otherCameraChanged!=0)
                throw new InvalidOperationException($"Projection/residue check failed: correct={correct}, mirrored={wrong}, residue={residue}, otherCamera={otherCameraChanged}");
            return JsonUtility.ToJson(new ProjectionReport{correctCenterPixels=correct,mirroredCenterPixels=wrong,changedPixels=changed,residualPixels=residue,otherCameraChangedPixels=otherCameraChanged},true);
        }
        finally
        {
            pulse.Reset();RenderTexture.active=previous;
            if(otherObject!=null)UnityEngine.Object.DestroyImmediate(otherObject);
            UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
            clock.ManualAdvanceOnly=manual;
        }
    }
    private static Color32[] ReadCamera(Camera camera,RenderTexture target,Texture2D image)
    {
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1920,1080),0,0,false);image.Apply(false);
        return image.GetPixels32();
    }
    [Serializable]private sealed class ProjectionReport
    {public int correctCenterPixels,mirroredCenterPixels,changedPixels,residualPixels,otherCameraChangedPixels;}
}
