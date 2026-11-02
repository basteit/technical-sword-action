using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class ParryImpactTuningTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private readonly List<Object> owned = new();
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name,Flags).Invoke(target,args);
        private static V Get<V>(object target,string name) => (V)(target.GetType().GetProperty(name,Flags)?.GetValue(target) ?? target.GetType().GetField(name,Flags).GetValue(target));
        private static void Set(object target,string name,object value) => target.GetType().GetField(name).SetValue(target,value);
        private ScriptableObject Profile() {var p=ScriptableObject.CreateInstance(T("CounterCinematicProfile"));owned.Add(p);return p;}
        [TearDown] public void Cleanup(){foreach(var o in owned)if(o!=null)Object.DestroyImmediate(o);owned.Clear();}

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SoftParryLightHasOneLowContrastPeakAndAFiniteSmoothReturn(int fps)
        {
            var p=Profile();float peak=0;int rises=0;bool wasRising=false;float previous=0;
            for(int i=0;i<fps;i++)
            {
                float value=(float)Call(p,"SampleParryLight",i/(float)fps);
                Assert.That(value,Is.InRange(0f,1f));peak=Mathf.Max(peak,value);
                bool rising=value>previous+.0001f;if(rising&&!wasRising)rises++;
                wasRising=rising;previous=value;
                Assert.That(value*Get<float>(p,"parryBackgroundDim"),Is.LessThanOrEqualTo(.4f));
            }
            Assert.That(peak,Is.EqualTo(1).Within(.001f));Assert.That(rises,Is.EqualTo(1));
            Assert.That(Call(p,"SampleParryLight",-1f),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryLight",float.NaN),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryLight",1f),Is.EqualTo(0f));
        }
        [TestCase("parryWaveSeconds",0f)] [TestCase("parryWavePixels",float.NaN)]
        [TestCase("parryBackgroundDim",.9f)] [TestCase("parryLightFadeSeconds",-1f)]
        [TestCase("parrySparkSeconds",0f)] [TestCase("parryGlowSeconds",float.NaN)]
        [TestCase("parrySparkRadius",-1f)] [TestCase("parryActorJitterPixels",5f)] [TestCase("parryLegacyBloomStrength",2f)]
        [TestCase("parryGlowGain",float.NaN)] [TestCase("parryGlowRadius",0f)]
        [TestCase("parryCameraImpactSize",5f)] [TestCase("parryCameraHoldSeconds",-1f)] [TestCase("parryCameraReturnSeconds",0f)]
        [TestCase("parryFlareLength",0f)] [TestCase("parryFlareSeconds",float.NaN)] [TestCase("parryReferenceSparkRadius",-1f)]
        [TestCase("parryCameraArcHeight",-1f)]
        public void InvalidTuningIsRejected(string field,float value)
        {var p=Profile();Set(p,field,value);Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);}

        [Test] public void TunedWaveIsStrongerAndSlowerWithoutChangingTheLegacyDefaults()
        {
            var go=new GameObject("TunedParryCamera");owned.Add(go);var camera=go.AddComponent<Camera>();camera.enabled=false;
            var pulse=Activator.CreateInstance(T("ParryShockwavePulse"));
            try
            {
                Assert.That(Call(pulse,"BeginTuned",camera,Vector3.one,1f,.4f,32f),Is.True);
                Call(pulse,"Advance",.2f,true);
                var sample=T("ParryShockwaveFeature").GetMethod("GetSample").Invoke(null,new object[]{camera});
                Assert.That(Get<float>(sample,"progress"),Is.EqualTo(.5f));Assert.That(Get<float>(sample,"displacementPixels"),Is.EqualTo(32));
                Assert.That(Call(pulse,"Begin",camera,Vector3.one,.65f,.24f),Is.True);
                sample=T("ParryShockwaveFeature").GetMethod("GetSample").Invoke(null,new object[]{camera});
                Assert.That(Get<float>(sample,"displacementPixels"),Is.EqualTo(22));
            }
            finally{Call(pulse,"Reset");}
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDisplacementCannotLeaveAnActivePass(float pixels)
        {
            var go=new GameObject("InvalidParryCamera");owned.Add(go);var camera=go.AddComponent<Camera>();camera.enabled=false;
            var pulse=Activator.CreateInstance(T("ParryShockwavePulse"));
            Assert.That(Call(pulse,"BeginTuned",camera,Vector3.zero,1f,.4f,pixels),Is.False);
            Assert.That(Get<bool>(pulse,"IsActive"),Is.False);
        }
        [TestCase(3,24,0,15)] [TestCase(3,24,2,13)] [TestCase(3,30,0,18)] [TestCase(3,24,30,0)]
        public void EnemyPeakDelayFollowsItsOwnRecoveryTiming(int active,int recovery,int elapsed,int expected)
        {
            var motion=Activator.CreateInstance(T("VideoArtLibrary+Motion"));
            Set(motion,"frames",new Sprite[2]);Set(motion,"durations",new[]{12,12});
            Set(motion,"hasParryImpact",true);Set(motion,"parryImpactFrame",12);
            Assert.That(T("PrototypeEnemyArtPresentation").GetMethod("ParryImpactDelayFrames").Invoke(null,new[]{motion,(object)active,recovery,elapsed}),Is.EqualTo(expected));
        }
        [TestCase(0,15,8,0,0)] [TestCase(0,15,8,4,7)] [TestCase(0,15,8,7,13)]
        [TestCase(0,15,8,8,15)] [TestCase(0,15,8,10,17)]
        [TestCase(2,13,8,8,15)] [TestCase(0,15,0,0,15)] [TestCase(0,15,30,15,15)]
        public void ShorterApproachReachesTheSameEnemyPeakAndThenResumesNormalDrawing(int captured,int natural,int display,int elapsed,int expected)
        {
            Assert.That(T("PrototypeEnemyArtPresentation").GetMethod("SampleParryDeflectionFrame").Invoke(null,new object[]{captured,natural,display,elapsed}),Is.EqualTo(expected));
        }
        [Test] public void LatestTrialDefaultsHaveAFortyPercentDimAndAShorterStopAndApproach()
        {
            var p=Profile();Assert.That(Get<int>(p,"parryTrialHitstopFrames"),Is.EqualTo(30));
            Assert.That(Get<int>(p,"parryApproachMaxFrames"),Is.EqualTo(8));
            Assert.That(Get<float>(p,"parryBackgroundDim"),Is.EqualTo(.4f));
            Set(p,"parryApproachMaxFrames",-1);Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ActorJitterAlternatesOnRealTimePixelStepsAndHasAFiniteEnvelope(int fps)
        {
            var p=Profile();bool positive=false,negative=false;
            for(int i=0;i<fps;i++)
            {
                float age=i/(float)fps,value=(float)Call(p,"SampleParryJitter",age,1f);
                Assert.That(Mathf.Abs(value),Is.LessThanOrEqualTo(2f/32));
                Assert.That(value*32,Is.EqualTo(Mathf.Round(value*32)));
                if(age>=.5f)Assert.That(value,Is.Zero);else{positive|=value>0;negative|=value<0;}
            }
            Assert.That(positive&&negative,Is.True);Assert.That(Call(p,"SampleParryJitter",0f,.5f),Is.EqualTo(1f/32));
            Assert.That(Call(p,"SampleParryJitter",0f,0f),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryJitter",float.NaN,1f),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryJitter",-1f,1f),Is.EqualTo(0f));
        }
        [Test] public void InvalidAccentColorOrJitterStepIsRejected()
        {
            var p=Profile();Set(p,"parrySparkColor",new Color(float.NaN,0,0,1));
            Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);Set(p,"parrySparkColor",new Color(1,.36f,.035f,1));
            Set(p,"parryActorJitterStepFrames",0);Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);
        }
        [Test] public void AdditionalOrangeGeometryMirrorsWithoutChangingItsWorldContactOrLeakingToAnotherCamera()
        {
            var go=new GameObject("OrangeParryCamera");owned.Add(go);var camera=go.AddComponent<Camera>();camera.enabled=false;
            var other=new GameObject("OtherParryCamera");owned.Add(other);var otherCamera=other.AddComponent<Camera>();otherCamera.enabled=false;
            var material=new Material(Shader.Find("TechnicalSwordAction/VideoPixelGlow"));owned.Add(material);
            var accent=go.AddComponent(T("ParryOrangeImpact"));Call(accent,"Configure",camera,material);
            var p=Profile();var contact=new Vector2(2,3);
            Call(accent,"Begin",contact,1,p);Call(accent,"Advance",0f,1f,1f);
            var mesh=Get<Mesh>(accent,"sparkMesh");var right=mesh.vertices;
            Assert.That(Get<int>(accent,"ActiveRendererCount"),Is.EqualTo(3));
            Assert.That(Get<int>(accent,"ParticleCount"),Is.EqualTo(32));
            Assert.That(Array.Exists(mesh.colors,c=>c.r>.9f&&c.g>.2f&&c.g<.5f&&c.b<.1f),Is.True);
            Call(accent,"BeforeCamera",default(UnityEngine.Rendering.ScriptableRenderContext),otherCamera);
            Assert.That(Get<MeshRenderer>(accent,"sparks").enabled,Is.False);
            Call(accent,"BeforeCamera",default(UnityEngine.Rendering.ScriptableRenderContext),camera);
            Assert.That(Get<MeshRenderer>(accent,"sparks").enabled,Is.True);
            Assert.That((Vector2)Get<MeshRenderer>(accent,"sparks").transform.position,Is.EqualTo(contact));
            Call(accent,"AfterCamera",default(UnityEngine.Rendering.ScriptableRenderContext),camera);
            Assert.That(Get<MeshRenderer>(accent,"sparks").enabled,Is.False);
            Call(accent,"Begin",contact,-1,p);Call(accent,"Advance",0f,1f,1f);var left=mesh.vertices;
            for(int i=0;i<right.Length;i++)
            {
                // Mirroring reverses quad winding too, so compare corresponding end-pair bounds.
                Assert.That(float.IsFinite(left[i].x)&&float.IsFinite(left[i].y),Is.True);
            }
            Assert.That(mesh.bounds.center.x,Is.EqualTo(-BoundsOf(right).center.x).Within(1f/64));
            Call(accent,"Advance",.4f,1f,1f);Assert.That(Get<int>(accent,"ActiveRendererCount"),Is.Zero);
        }
        private static Bounds BoundsOf(Vector3[] points)
        {var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);return bounds;}
        [TestCase(1)] [TestCase(-1)]
        public void ExpressiveCameraUsesAnArcAndDelayedZoomButStillEndsExactlyOnContact(int direction)
        {
            var p=Profile();var from=new Vector3(0,3,-10);var to=new Vector3(1.4f*direction,1.2f,-10);
            Assert.That(Call(p,"SampleParryCameraPosition",from,to,0,8),Is.EqualTo(from));
            Assert.That(Call(p,"SampleParryCameraSize",5.625f,0,8),Is.EqualTo(5.625f));
            Assert.That(Call(p,"SampleParryCameraSize",5.625f,2,8),Is.EqualTo(5.625f));
            var middle=(Vector3)Call(p,"SampleParryCameraPosition",from,to,3,8);
            Assert.That(middle.x,Is.EqualTo(.7f*direction).Within(.0001f));
            Assert.That(middle.y,Is.EqualTo(Vector3.Lerp(from,to,.5f).y+.2f).Within(.0001f));
            Assert.That(Call(p,"SampleParryCameraPosition",from,to,6,8),Is.EqualTo(to));
            Assert.That((float)Call(p,"SampleParryCameraSize",5.625f,6,8),Is.GreaterThan(4.25f));
            Assert.That(Call(p,"SampleParryCameraSize",5.625f,8,8),Is.EqualTo(4.25f));
            Assert.That((float)Call(p,"SampleParryCameraReturn",.5f),Is.EqualTo(.875f).Within(.0001f));
            Assert.That(Call(p,"SampleParryCameraReturn",1f),Is.EqualTo(1f));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)]
        public void ShortOrMissingPreparationStillHasFiniteExactCameraEndpoints(int duration)
        {
            var p=Profile();var from=new Vector3(0,3,-10);var to=new Vector3(1,1,-10);
            Assert.That(Call(p,"SampleParryCameraPosition",from,to,duration,duration),Is.EqualTo(to));
            Assert.That(Call(p,"SampleParryCameraSize",5.625f,duration,duration),Is.EqualTo(4.25f));
        }
        [Test] public void InvalidCameraCurveIsRejectedWithoutReturningANonfinitePose()
        {
            var p=Profile();Set(p,"parryCameraPositionCurve",new AnimationCurve(new Keyframe(0,0),new Keyframe(1,float.NaN)));
            Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);
            var value=(Vector3)Call(p,"SampleParryCameraPosition",Vector3.zero,Vector3.one,4,8);
            Assert.That(float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z),Is.True);
        }
    }
}
