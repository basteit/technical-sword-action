using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Samples the supplied Aseprite originals in the studio without replacing gameplay artwork.</summary>
public static class VideoStudioReferenceCapture
{
    private const int Fps = 60, Width = 1920, Height = 1080;
    private const float WalkSpeed = 3f, CaptureCameraSize = 5f;
    private const string AsepritePath = "C:/Program Files (x86)/Steam/steamapps/common/Aseprite/Aseprite.exe";
    private const string SourceFolder = "Assets/Art/Player/";
    private static readonly Segment[] Segments =
    {
        new("idle", 72, 0), new("walk", 96, 1), new("idle", 36, 0),
        new("walk", 96, -1), new("idle", 60, 0)
    };

    [Serializable] private sealed class Segment
    {
        public string motion;
        public int frameCount, direction;
        public Segment(string motion, int frameCount, int direction)
        { this.motion=motion; this.frameCount=frameCount; this.direction=direction; }
    }
    [Serializable] private sealed class MotionRecord
    {
        public string source;
        public int[] durationsMs;
    }
    [Serializable] private sealed class Report
    {
        public string scene, takeKind="original-idle-walk", sampling="native milliseconds, sampled at 60 fps";
        public int fps=Fps, width=Width, height=Height, frameCount;
        public double duration;
        public float walkSpeed=WalkSpeed, cameraSize=CaptureCameraSize, finalX;
        public float requestedSpeedMultiplier=1f, baselineMeasuredSpeed, measuredMoveSpeed, actualSpeedMultiplier;
        public Vector2 cameraCenter;
        public string cameraReferenceScene="Assets/Scenes/SampleScene.unity";
        public bool audio=false, originalArtwork=true, sourceFloorGuideHidden=true, standardWalkSpeed;
        public string speedReference;
        public MotionRecord[] motions;
        public Segment[] segments;
    }
    private sealed class Motion : IDisposable
    {
        public Sprite[] Sprites;
        public Texture2D[] Textures;
        public int[] Durations;
        public Sprite Sample(int frame)
        {
            double milliseconds=(frame*1000d/Fps)%Durations.Sum();
            for (int i=0;i<Durations.Length;i++)
            {
                if (milliseconds+1e-7<Durations[i]) return Sprites[i];
                milliseconds-=Durations[i];
            }
            return Sprites[0];
        }
        public void Dispose()
        {
            foreach (var sprite in Sprites) if(sprite!=null) Object.DestroyImmediate(sprite);
            foreach (var texture in Textures) if(texture!=null) Object.DestroyImmediate(texture);
        }
    }

