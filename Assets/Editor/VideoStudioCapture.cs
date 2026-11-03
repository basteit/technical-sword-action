using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

/// <summary>Clean camera capture, outside Game View UI. Encoding runs off the editor thread.</summary>
[InitializeOnLoad]
public static class VideoStudioCapture
{
    private sealed class Packet { public byte[] pixels; public int copies; }
    private sealed class Readback { public AsyncGPUReadbackRequest request; public int copies; }
    [Serializable] public sealed class Report
    {
        public string output,preset,error;
        public int width=1920,height=1080,fps=60,frames,repeatedFrames,scheduledFrames;
        public double durationSeconds,observedSeconds;
        public bool silent=true,playerArtworkIsRough=true;
    }
    private static CombatVideoStudio bound;
    private static RenderTexture target;
    private static BlockingCollection<Packet> queue;
    private static readonly Queue<Readback> pending=new();
    private static Process encoder;
    private static Task worker;
    private static string workerError;
    private static Report report;
    private static int previousFrameRate,previousVsync;
    private static bool recording;
    private static double elapsed,startTime,limitSeconds;
    public static float TimeLimit {get=>EditorPrefs.GetFloat("TSA.VideoStudio.TimeLimit",0);set=>EditorPrefs.SetFloat("TSA.VideoStudio.TimeLimit",value);}
    public static bool IsRecording=>recording;
    public static bool IsBusy=>recording||worker!=null;
    public static string Status {get;private set;}="1080p / 60fps / 無音";
    public static string LastOutput {get;private set;}
    public static string OutputFolder
    {
        get=>Path.GetFullPath(EditorPrefs.GetString("TSA.VideoStudio.Output","output/video-studio"));
        set=>EditorPrefs.SetString("TSA.VideoStudio.Output",Path.GetFullPath(value));
    }
    public static string FFmpegPath
    {
        get
        {
            string configured=EditorPrefs.GetString("TSA.VideoStudio.FFmpeg","");
            if(File.Exists(configured)) return configured;
            string bundled=".uloop/outputs/python-deps/imageio_ffmpeg/binaries";
            if(Directory.Exists(bundled))
            { string candidate=Directory.GetFiles(bundled,"ffmpeg*.exe").FirstOrDefault();if(candidate!=null)return Path.GetFullPath(candidate); }
            foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
            { string candidate=Path.Combine(folder.Trim('"'),"ffmpeg.exe");if(File.Exists(candidate))return candidate; }
            return "";
        }
        set=>EditorPrefs.SetString("TSA.VideoStudio.FFmpeg",value);
    }
    static VideoStudioCapture()
    {
        EditorApplication.update+=Update;
        EditorApplication.playModeStateChanged+=OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload+=Shutdown;
        EditorApplication.quitting+=Shutdown;
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingPlayMode) StopRecording();
        if(state==PlayModeStateChange.EnteredEditMode) Bind(null);
    }
    private static void Bind(CombatVideoStudio studio)
    {
        if(bound!=null)
        { bound.FramePresented-=OnFrame;bound.CaptureToggleRequested-=Toggle;bound.ScreenshotRequested-=ScreenshotHotkey; }
        bound=studio;
        if(bound!=null)
        { bound.FramePresented+=OnFrame;bound.CaptureToggleRequested+=Toggle;bound.ScreenshotRequested+=ScreenshotHotkey; }
    }
    private static void Update()
    {
        if(Application.isPlaying && bound==null) Bind(Object.FindFirstObjectByType<CombatVideoStudio>());
        if(recording && bound==null) StopRecording();
        if(recording)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.isPaused)StopRecording();
            DrainReadbacks();
            if(workerError!=null) StopRecording();
        }
        if(worker!=null && worker.IsCompleted) Finish();
    }
    private static void Toggle()
    {
        if(recording) StopRecording();
        else if(!IsBusy) TryStart(bound);
    }
    private static void ScreenshotHotkey()
    { if(bound!=null) SaveScreenshot(bound); }
    public static void TryStart(CombatVideoStudio studio)
    {
        try { StartRecording(studio,TimeLimit); }
        catch(Exception error) { Status=error.Message;Debug.LogWarning("Video Studio: "+error.Message); }
    }
    public static void StartRecording(CombatVideoStudio studio,double maximumSeconds=0)
    {
        if(IsBusy) throw new InvalidOperationException("書き出し終了を待ってください。");
        if(!Application.isPlaying || studio==null) throw new InvalidOperationException("撮影スタジオをPlayしてください。");
        if(EditorApplication.isPaused) throw new InvalidOperationException("EditorのPauseを解除してください。");
        string ffmpeg=FFmpegPath;
        if(string.IsNullOrEmpty(ffmpeg)) throw new InvalidOperationException("撮影パネルでFFmpeg本体を選択してください。");
        if(!SystemInfo.supportsAsyncGPUReadback) throw new InvalidOperationException("このGPUでは非同期録画を利用できません。静止画は保存できます。");
        Bind(studio);Directory.CreateDirectory(OutputFolder);
        EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.QueuePlayerLoopUpdate();
        string output=Path.Combine(OutputFolder,"studio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".mp4");
        var start=new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true,
            Arguments="-hide_banner -loglevel error -f rawvideo -pixel_format rgba -video_size 1920x1080 -framerate 60 -i pipe:0 -an -vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+output+"\""
        };
        encoder=new Process{StartInfo=start};encoder.Start();
        var errorTask=encoder.StandardError.ReadToEndAsync();
        queue=new BlockingCollection<Packet>(12);pending.Clear();workerError=null;elapsed=0;
        startTime=Time.realtimeSinceStartupAsDouble;limitSeconds=Math.Max(0,maximumSeconds);
        report=new Report{output=output,preset=studio.CurrentPreset!=null?studio.CurrentPreset.displayName:"Custom"};
        target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();
        previousFrameRate=Application.targetFrameRate;previousVsync=QualitySettings.vSyncCount;
        Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        worker=Task.Run(()=>
        {
            try
            {
                foreach(var packet in queue.GetConsumingEnumerable())
                {
                    for(int i=0;i<packet.copies;i++)
                    { encoder.StandardInput.BaseStream.Write(packet.pixels,0,packet.pixels.Length);report.frames++; }
                    report.repeatedFrames+=packet.copies-1;
                }
                encoder.StandardInput.Close();
                if(!encoder.WaitForExit(15000)) { encoder.Kill();throw new IOException("FFmpegの終了待機がタイムアウトしました。"); }
                string error=errorTask.GetAwaiter().GetResult();
                if(encoder.ExitCode!=0) throw new IOException(error);
            }
            catch(Exception error) { workerError=error.Message; }
        });
        recording=true;Status="録画中 0.00秒 / F9で終了";
    }
    // Sample count follows real presentation time, including slow and hitstop. Low render FPS
    // repeats the latest sample rather than shortening the clip; the report exposes every repeat.
    public static int FramesDue(double seconds,int alreadyScheduled) => Math.Max(0,(int)Math.Floor(seconds*60+1e-6)-alreadyScheduled);
    private static void OnFrame(double seconds)
    {
        if(!recording || seconds<=0) return;
        // A first Game View update can include time spent in the editor before Record.
        // Measure from the recording request, never accumulate that stale delta.
        elapsed=Time.realtimeSinceStartupAsDouble-startTime;
        if(limitSeconds>0)elapsed=Math.Min(elapsed,limitSeconds);
        int copies=FramesDue(elapsed,report.scheduledFrames);if(copies==0)return;
        if(pending.Count>=8) { workerError="GPU読戻しが追いつかないため録画を停止しました。";StopRecording();return; }
        try
        {
            Render(bound.StudioCamera,target);
            pending.Enqueue(new Readback{request=AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32),copies=copies});
            report.scheduledFrames+=copies;
            DrainReadbacks();Status=$"録画中 {elapsed:0.00}秒 / F9で終了";
            if(limitSeconds>0&&elapsed>=limitSeconds)StopRecording();
        }
        catch(Exception error) { workerError=error.Message;StopRecording(); }
    }
    private static void DrainReadbacks()
    {
        while(pending.Count>0 && pending.Peek().request.done)
        {
            var sample=pending.Dequeue();
            if(sample.request.hasError) { workerError="GPU読戻しに失敗しました。";return; }
            var packet=new Packet{pixels=sample.request.GetData<byte>().ToArray(),copies=sample.copies};
            if(!queue.TryAdd(packet)) { workerError="エンコーダが追いつかないため録画を停止しました。";return; }
        }
    }
    public static void StopRecording()
    {
        if(!recording)return;
        recording=false;
        Application.targetFrameRate=previousFrameRate;QualitySettings.vSyncCount=previousVsync;
        AsyncGPUReadback.WaitAllRequests();DrainReadbacks();pending.Clear();
        target.Release();Object.DestroyImmediate(target);target=null;
        report.observedSeconds=elapsed;
        queue.CompleteAdding();Status="MP4を仕上げています…";
    }
    private static void Finish()
    {
        if(workerError==null&&report.frames==0)workerError="フレームを取得できませんでした。Game画面を動かして再収録してください。";
        report.error=workerError??"";report.durationSeconds=report.frames/60d;
        LastOutput=report.output;
        File.WriteAllText(Path.ChangeExtension(report.output,".json"),JsonUtility.ToJson(report,true));
        Status=workerError!=null?"録画失敗: "+workerError:$"保存完了 {report.durationSeconds:0.00}秒 / 重複 {report.repeatedFrames}F";
        encoder.Dispose();queue.Dispose();worker=null;encoder=null;queue=null;
    }
    private static void Shutdown()
    {
        StopRecording();
        if(worker!=null)
        {
            if(!worker.Wait(5000))
            {
                workerError="Editor終了によって録画を中断しました。";
                try { encoder.Kill(); } catch(InvalidOperationException) { }
                worker.Wait(1000);
            }
            if(worker.IsCompleted) Finish();
        }
        Bind(null);
    }
    private static void Render(Camera camera,RenderTexture destination)
    {
        float previous=camera.aspect;
        try
        {
            camera.aspect=16f/9f;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=destination});
        }
        finally { camera.aspect=previous; }
    }
    public static string SaveScreenshot(CombatVideoStudio studio)
    {
        if(studio==null) throw new ArgumentNullException(nameof(studio));
        Directory.CreateDirectory(OutputFolder);
        string path=Path.Combine(OutputFolder,"studio-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");
        var texture=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);texture.Create();
        var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try
        {
            Render(studio.StudioCamera,texture);RenderTexture.active=texture;
            pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            LastOutput=path;Status="静止画を保存しました";return path;
        }
        finally { RenderTexture.active=previous;texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(pixels); }
    }
}
