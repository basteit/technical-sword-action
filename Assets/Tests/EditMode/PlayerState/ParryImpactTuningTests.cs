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
        private readonly List<Object> owned = new();
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target,args);
        private static V Get<V>(object target,string name) => (V)(target.GetType().GetProperty(name)?.GetValue(target) ?? target.GetType().GetField(name).GetValue(target));
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
                Assert.That(value*Get<float>(p,"parryBackgroundDim"),Is.LessThanOrEqualTo(.3f));
            }
            Assert.That(peak,Is.EqualTo(1).Within(.001f));Assert.That(rises,Is.EqualTo(1));
            Assert.That(Call(p,"SampleParryLight",-1f),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryLight",float.NaN),Is.EqualTo(0f));
            Assert.That(Call(p,"SampleParryLight",1f),Is.EqualTo(0f));
        }
        [TestCase("parryWaveSeconds",0f)] [TestCase("parryWavePixels",float.NaN)]
        [TestCase("parryBackgroundDim",.9f)] [TestCase("parryLightFadeSeconds",-1f)]
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
        [Test] public void LatestTrialDefaultsHaveAThirtyPercentDimAndAShorterStopAndApproach()
        {
            var p=Profile();Assert.That(Get<int>(p,"parryTrialHitstopFrames"),Is.EqualTo(36));
            Assert.That(Get<int>(p,"parryApproachMaxFrames"),Is.EqualTo(8));
            Assert.That(Get<float>(p,"parryBackgroundDim"),Is.EqualTo(.3f));
            Set(p,"parryApproachMaxFrames",-1);Assert.That(Get<bool>(p,"IsParryTuningValid"),Is.False);
        }
    }
}