    public static string Capture() => CaptureTake(1f,true);
    // Six screen pixels per source pixel at 1080p, between the close and game-scale takes.
    public static string CaptureMedium() => CaptureTake(1f,true,true);
    // Preserve the previous 3 unit/s comparison reference and its existing output names.
    public static string Capture(float speedMultiplier) => CaptureTake(speedMultiplier,false);
    private static string CaptureTake(float speedMultiplier,bool standard,bool medium=false)
    {
        if(speedMultiplier!=1f && speedMultiplier!=1.5f && speedMultiplier!=2f)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier),"Use 1, 1.5 or 2 times the current preview speed.");
        if (!Application.isPlaying) throw new InvalidOperationException("Open CombatVideoStudio and enter PlayMode first.");
        var studio=Object.FindFirstObjectByType<CombatVideoStudio>();
        if(studio==null || VideoStudioCapture.IsBusy) throw new InvalidOperationException("An idle filming studio is required.");
        string sourceOutput=Path.GetFullPath(".uloop/outputs/idle-walk-reference");
        ExportOriginals(sourceOutput);
        string takeName=medium?"standard-medium":standard?"standard":speedMultiplier==1f?"game-scale":speedMultiplier==1.5f?"speed-1p5x":"speed-2x";
        float cameraSize=medium?Height/(2f*32*6):CaptureCameraSize;
        string output=Path.GetFullPath(".uloop/outputs/video/studio-idle-walk-"+takeName);
        Directory.CreateDirectory(output);
        using var idle=LoadMotion("idle", sourceOutput);
        using var walk=LoadMotion("walk", sourceOutput);
        var clock=Object.FindFirstObjectByType<CombatTimeController>();
        var player=studio.Player;
        var motor=player.GetComponent<PlayerMotor2D>();
        var router=player.GetComponent<PlayerInputRouter>();
        var driver=player.GetComponent<VideoPlayerAnimationDriver>();
        var recovery=player.GetComponent<PlayerVisualRecovery2D>();
        var animator=player.GetComponent<Animator>();
        var visual=player.GetComponent<SpriteRenderer>();
        var body=player.GetComponent<Rigidbody2D>();
        var hidden=studio.Enemies.Select(e=>e.gameObject).Concat(studio.Gimmicks.Select(g=>g.gameObject))
            .Append(studio.CustomContent.gameObject).ToDictionary(g=>g,g=>g.activeSelf);
        bool previousManual=clock.ManualAdvanceOnly, previousFollow=studio.FollowCamera;
        bool previousRouter=router.enabled, previousDriver=driver.enabled, previousRecovery=recovery.enabled;
        bool unmanagedAnimatorEnabled=animator.enabled;
        float previousSize=studio.CameraSize;
        Vector2 previousCenter=studio.FixedCameraCenter;
        var motorSettings=new SerializedObject(motor);
        float previousSpeed=motorSettings.FindProperty("moveSpeed").floatValue;
        var previousInterpolation=body.interpolation;
        var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);
        target.Create();
        var image=new Texture2D(Width,Height,TextureFormat.RGB24,false);
        var previousActive=RenderTexture.active;
        int frame=0;
        float referenceSpeed=standard?PlayerMotor2D.StandardMoveSpeed:WalkSpeed;
        float baselineMeasuredSpeed=0, commandedSpeed=referenceSpeed, walkDistance=0, walkSeconds=0;
        try
        {
            clock.ManualAdvanceOnly=true;
            studio.ResetStudio();
            foreach(var pair in hidden) pair.Key.SetActive(false);
            router.enabled=false; driver.enabled=false; recovery.enabled=false;
            unmanagedAnimatorEnabled=animator.enabled;animator.enabled=false;
            motorSettings.FindProperty("moveSpeed").floatValue=referenceSpeed;
            motorSettings.ApplyModifiedPropertiesWithoutUndo();
            body.interpolation=RigidbodyInterpolation2D.None;
            body.position=new Vector2(0,1); player.transform.position=body.position; motor.SetFacing(1);
            studio.FollowCamera=false; studio.CameraSize=cameraSize;
            studio.FixedCameraCenter=new Vector2(WalkSpeed*Segments.First(s=>s.direction>0).frameCount/Fps/2,1.05f);
            // Measure the baseline after contact settles so labels describe actual movement,
            // including the existing floor friction. All variants keep the same camera.
            for(int warmup=0;warmup<12;warmup++) studio.StepStudio(1d/Fps);
            for(int sample=0;sample<8;sample++)
            { motor.SetMoveInput(Vector2.right);studio.StepStudio(1d/Fps); }
            baselineMeasuredSpeed=Mathf.Abs(body.linearVelocity.x);
            if(baselineMeasuredSpeed<=0) throw new InvalidOperationException("Could not measure baseline walking speed.");
            commandedSpeed=referenceSpeed+baselineMeasuredSpeed*(speedMultiplier-1f);
            if(medium)
                studio.FixedCameraCenter=new Vector2(baselineMeasuredSpeed*Segments.First(s=>s.direction>0).frameCount/Fps/2,1.05f);
            motorSettings.FindProperty("moveSpeed").floatValue=commandedSpeed;
            motorSettings.ApplyModifiedPropertiesWithoutUndo();
            motor.ClearSampledInput();body.linearVelocity=Vector2.zero;
            body.position=new Vector2(0,1);player.transform.position=body.position;motor.SetFacing(1);
            // Settle contact before recording; only the original sprites are sampled below.
            for(int warmup=0;warmup<4;warmup++) studio.StepStudio(1d/Fps);
            foreach(var segment in Segments)
            {
                var motion=segment.motion=="idle"?idle:walk;
                float startX=body.position.x;
                for(int local=0;local<segment.frameCount;local++,frame++)
                {
                    motor.SetMoveInput(Vector2.right*segment.direction);
                    studio.StepStudio(1d/Fps);
                    player.transform.position=body.position;
                    visual.sprite=motion.Sample(local);
                    RenderPipeline.SubmitRenderRequest(studio.StudioCamera,
                        new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                    RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,Width,Height),0,0,false);image.Apply(false);
                    File.WriteAllBytes(Path.Combine(output,$"frame-{frame:D4}.png"),image.EncodeToPNG());
                }
                if(segment.direction!=0)
                { walkDistance+=Mathf.Abs(body.position.x-startX);walkSeconds+=segment.frameCount/(float)Fps; }
            }
            if(Mathf.Abs(body.position.x)>.001f || player.ActionState!=PlayerActionState.Neutral || !motor.IsGrounded)
                throw new InvalidOperationException("Preview failed to finish grounded at its starting position.");
            var report=new Report
            {
                scene=studio.gameObject.scene.path, frameCount=frame, duration=frame/(double)Fps, finalX=body.position.x,
                walkSpeed=commandedSpeed,requestedSpeedMultiplier=speedMultiplier,baselineMeasuredSpeed=baselineMeasuredSpeed,
                measuredMoveSpeed=walkDistance/walkSeconds,actualSpeedMultiplier=walkDistance/walkSeconds/baselineMeasuredSpeed,
                cameraSize=cameraSize,cameraCenter=studio.FixedCameraCenter,
                cameraReferenceScene=medium?"": "Assets/Scenes/SampleScene.unity",
                standardWalkSpeed=standard,speedReference=standard?"approved-2026-10-16":"previous-game-scale",
                motions=new[]
                {
                    new MotionRecord{source=SourceFolder+"Player_idle.aseprite",durationsMs=idle.Durations},
                    new MotionRecord{source=SourceFolder+"Player_walk.aseprite",durationsMs=walk.Durations}
                },
                segments=Segments
            };
            File.WriteAllText(Path.Combine(output,"events.json"),JsonUtility.ToJson(report,true));
            return output;
        }
        finally
        {
            RenderTexture.active=previousActive; Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
            motorSettings.FindProperty("moveSpeed").floatValue=previousSpeed;motorSettings.ApplyModifiedPropertiesWithoutUndo();
            body.interpolation=previousInterpolation;
            animator.enabled=unmanagedAnimatorEnabled;recovery.enabled=previousRecovery;driver.enabled=previousDriver;
            foreach(var pair in hidden) if(pair.Key!=null) pair.Key.SetActive(pair.Value);
            studio.FollowCamera=previousFollow;studio.CameraSize=previousSize;studio.FixedCameraCenter=previousCenter;
            studio.ResetStudio();router.enabled=previousRouter;clock.ManualAdvanceOnly=previousManual;
        }
    }

    private static void ExportOriginals(string output)
    {
        Directory.CreateDirectory(output);
        string root=Path.GetFullPath(".").Replace('\\','/');
        var info=new ProcessStartInfo(AsepritePath)
        {
            Arguments=$"--batch --script-param root=\"{root}\" --script-param out=\"{output.Replace('\\','/')}\" --script-param hide-guides=true --script \"{root}/Tools/Art/inspect-player-reference.lua\"",
            UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden
        };
        using var process=Process.Start(info);
        if(process==null || !process.WaitForExit(20000) || process.ExitCode!=0)
            throw new InvalidOperationException("Aseprite original export failed.");
    }
    private static Motion LoadMotion(string name,string output)
    {
        // Reading timing from the original avoids the old walk reference's rounded 5-tick (83.33ms) duration.
        string path=SourceFolder+"Player_"+name+".aseprite";
        using var reader=new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position=4;
        if(reader.ReadUInt16()!=0xA5E0) throw new InvalidDataException("Invalid Aseprite source: "+path);
        int count=reader.ReadUInt16();reader.BaseStream.Position=18;int defaultDuration=reader.ReadUInt16();
        var motion=new Motion{Sprites=new Sprite[count],Textures=new Texture2D[count],Durations=new int[count]};
        long position=128;
        try
        {
            for(int i=0;i<count;i++)
            {
                reader.BaseStream.Position=position;uint size=reader.ReadUInt32();
                if(size<16 || reader.ReadUInt16()!=0xF1FA) throw new InvalidDataException("Invalid Aseprite frame.");
                reader.ReadUInt16();int duration=reader.ReadUInt16();
                motion.Durations[i]=duration==0?defaultDuration:duration;
                if(motion.Durations[i]<=0) throw new InvalidDataException("Frame duration must be positive.");
                position+=size;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                motion.Textures[i]=texture;
                if(!texture.LoadImage(File.ReadAllBytes(Path.Combine(output,$"Player_{name}_{i+1:D2}.png"))))
                    throw new InvalidDataException("Failed to load Aseprite frame.");
                if(texture.width!=64 || texture.height!=64) throw new InvalidDataException("Expected original 64×64 canvas.");
                motion.Sprites[i]=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,38f/64),32,0,SpriteMeshType.FullRect);
            }
            return motion;
        }
        catch { motion.Dispose();throw; }
    }
}
