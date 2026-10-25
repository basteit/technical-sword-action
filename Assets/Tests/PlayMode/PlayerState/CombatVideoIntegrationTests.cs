using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class CombatVideoIntegrationTests
    {
        private Component director,clock,state,counter,health,presentation;
        private Type clockType;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        [UnitySetUp] public IEnumerator Setup()
        {
            clockType=Type.GetType("CombatTimeController, Assembly-CSharp",true);
            clock=(Component)Object.FindFirstObjectByType(clockType);Set(clock,"ManualAdvanceOnly",true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideo.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            BindScene();
        }
        private void BindScene()
        {
            director=Find("CombatVideoDirector");state=(Component)Get<object>(director,"Player");
            counter=state.GetComponent(Type.GetType("PlayerParryCounter2D, Assembly-CSharp",true));
            health=state.GetComponent(Type.GetType("PlayerDamageReceiver2D, Assembly-CSharp",true));
            presentation=Find("VideoCombatPresentation");
        }
        [TestCase(false)] [TestCase(true)]
        public void WiderCaptureLayoutUsesTheSuppliedIdleWithoutChangingItsScaleOrTiming(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"ResetTake",2);int sign=mirrored?-1:1;
            Assert.That(state.transform.position,Is.EqualTo(new Vector3(sign,1,0)));
            Assert.That(((Component)Get<object>(director,"FrontEnemy")).transform.position,Is.EqualTo(new Vector3(2.85f*sign,1,0)));
            Assert.That(((Component)Get<object>(director,"RearEnemy")).transform.position,Is.EqualTo(new Vector3(-2.2f*sign,1,0)));
            var visual=state.GetComponent<SpriteRenderer>();
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var idle=Call(Get<object>(driver,"Art"),"Find","IdleReference");
            Assert.That(Get<int[]>(idle,"durations"),Is.EqualTo(new[]{6,6,6,6,6,6}));
            Assert.That(Get<Sprite[]>(idle,"frames").Length,Is.EqualTo(6));
            var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<36;i++)
            {
                Step();Assert.That(visual.sprite.name,Does.StartWith("IdleReference_"));seen.Add(visual.sprite.name);
                Assert.That(visual.sprite.rect.size,Is.EqualTo(new Vector2(64,64)));
                Assert.That(visual.sprite.pivot,Is.EqualTo(new Vector2(32,38)));
                Assert.That(visual.sprite.pixelsPerUnit,Is.EqualTo(32));
            }
            Assert.That(seen.Count,Is.EqualTo(6));
            Assert.That(state.transform.position.x,Is.EqualTo(sign).Within(.001f));
        }
        [UnityTearDown] public IEnumerator Teardown()
        { Call(director,"ResetTake",2);Set(clock,"ManualAdvanceOnly",false);yield return null; }
        [Test] public void TwoAndFourStagesAndBothDirectionsResetTenTimesEach()
        {
            var utility=Type.GetType("VideoCaptureUtility, Assembly-CSharp-Editor",true);
            string report=(string)utility.GetMethod("ValidateResetRuns").Invoke(null,null);
            Assert.That(report,Does.Contain("\"runsPassed\": 40"));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ActualScenePreservesHitsTargetsAndTimeAtEveryRenderRate(int fps)
        {
            Call(director,"StartTake",2,true);
            for(int frame=0;frame<fps*6&&!Get<bool>(director,"Completed");frame++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<int>((Component)Get<object>(director,"FrontEnemy"),"HitCount"),Is.EqualTo(2));
            Assert.That(Get<int>((Component)Get<object>(director,"RearEnemy"),"HitCount"),Is.EqualTo(1));
            Assert.That(Get<double>(director,"RealElapsed"),Is.InRange(3d,6d));
            Assert.That(Get<float>(presentation,"ChargeRealElapsed"),Is.InRange(.98f,1.05f));
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void RearEnemyApproachesFromTheOpeningAndOverlapsItsFullAttackWithTheFrontCombo(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            Assert.That(Get<bool>(director,"UsesRearEntrance"),Is.True);
            var rear=(Component)Get<object>(director,"RearEnemy");var rearVisual=rear.GetComponentInChildren<SpriteRenderer>();
            var ai=rear.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true));
            float originX=state.transform.position.x,rearX=rear.transform.position.x;bool walked=false,framed=false;
            for(int i=0;i<fps*6&&!Get<bool>(director,"Completed");i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(Get<string>(ai,"CurrentStateName")=="Approach"&&Mathf.Abs(rear.transform.position.x-rearX)>.1f)
                {walked=true;Assert.That(rearVisual.sprite.name,Does.StartWith("EnemySample_walk_"));}
                if(!Marks().Any(m=>Get<string>(m,"kind")=="CounterStart"))
                    Assert.That(state.transform.position.x,Is.EqualTo(originX).Within(.002f));
                if(Get<bool>(counter,"IsActive")&&Get<int>(counter,"ElapsedFrames")>=Get<int>(counter,"BlinkArrivalFrame"))
                {
                    var bounds=state.GetComponent<SpriteRenderer>().bounds;
                    float left=Camera.main.WorldToViewportPoint(new Vector3(bounds.min.x,state.transform.position.y,0)).x;
                    float right=Camera.main.WorldToViewportPoint(new Vector3(bounds.max.x,state.transform.position.y,0)).x;
                    Assert.That(left,Is.GreaterThanOrEqualTo(.01f),"The entire counter canvas needs a left margin.");
                    Assert.That(right,Is.LessThanOrEqualTo(.99f),"The mirrored counter needs the same right margin.");framed=true;
                }
            }
            Assert.That(walked&&framed&&Get<bool>(director,"Completed"),Is.True);
            var marks=Marks().ToArray();
            object Mark(string kind,string target=null)=>marks.Single(m=>Get<string>(m,"kind")==kind&&(target==null||Get<string>(m,"target")==target));
            Assert.That(Get<long>(Mark("RearApproach"),"tick"),Is.LessThan(Get<long>(Mark("Slash1"),"tick")));
            Assert.That(Get<long>(Mark("RearApproach"),"tick"),Is.LessThan(Get<long>(Mark("Death","Enemy_A"),"tick")));
            Assert.That(Get<long>(Mark("RearWindup"),"tick"),Is.LessThan(Get<long>(Mark("Death","Enemy_A"),"tick")));
            Assert.That(Get<long>(Mark("Parry"),"tick"),Is.GreaterThan(Get<long>(Mark("RearWindup"),"tick")));
            Assert.That(Get<string>(Mark("Kick"),"target"),Is.EqualTo(Get<string>(Mark("Parry"),"target")));
            Assert.That(Get<double>(Mark("RearWindup"),"realSeconds"),Is.InRange(.4d,.7d));
            Assert.That(Get<double>(Mark("Parry"),"realSeconds"),Is.InRange(2.05d,2.3d));
            Assert.That(Get<double>(Mark("Parry"),"realSeconds")-Get<double>(Mark("Death","Enemy_A"),"realSeconds"),Is.LessThan(1.3d));
        }
        [TestCase(false)] [TestCase(true)]
        public void SubmittedEnemyShowsTheCompleteStartupFlashAndLungeBeforeTheParryContact(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var rear=(Component)Get<object>(director,"RearEnemy");
            var visual=rear.GetComponentInChildren<SpriteRenderer>();
            var ai=rear.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true));
            int peakFrames=0,lungeFrames=0;long firstPeak=-1,lastPeak=-1;
            var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<240&&!Marks().Any(m=>Get<string>(m,"kind")=="Parry");i++)
            {
                Step();
                if(visual.sprite.name.StartsWith("EnemySample_attack_"))seen.Add(visual.sprite.name);
                if(visual.sprite.name=="EnemySample_attack_12")lungeFrames++;
                if(visual.sprite.name!="EnemySample_attack_08")continue;
                Assert.That(Get<bool>(ai,"IsWindingUp"),Is.True,"The mouth flash is a visible warning, before the damaging attack.");
                long tick=(long)clockType.GetProperty("TickCount").GetValue(null);
                if(firstPeak<0)firstPeak=tick;lastPeak=tick;peakFrames++;
            }
            Assert.That(peakFrames,Is.GreaterThanOrEqualTo(5),"Show the submitted flash at its native duration.");
            Assert.That(lungeFrames,Is.GreaterThanOrEqualTo(4),"The lunge must be visible before it is parried.");
            for(int frame=1;frame<=12;frame++)Assert.That(seen,Does.Contain($"EnemySample_attack_{frame:00}"));
            var parry=Marks().Single(m=>Get<string>(m,"kind")=="Parry");
            long parryTick=Get<long>(parry,"tick");
            // Capture starts after the global clock's reset; compare relative ticks.
            long origin=Get<long>(director,"startTick");
            Assert.That(parryTick,Is.GreaterThan(lastPeak-origin));
            Assert.That(parryTick-(firstPeak-origin),Is.GreaterThanOrEqualTo(6));
            Assert.That(Get<string>(parry,"target"),Is.EqualTo("Enemy_B"));
            var windup=Marks().Single(m=>Get<string>(m,"kind")=="RearWindup");
            Assert.That(parryTick-Get<long>(windup,"tick"),Is.EqualTo(95),"Keep the full 96-tick attack, including its first pose.");
            Assert.That(Get<double>(parry,"realSeconds")-Get<double>(windup,"realSeconds"),Is.InRange(1.58d,1.7d));
        }
        [TestCase(false)] [TestCase(true)]
        public void ResetDuringTheRearWalkRestoresAiSettingsPositionsAndPause(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var rear=(Component)Get<object>(director,"RearEnemy");var ai=rear.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true));
            Vector3 origin=rear.transform.position;
            float speed=Get<float>(ai,"moveSpeed"),reach=Get<float>(ai,"attackRange"),preferred=Get<float>(ai,"preferredMaxDistance"),windup=Get<float>(ai,"attackWindupDuration");
            for(int i=0;i<160&&Mathf.Abs(rear.transform.position.x-origin.x)<.2f;i++)Step();
            Assert.That(Mathf.Abs(rear.transform.position.x-origin.x),Is.GreaterThan(.2f));
            Vector3 walking=rear.transform.position;clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<12;i++)Step();Assert.That(rear.transform.position,Is.EqualTo(walking));
            Call(director,"ResetTake",2);
            Assert.That(rear.transform.position,Is.EqualTo(origin));
            Assert.That(Get<float>(ai,"moveSpeed"),Is.EqualTo(speed));Assert.That(Get<float>(ai,"attackRange"),Is.EqualTo(reach));
            Assert.That(Get<float>(ai,"preferredMaxDistance"),Is.EqualTo(preferred));
            Assert.That(Get<float>(ai,"attackWindupDuration"),Is.EqualTo(windup),"Capture startup timing must not remain after reset.");
            Assert.That(Get<string>(ai,"CurrentStateName"),Is.EqualTo("Idle"));
            Assert.That(Get<Vector3>(director,"InitialPlayerPosition"),Is.EqualTo(state.transform.position));
            Assert.That((bool)clockType.GetProperty("IsPaused").GetValue(null),Is.False);
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void CounterPreparationShowsEveryChargingPoseBeforeTheExtension(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var visual=state.GetComponent<SpriteRenderer>();
            var exposure=new System.Collections.Generic.Dictionary<string,double>();
            for(int i=0;i<fps*6&&!Get<bool>(counter,"HasReleasedCharge");i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(!Get<bool>(presentation,"IsCharged")) continue;
                Assert.That((float)clockType.GetProperty("SlowScale").GetValue(null),Is.EqualTo(.15f),"The slow lease must last through every preparation pose.");
                string pose=visual.sprite.name;
                exposure.TryGetValue(pose,out double seconds);exposure[pose]=seconds+1d/fps;
            }
            Assert.That(Get<bool>(counter,"HasReleasedCharge"),Is.True);
            Assert.That(exposure.Count,Is.EqualTo(8),"Include the final tucked cel before release.");
            int[] ticks={2,1,1,1,1,1,1,1};
            // Sprite sampling crosses the fixed 60Hz Animator update as well as the render frame.
            double tolerance=Math.Max(1d/fps,1d/60)+.001d;
            for(int i=0;i<ticks.Length;i++)
                Assert.That(exposure["CounterKick_"+(i+1).ToString("00")],Is.EqualTo(ticks[i]/9d).Within(tolerance));
            Assert.That(exposure["CounterKick_01"],Is.GreaterThan(exposure["CounterKick_07"]*1.5));
            Assert.That((float)clockType.GetProperty("SlowScale").GetValue(null),Is.EqualTo(1));
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void CounterFollowThroughShowsEveryCelAtFullSpeed(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var visual=state.GetComponent<SpriteRenderer>();
            var exposure=new System.Collections.Generic.Dictionary<string,double>();
            for(int i=0;i<fps*6&&!Get<bool>(director,"Completed");i++)
            {
                Call(director,"StepCapture",1d/fps);
                string pose=visual.sprite.name;
                if(!pose.StartsWith("CounterKick_")||!int.TryParse(pose.Substring("CounterKick_".Length),out int cel)||cel<10) continue;
                Assert.That((float)clockType.GetProperty("SlowScale").GetValue(null),Is.EqualTo(1),"Only the cel exposure is extended; full-speed gameplay must resume.");
                Assert.That((bool)clockType.GetProperty("IsHitStopped").GetValue(null),Is.False,"Do not lengthen the kick by adding more hitstop.");
                exposure.TryGetValue(pose,out double seconds);exposure[pose]=seconds+1d/fps;
            }
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(exposure.Count,Is.EqualTo(6),"Show every follow-through and settling cel.");
            int[] ticks={4,4,4,4,4,3};double tolerance=Math.Max(1d/fps,1d/60)+.001d;
            for(int i=0;i<ticks.Length;i++)
                Assert.That(exposure["CounterKick_"+(i+10).ToString("00")],Is.EqualTo(ticks[i]/60d).Within(tolerance));
            Assert.That(Get<int>((Component)Get<object>(director,"RearEnemy"),"HitCount"),Is.EqualTo(1));
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void CounterShowsChargeReleaseAndContactPosesOnTheirEvents(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var visual=state.GetComponent<SpriteRenderer>();
            bool charge=false,release=false,hit=false;int previousHits=Get<int>(counter,"HitCount");
            for(int frame=0;frame<fps*6&&!hit;frame++)
            {
                Call(director,"StepCapture",1d/fps);
                if(!charge&&Get<bool>(presentation,"IsCharged"))
                {
                    charge=true;
                    Assert.That(visual.enabled,Is.True);
                    Assert.That(visual.sprite.name,Is.EqualTo("CounterKick_01"),"Slow must show the first supplied preparation cel.");
                }
                if(!release&&Get<bool>(counter,"HasReleasedCharge"))
                {
                    release=true;
                    // A 30fps render can observe the impact after advancing two combat frames.
                    int releaseFrame=Get<int>(counter,"ElapsedFrames");
                    Assert.That(releaseFrame,Is.InRange(21,22));
                    Assert.That(visual.sprite.name,Is.EqualTo(releaseFrame==21?"CounterKick_08":"CounterKick_09"),"The held last tucked cel releases into the contact cel.");
                }
                if(Get<int>(counter,"HitCount")>previousHits)
                {
                    hit=true;
                    Assert.That(visual.sprite.name,Is.EqualTo("CounterKick_09"),"Hitstop must hold the supplied contact pose.");
                    Vector2 shake=Get<Vector2>(presentation,"ShakeOffset");
                    Assert.That(Mathf.Abs(shake.x),Is.GreaterThan(Mathf.Abs(shake.y)),"The lateral sample must drive a lateral impact.");
                    Call(director,"StepCapture",1d/fps);
                    Assert.That(visual.sprite.name,Is.EqualTo("CounterKick_09"));
                }
            }
            Assert.That(charge&&release&&hit,Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void EverySwordHitHoldsItsContactPoseInsteadOfItsWindup(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"FrontEnemy");int checkedHits=0;
            string[] expected={"VerticalSlash_05","HorizontalSlash_04","RapidSheathe_05","IaiSlash_03"};
            for(int i=0;i<220&&checkedHits<4;i++)
            {
                Step();int hits=Get<int>(enemy,"HitCount");
                if(hits<=checkedHits) continue;
                Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo(expected[hits-1]));
                checkedHits=hits;
            }
            Assert.That(checkedHits,Is.EqualTo(4));
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void LightParryFreezesItsAuthoredDrawingWithoutLegacyBurstsThenResets(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            for(int i=0;i<fps*5&&Get<int>(parry,"SuccessCount")==0;i++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(1));
            var visual=state.GetComponent<SpriteRenderer>();
            Assert.That(visual.sprite.name,Is.EqualTo("ParrySuccessLight_01"));
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var enemy=((Component)Get<object>(director,"RearEnemy")).GetComponentInChildren<SpriteRenderer>();
            var incoming=enemy.sprite;
            Assert.That(incoming.name,Is.Not.EqualTo("EnemySample_down_01"));
            Assert.That((bool)clockType.GetProperty("IsHitStopped").GetValue(null),Is.False);
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Assert.That(Get<Vector2>(presentation,"ShakeOffset"),Is.EqualTo(Vector2.zero));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Get<int>(driver,"ParryImpactCount"),Is.Zero);
            Assert.That(enemy.sprite,Is.SameAs(incoming));
            Assert.That(visual.sprite.name,Is.EqualTo("ParrySuccessLight_01"));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<fps&&Get<int>(driver,"ParryImpactCount")==0;i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(Get<int>(driver,"ParryImpactCount")==0)Assert.That(enemy.sprite,Is.SameAs(incoming));
            }
            Assert.That(Get<int>(driver,"ParryImpactCount"),Is.EqualTo(1));
            Assert.That(visual.sprite.name,Is.EqualTo("ParrySuccessLight_03"));
            Assert.That(enemy.sprite.name,Is.EqualTo("EnemySample_down_01"));
            Assert.That(Get<Vector2>(presentation,"ShakeOffset").magnitude,Is.EqualTo(.22f).Within(.00001f),"The impulse peaks on cel 3.");
            var success=Marks().Single(m=>Get<string>(m,"kind")=="Parry");
            var impact=Marks().Single(m=>Get<string>(m,"kind")=="ParryImpact");
            Assert.That(Get<long>(impact,"tick")-Get<long>(success,"tick"),Is.EqualTo(3));
            var vfx=Find("VideoVfxRenderer");
            Assert.That(vfx.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.sprite!=null&&r.sprite.name.StartsWith("Fx_Parry")),Is.False);
            Assert.That(Get<int>(vfx,"ActiveCount"),Is.Zero,"The parry spark comes from the submitted cels, without legacy sprite/glow/particles.");
            int facing=mirrored?1:-1;
            Vector3 contact=state.transform.position+new Vector3(facing*45/32f,6/32f,0);
            Assert.That(Vector2.Distance(Get<Vector2>(driver,"ParryContactPoint"),contact),Is.LessThan(.001f));
            Assert.That(visual.sprite.rect.size,Is.EqualTo(new Vector2(128,128)));
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero,"The backlight uses its own camera-local backdrop.");
            var position=Camera.main.transform.position;
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++) Call(director,"StepCapture",1d/fps);
            Assert.That(visual.sprite.name,Is.EqualTo("ParrySuccessLight_03"));
            Assert.That(Camera.main.transform.position,Is.EqualTo(position));
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            Call(director,"ResetTake",4);
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ParryBacklightLastsFourRealVideoFramesWhileContactStaysFrozen(int fps)
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<fps*3&&!Get<bool>(presentation,"IsParryBacklit");i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);
            long impactTick=(long)clockType.GetProperty("TickCount").GetValue(null);
            int exposed=0;
            while(Get<bool>(presentation,"IsParryBacklit")&&exposed<fps)
            {
                exposed++;Call(director,"StepCapture",1d/fps);
                Assert.That((long)clockType.GetProperty("TickCount").GetValue(null),Is.EqualTo(impactTick));
                Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo("ParrySuccessLight_03"));
            }
            Assert.That(exposed,Is.EqualTo(fps/15),"4/60 seconds, independent of the nine-frame combat stop.");
            Assert.That((int)clockType.GetProperty("HitstopRemainingTicks").GetValue(null),Is.GreaterThan(0));
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("SessionReset")] [TestCase("TargetLost")]
        public void ActiveParryBacklightClearsImmediatelyOnResetOrDisable(string interruption)
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<180&&!Get<bool>(presentation,"IsParryBacklit");i++)Step();
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);
            if(interruption=="Disable")((Behaviour)presentation).enabled=false;
            else if(interruption=="SessionReset")clockType.GetMethod("ResetSession").Invoke(null,null);
            else if(interruption=="TargetLost"){((Component)Get<object>(presentation,"BacklitEnemy")).gameObject.SetActive(false);Step();}
            else Call(director,"ResetTake",2);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
            Call(presentation,"AdvanceVisuals",1/60f);
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.False);
        }
        [TestCase(false,true,true)] [TestCase(true,true,true)] [TestCase(false,false,true)] [TestCase(false,true,false)]
        public void ParryBacklightWhitensBothActorsKeepsGoldAndLeavesOtherCamerasUntouched(bool mirrored,bool hybrid,bool keepGold)
        {
            presentation.GetType().GetField("preserveParrySparkColor",Flags).SetValue(presentation,keepGold);
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,hybrid);
            for(int i=0;i<180&&!Get<bool>(presentation,"IsParryBacklit");i++)Step();
            Assert.That(Get<bool>(presentation,"IsParryBacklit"),Is.True);
            var camera=Camera.main;
            Assert.That(Get<bool>(presentation,"IsParryInverted"),Is.False);
            Assert.That(Get<object>(presentation,"BacklitEnemy"),Is.SameAs(Get<object>(director,"RearEnemy")));
            var light=camera.GetComponent(Type.GetType("VideoParryBacklight, Assembly-CSharp",true));
            Assert.That(Get<bool>(light,"IsHaloVisible"),Is.EqualTo(hybrid));
            var other=new GameObject("UnaffectedCamera").AddComponent<Camera>();other.CopyFrom(camera);other.enabled=false;
            other.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
            try
            {
                var lit=RenderParryPixels(camera);
                var unaffected=RenderParryPixels(other);
                Call(light,"ResetLight");
                var normal=RenderParryPixels(camera);
                Assert.That(lit[0].r+lit[0].g+lit[0].b,Is.LessThan(100),"The distant background must darken.");
                int gold=0;
                for(int i=0;i<normal.Length;i++)
                {
                    var source=normal[i];var bright=lit[i];
                    Assert.That(unaffected[i],Is.EqualTo(source),"Other cameras keep their original lighting.");
                    if(source.r>200&&source.g>110&&source.b<140&&source.g>source.r*.45f)
                    {
                        gold++;
                        if(keepGold)Assert.That(Math.Abs(bright.r-source.r)+Math.Abs(bright.g-source.g)+Math.Abs(bright.b-source.b),Is.LessThanOrEqualTo(9),"Keep the authored yellow spark.");
                        else Assert.That(bright.r>180&&bright.g>180&&bright.b>180&&Math.Abs(bright.r-bright.b)<30,Is.True,"The comparison may whiten the spark without changing its drawing.");
                    }
                }
                Assert.That(gold,Is.GreaterThan(2));
                foreach(var actor in new[]{state.GetComponent<SpriteRenderer>(),((Component)Get<object>(director,"RearEnemy")).GetComponentInChildren<SpriteRenderer>()})
                {
                    var min=camera.WorldToViewportPoint(actor.bounds.min);var max=camera.WorldToViewportPoint(actor.bounds.max);int colored=0,white=0;
                    for(int y=Mathf.Max(0,(int)(min.y*180));y<Mathf.Min(180,(int)(max.y*180));y++)
                    for(int x=Mathf.Max(0,(int)(min.x*320));x<Mathf.Min(320,(int)(max.x*320));x++)
                    {
                        int i=y*320+x;var source=normal[i];var bright=lit[i];
                        if(source.b>source.r+15&&source.b>source.g)
                        {colored++;if(bright.r>180&&bright.g>180&&bright.b>180&&bright.b-bright.r<30)white++;}
                    }
                    Assert.That(colored,Is.GreaterThan(4));Assert.That(white,Is.GreaterThan(colored*.7f),"Both the player and the parried enemy must read as white-lit silhouettes.");
                }
            }
            finally
            {
                Object.DestroyImmediate(other.gameObject);
                Call(director,"ResetTake",2);
            }
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void CounterLightingFadesInRealTimeAcrossHitstopAndLaunch(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var victim=(Component)Get<object>(director,"RearEnemy");float strongestCharge=0;
            var light=Camera.main.GetComponent(Type.GetType("VideoCounterLighting, Assembly-CSharp",true));
            for(int i=0;i<fps*6&&Get<int>(victim,"HitCount")==0;i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(Get<string>(light,"Phase")=="Charge")strongestCharge=Mathf.Max(strongestCharge,Get<float>(light,"ChargeProgress"));
            }
            Assert.That(strongestCharge,Is.GreaterThan(.8f));
            Assert.That(Get<string>(light,"Phase"),Is.EqualTo("Flash"));Assert.That(Get<int>(light,"ImpactFrame"),Is.Zero);
            Assert.That(Get<object>(light,"Target"),Is.SameAs(victim));
            var visual=state.GetComponent<SpriteRenderer>();var pose=visual.sprite;var point=victim.transform.position;
            long tick=(long)clockType.GetProperty("TickCount").GetValue(null);
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Get<int>(light,"ImpactFrame"),Is.Zero);
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            int exposed=0;bool faded=false,launched=false;float previousWeight=1;
            while(Get<bool>(light,"IsVisible")&&exposed<fps)
            {
                faded|=Get<string>(light,"Phase")=="Fade";
                float weight=Get<float>(light,"ImpactWeight");Assert.That(weight,Is.LessThanOrEqualTo(previousWeight+.0001f));previousWeight=weight;
                if((bool)clockType.GetProperty("IsHitStopped").GetValue(null))
                {Assert.That(visual.sprite,Is.SameAs(pose));Assert.That(victim.transform.position,Is.EqualTo(point));Assert.That((long)clockType.GetProperty("TickCount").GetValue(null),Is.EqualTo(tick));}
                else if(victim.transform.position!=point)launched=true;
                exposed++;Call(director,"StepCapture",1d/fps);
            }
            Assert.That(faded&&launched,Is.True);Assert.That(exposed/(float)fps,Is.EqualTo(24/60f).Within(.0001f));
            Assert.That(Get<object>(light,"Target"),Is.Null);Assert.That(Get<int>(victim,"HitCount"),Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void CounterRimBreathesSlowlyWhileFootLightHasNoProceduralRays(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            for(int i=0;i<360&&Get<string>(presentation,"CounterLightPhase")!="Charge";i++)Step();
            var camera=Camera.main;var light=camera.GetComponent(Type.GetType("VideoCounterLighting, Assembly-CSharp",true));
            var target=(Component)Get<object>(director,"RearEnemy");
            var playerVisual=state.GetComponent<SpriteRenderer>();var enemyVisual=target.GetComponentInChildren<SpriteRenderer>();
            int facing=Get<int>(counter,"KickFacingSign");
            try
            {
                Call(light,"ResetLight");Call(light,"Sample",.2f,true,true,1f,target,facing);
                Assert.That(Get<float>(light,"RimPulse"),Is.EqualTo(1).Within(.001f));var peak=RenderParryPixels(camera);
                Call(light,"Sample",.4f,true,true,1f,target,facing);
                Assert.That(Get<float>(light,"RimPulse"),Is.EqualTo(.15f).Within(.001f));var dim=RenderParryPixels(camera);
                Call(light,"Sample",.4f,true,true,1f,target,facing);var nextPeak=RenderParryPixels(camera);
                int changed=0;
                for(int i=0;i<peak.Length;i++)
                {
                    if(Math.Abs(peak[i].r-dim[i].r)+Math.Abs(peak[i].g-dim[i].g)+Math.Abs(peak[i].b-dim[i].b)>3)changed++;
                    Assert.That(nextPeak[i],Is.EqualTo(peak[i]),"The rim returns to the same brightness after one 0.8-second cycle.");
                }
                Assert.That(changed,Is.GreaterThan(20));
                playerVisual.enabled=enemyVisual.enabled=false;
                var foot=RenderParryPixels(camera);var center=(Vector2)light.GetType().GetField("contact",Flags).GetValue(light);
                Call(light,"ResetLight");var normal=RenderParryPixels(camera);int localLight=0;
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)
                {
                    int i=y*320+x;int delta=Math.Abs(foot[i].r-normal[i].r)+Math.Abs(foot[i].g-normal[i].g)+Math.Abs(foot[i].b-normal[i].b);
                    var world=camera.ViewportToWorldPoint(new Vector3((x+.5f)/320,(y+.5f)/180,1));
                    if(Vector2.Distance(world,center)>1.1f)Assert.That(delta,Is.LessThanOrEqualTo(3),"No converging or outward lines may appear outside the local foot light.");
                    else if(delta>3)localLight++;
                }
                Assert.That(localLight,Is.GreaterThan(5),"The original soft auxiliary foot light stays visible.");
            }
            finally{playerVisual.enabled=enemyVisual.enabled=true;Call(director,"ResetTake",2);}
        }
        [TestCase("Reset",false)] [TestCase("Disable",false)] [TestCase("SessionReset",false)] [TestCase("TargetLost",false)]
        [TestCase("Reset",true)] [TestCase("Disable",true)] [TestCase("SessionReset",true)] [TestCase("TargetLost",true)]
        public void CounterLightingClearsOnInterruptionsAndDoesNotResurrect(string interruption,bool impact)
        {
            Call(director,"StartTake",2,true);
            string wanted=impact?"Flash":"Charge";
            for(int i=0;i<360&&Get<string>(presentation,"CounterLightPhase")!=wanted;i++)Step();
            Assert.That(Get<string>(presentation,"CounterLightPhase"),Is.EqualTo(wanted));
            var light=Camera.main.GetComponent(Type.GetType("VideoCounterLighting, Assembly-CSharp",true));
            var victim=(Component)Get<object>(light,"Target");
            if(interruption=="Disable")((Behaviour)presentation).enabled=false;
            else if(interruption=="SessionReset")clockType.GetMethod("ResetSession").Invoke(null,null);
            else if(interruption=="TargetLost"){victim.gameObject.SetActive(false);Step();victim.gameObject.SetActive(true);}
            else Call(director,"ResetTake",2);
            Assert.That(Get<bool>(light,"IsVisible"),Is.False);
            Call(presentation,"AdvanceVisuals",1/60f);
            Assert.That(Get<bool>(light,"IsVisible"),Is.False);Assert.That(Get<object>(light,"Target"),Is.Null);
        }
        [TestCase(false)] [TestCase(true)]
        public void CounterLightingUsesBlueLightOnItsOwnCameraAndKeepsOriginalPixelsOnOtherCameras(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            for(int i=0;i<360&&Get<string>(presentation,"CounterLightPhase")!="Flash";i++)Step();
            Assert.That(Get<string>(presentation,"CounterLightPhase"),Is.EqualTo("Flash"));
            var camera=Camera.main;var light=camera.GetComponent(Type.GetType("VideoCounterLighting, Assembly-CSharp",true));
            var other=new GameObject("CounterUnaffectedCamera").AddComponent<Camera>();other.CopyFrom(camera);other.enabled=false;
            other.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
            try
            {
                var lit=RenderParryPixels(camera);var unaffected=RenderParryPixels(other);
                Call(light,"ResetLight");var normal=RenderParryPixels(camera);
                Assert.That(lit[0].r+lit[0].g+lit[0].b,Is.LessThan(110));
                int bluer=0;
                for(int i=0;i<normal.Length;i++)
                {
                    Assert.That(unaffected[i],Is.EqualTo(normal[i]));
                    if(lit[i].b>80&&lit[i].b-lit[i].r>normal[i].b-normal[i].r+20)bluer++;
                }
                Assert.That(bluer,Is.GreaterThan(30),"The blue light must be visible beyond the existing palette.");
            }
            finally {Object.DestroyImmediate(other.gameObject);Call(director,"ResetTake",2);}
        }
        [TestCase(false,true)] [TestCase(true,false)]
        public void PurePixelAndComparisonSwitchCanKeepCounterLightingOff(bool hybrid,bool enabled)
        {
            presentation.GetType().GetField("counterLightingEnabled",Flags).SetValue(presentation,enabled);
            Call(director,"StartTake",2,hybrid);
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)
            {Step();Assert.That(Get<bool>(presentation,"IsCounterLightVisible"),Is.False);}
            Assert.That(Get<bool>(director,"Completed"),Is.True);
        }

        private static Color32[] RenderParryPixels(Camera camera)
        {
            var requestType=Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipeline+SingleCameraRequest, Unity.RenderPipelines.Universal.Runtime",true);
            var target=RenderTexture.GetTemporary(320,180,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;var image=new Texture2D(320,180,TextureFormat.RGB24,false);
            try
            {
                var request=Activator.CreateInstance(requestType);requestType.GetField("destination").SetValue(request,target);
                typeof(UnityEngine.Rendering.RenderPipeline).GetMethod("SubmitRenderRequest").MakeGenericMethod(requestType).Invoke(null,new object[]{camera,request});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,320,180),0,0);image.Apply(false);
                return image.GetPixels32();
            }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image); }
        }
        [Test]
        public void ParryNegativeUnregistersEvenWhenItsCameraWasDestroyedFirst()
        {
            var feature=Type.GetType("VideoParryInversionFeature, Assembly-CSharp",true);
            var setVisible=feature.GetMethod("SetVisible");
            var camera=new GameObject("DestroyedParryCamera").AddComponent<Camera>();
            setVisible.Invoke(null,new object[]{camera,true});
            Object.DestroyImmediate(camera.gameObject);
            setVisible.Invoke(null,new object[]{camera,false});
            var cameras=(System.Collections.IEnumerable)feature.GetField("activeCameras",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Assert.That(cameras.Cast<Camera>().Any(c=>ReferenceEquals(c,camera)),Is.False,"Scene teardown must not retain a destroyed camera reference.");
        }
        [TestCase("Reset")] [TestCase("Disable")] [TestCase("SessionReset")] [TestCase("ForcedHit")] [TestCase("TargetLost")]
        public void PendingThirdCelImpactCannotLeakAfterInterruption(string interruption)
        {
            Call(director,"StartTake",2,true);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            for(int i=0;i<200&&Get<int>(parry,"SuccessCount")==0;i++)Step();
            Assert.That(Get<bool>(driver,"IsParryImpactPending"),Is.True);
            try
            {
                if(interruption=="Reset")Call(director,"ResetTake",2);
                if(interruption=="Disable")((Behaviour)driver).enabled=false;
                if(interruption=="SessionReset")clockType.GetMethod("ResetSession").Invoke(null,null);
                if(interruption=="ForcedHit")Call(state,"ForceHit","PendingImpactTest");
                if(interruption=="TargetLost")((Component)Get<object>(director,"RearEnemy")).gameObject.SetActive(false);
                for(int i=0;i<16;i++)Step();
                Assert.That(Get<bool>(driver,"IsParryImpactPending"),Is.False);
                Assert.That(Get<int>(driver,"ParryImpactCount"),Is.Zero);
                Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
                Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);
                Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
            }
            finally {((Behaviour)driver).enabled=true;Call(director,"ResetTake",2);}
        }
        [TestCase(false)] [TestCase(true)]
        public void JustParryUsesTenTickStopWithAndWithoutTheVideoPresenter(bool presented)
        {
            Call(director,"ResetTake",2);
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            ((Behaviour)driver).enabled=presented;
            try
            {
                var source=(Component)Get<object>(director,"RearEnemy");
                source.transform.position=state.transform.position-Vector3.right*1.6f;Physics2D.SyncTransforms();
                Assert.That(Call(state,"RequestAction",PlayerActionRequest.Parry),Is.True);
                Step();
                var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
                Assert.That(Get<bool>(parry,"IsParryActive"),Is.True,"Apply the queued input on a combat tick before injecting contact.");
                for(int i=0;i<12&&Get<int>(parry,"ElapsedFrames")<Get<int>(parry,"AcceptanceStartFrame");i++)Step();
                var context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),source,975L,true);
                Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
                Assert.That(Get<object>(health,"LastParryResult").ToString(),Is.EqualTo("Just"));
                int Remaining()=>(int)clockType.GetProperty("HitstopRemainingTicks").GetValue(null);
                Assert.That(Remaining(),Is.EqualTo(presented?0:10));
                if(presented)
                {
                    for(int i=0;i<12&&Get<int>(driver,"ParryImpactCount")==0;i++)Step();
                    Assert.That(Get<int>(driver,"ParryImpactCount"),Is.EqualTo(1));
                    Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo("ParrySuccessLight_03"));
                    Assert.That(Remaining(),Is.EqualTo(10));
                }
            }
            finally {((Behaviour)driver).enabled=true;Call(director,"ResetTake",2);}
        }
        [TestCase(false)] [TestCase(true)]
        public void LongerParryAndKickStopsHoldContactForNineAndSixteenSlotsAndResume(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            long Tick()=>(long)clockType.GetProperty("TickCount").GetValue(null);
            int Remaining()=>(int)clockType.GetProperty("HitstopRemainingTicks").GetValue(null);
            for(int i=0;i<240&&Get<int>(parry,"SuccessCount")==0;i++)Step();
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(1));Assert.That(Remaining(),Is.Zero);
            for(int i=0;i<12&&Get<int>(driver,"ParryImpactCount")==0;i++)Step();
            Assert.That(Get<int>(driver,"ParryImpactCount"),Is.EqualTo(1));
            Assert.That(Remaining(),Is.EqualTo(9));
            var visual=state.GetComponent<SpriteRenderer>();var blade=visual.sprite;long tick=Tick();
            Assert.That(blade.name,Is.EqualTo("ParrySuccessLight_03"));
            for(int i=0;i<9;i++){Step();Assert.That(Tick(),Is.EqualTo(tick));Assert.That(visual.sprite,Is.SameAs(blade));}
            Assert.That(Remaining(),Is.Zero);Step();Assert.That(Tick(),Is.EqualTo(tick+1));
            var target=(Component)Get<object>(director,"RearEnemy");
            for(int i=0;i<180&&Get<int>(target,"HitCount")==0;i++)Step();
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));Assert.That(Remaining(),Is.EqualTo(16));
            var effect=Find("VideoVfxRenderer").GetComponentsInChildren<SpriteRenderer>().Single(r=>r.name=="AuthoredCounterEffect");
            var contact=visual.sprite;var electricity=effect.sprite;var position=target.transform.position;tick=Tick();
            Assert.That(contact.name,Is.EqualTo("CounterKick_09"));
            for(int i=0;i<2;i++)Step();
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<60;i++)Step();
            Assert.That(Remaining(),Is.EqualTo(14));Assert.That(Tick(),Is.EqualTo(tick));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<14;i++)
            {Step();Assert.That(Tick(),Is.EqualTo(tick));Assert.That(visual.sprite,Is.SameAs(contact));Assert.That(effect.sprite,Is.SameAs(electricity));Assert.That(target.transform.position,Is.EqualTo(position));}
            Assert.That(Remaining(),Is.Zero);Step();Assert.That(Tick(),Is.EqualTo(tick+1));
            Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));
            Call(director,"ResetTake",2);Assert.That(Remaining(),Is.Zero);
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);Assert.That(Camera.main.orthographicSize,Is.EqualTo(3.375f));
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void SubmittedCounterEffectAlignsAndFreezesWithTheKickOnPauseAndHitstop(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");var targetVisual=enemy.GetComponentInChildren<SpriteRenderer>();
            int restOrder=targetVisual.sortingOrder;
            Vector3 localBefore=default,rootBefore=default;Quaternion rotationBefore=default;
            for(int i=0;i<fps*6&&Get<int>(enemy,"HitCount")==0;i++)
            {localBefore=targetVisual.transform.localPosition;rootBefore=enemy.transform.position;rotationBefore=targetVisual.transform.localRotation;Call(director,"StepCapture",1d/fps);}
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            Assert.That(enemy.transform.position,Is.EqualTo(rootBefore),"Launch starts after the contact tick and its hitstop.");
            Assert.That(targetVisual.transform.localPosition,Is.EqualTo(localBefore),"Death must not shift the contact body during hitstop.");
            Assert.That(Quaternion.Angle(targetVisual.transform.localRotation,rotationBefore),Is.LessThan(.001f));
            Assert.That(targetVisual.sortingOrder,Is.LessThan(state.GetComponent<SpriteRenderer>().sortingOrder),"Attack trails stay in front of the struck body.");
            var vfx=Find("VideoVfxRenderer");var renderers=vfx.GetComponentsInChildren<SpriteRenderer>();
            var effect=renderers.Single(r=>r.name=="AuthoredCounterEffect");
            var drawing=state.GetComponent<SpriteRenderer>();
            Assert.That(effect.sprite.name,Is.EqualTo("CounterKickEffect_09"));
            Assert.That(effect.sortingOrder,Is.GreaterThan(targetVisual.sortingOrder));
            Assert.That(effect.sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(effect.sprite.rect.size,Is.EqualTo(new Vector2(256,256)));
            // Body toe (83,86 top-origin) maps to (172,129) on the separate canvas.
            Assert.That(Vector2.Distance(effect.sprite.pivot,new Vector2(121,123)),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(effect.transform.TransformPoint(new Vector3(51/32f,4/32f)),
                drawing.transform.TransformPoint(new Vector3(51/32f,4/32f))),Is.LessThan(.001f));
            Assert.That(renderers.Any(r=>r.sprite!=null&&(r.sprite.name.StartsWith("Fx_ElectricKick_")||r.sprite.name.StartsWith("Video_ElectricDischarge_"))),Is.False,"Do not overlay the old ring/discharge on the received effect.");
            var sprite=effect.sprite;var position=effect.transform.position;int active=Get<int>(vfx,"ActiveCount");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(effect.sprite,Is.SameAs(sprite));Assert.That(effect.transform.position,Is.EqualTo(position));
            Assert.That(Get<int>(vfx,"ActiveCount"),Is.EqualTo(active));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<Mathf.CeilToInt(fps*.06f);i++)Call(director,"StepCapture",1d/fps);
            Assert.That(enemy.transform.position,Is.EqualTo(rootBefore));
            Assert.That(effect.sprite,Is.SameAs(sprite),"The authored impact cel shares the combat hitstop.");
            for(int i=0;i<fps*2&&!Get<bool>(director,"Completed");i++)Call(director,"StepCapture",1d/fps);
            Assert.That(targetVisual.sortingOrder,Is.EqualTo(restOrder));
            Call(director,"ResetTake",2);
            Assert.That(Get<int>(vfx,"ActiveCount"),Is.Zero);
            Assert.That(targetVisual.transform.localRotation,Is.EqualTo(Quaternion.identity));
            Assert.That(targetVisual.sortingOrder,Is.EqualTo(restOrder));
        }
        [TestCase("ElectricKick",false)] [TestCase("ElectricKick",true)]
        [TestCase("Parry",false)] [TestCase("Parry",true)]
        public void ImpactSpritesAreReusedAndPurePixelModeNeverEnablesGlow(string kind,bool hybrid)
        {
            Call(director,"ResetTake",2);var vfx=Find("VideoVfxRenderer");Set(vfx,"HybridGlow",hybrid);Set(vfx,"UseAuthoredCounterEffect",false);
            int pooled=0,generated=0;
            for(int run=0;run<10;run++)
            {
                Call(vfx,"Emit",kind,Vector2.zero,1f,1,0f);
                if(kind=="ElectricKick")
                    Assert.That(vfx.GetComponentsInChildren<SpriteRenderer>().First(r=>r.sprite!=null&&r.sprite.name=="Fx_ElectricKick_01").sortingOrder,
                        Is.GreaterThan(0),"The shared attack effect stays in front of prototype actors at default sorting order.");
                if(!hybrid)Assert.That(vfx.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="OptionalGlow").All(r=>!r.enabled),Is.True);
                if(run==0)
                {
                    pooled=((ICollection)Get<object>(vfx,"pool")).Count;
                    generated=((ICollection)Get<object>(vfx,"generatedSprites")).Count;
                    Assert.That(generated,Is.GreaterThan(0));
                }
                else
                {
                    Assert.That(((ICollection)Get<object>(vfx,"pool")).Count,Is.EqualTo(pooled));
                    Assert.That(((ICollection)Get<object>(vfx,"generatedSprites")).Count,Is.EqualTo(generated));
                }
                for(int i=0;i<60;i++)Call(vfx,"AdvanceVisuals",1/60f);
                Assert.That(Get<int>(vfx,"ActiveCount"),Is.Zero);
            }
            Call(vfx,"ResetEffects");Assert.That(Get<int>(vfx,"ActiveCount"),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ParriedEnemyHoldsTheSameDownCelAsDefeatWithoutMovingOrRotating(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");var visual=enemy.GetComponentInChildren<SpriteRenderer>();
            var ai=enemy.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true));
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            Vector3 rest=visual.transform.localPosition;
            for(int i=0;i<220&&Get<int>(parry,"SuccessCount")==0;i++)Step();
            var animation=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            for(int i=0;i<12&&Get<int>(animation,"ParryImpactCount")==0;i++)Step();
            Assert.That(Get<int>(animation,"ParryImpactCount"),Is.EqualTo(1));
            Vector3 root=enemy.transform.position;Sprite stagger=visual.sprite;int held=0;
            Assert.That(stagger.name,Is.EqualTo("EnemySample_down_01"));
            while(held<180&&Get<int>(enemy,"HitCount")==0)
            {
                Assert.That(visual.sprite,Is.SameAs(stagger));
                Assert.That(visual.transform.localPosition,Is.EqualTo(rest+Vector3.down*(2f/32)));
                Assert.That(visual.transform.localRotation,Is.EqualTo(Quaternion.identity));
                Assert.That(enemy.transform.position,Is.EqualTo(root));Step();held++;
            }
            Assert.That(held,Is.GreaterThan(50));Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            Call(director,"ResetTake",2);Assert.That(visual.transform.localPosition,Is.EqualTo(rest));
            Assert.That(visual.transform.localRotation,Is.EqualTo(Quaternion.identity));
            Call(enemy,"ApplyParryStun",Enum.Parse(Type.GetType("ParryResult, Assembly-CSharp",true),"Normal"));
            for(int i=0;i<240&&Get<bool>(enemy,"IsStunned");i++)Step();
            Step();Assert.That(Get<bool>(enemy,"IsStunned"),Is.False);
            Assert.That(visual.transform.localPosition,Is.EqualTo(rest),"An expired parry must restore the normal pose origin.");
        }
        [TestCase(false)] [TestCase(true)]
        public void SubmittedKickImpactUsesTheDrawnFootInsideTheSelectedTarget(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");
            Bounds beforeContact=default;
            for(int i=0;i<340&&Get<int>(enemy,"HitCount")==0;i++)
            { beforeContact=enemy.GetComponent<Collider2D>().bounds;Step(); }
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            var kick=Find("VideoVfxRenderer").GetComponentsInChildren<SpriteRenderer>()
                .Single(r=>r.name=="AuthoredCounterEffect");
            int facing=mirrored?-1:1;
            // Drawn electrical toe is (83,86), pivot (32,90 top-origin).
            Vector3 expected=state.transform.position+new Vector3(facing*51f/32f,4f/32f,0);
            Assert.That(Vector3.Distance(kick.transform.TransformPoint(new Vector3(51/32f,4/32f)),expected),Is.LessThan(.001f));
            Assert.That(beforeContact.Contains(expected),Is.True);
            Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo("CounterKick_09"));
        }
        [TestCase("TargetLost")] [TestCase("CounterDisabled")] [TestCase("PlayerDead")] [TestCase("ForcedHit")]
        public void ChargeInterruptionReleasesTimeGravityInvulnerabilityAndCamera(string interruption)
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);
            for(int i=0;i<5;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            int hits=Get<int>(counter,"HitCount");
            if(interruption=="TargetLost") ((Component)Get<object>(counter,"SelectedTarget")).gameObject.SetActive(false);
            if(interruption=="CounterDisabled") ((Behaviour)counter).enabled=false;
            if(interruption=="PlayerDead") Call(state,"SetDead","VideoTestDeath");
            if(interruption=="ForcedHit") Call(state,"ForceHit","VideoTestInterrupt");
            for(int i=0;i<30;i++) Step();
            Assert.That(Get<int>(counter,"HitCount"),Is.EqualTo(hits));
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(interruption=="PlayerDead"?0:3));
            ((Behaviour)counter).enabled=true;
            Call(director,"ResetTake",2);Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(3));
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void SubmittedKickWarpsBehindTheParriedSourceAndHitsItsHead(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            bool sawArrival=false,sawCharge=false,sawHit=false;Vector2 hover=default;
            var enemy=(Component)Get<object>(director,"RearEnemy");
            for(int i=0;i<fps*6&&!sawHit;i++)
            {
                Call(director,"StepCapture",1d/fps);
                int elapsed=Get<int>(counter,"ElapsedFrames");
                if(Get<bool>(counter,"IsActive")&&elapsed>=12&&elapsed<18)
                {
                    sawArrival=true;hover=Get<Vector2>(counter,"HoverPosition");
                    Assert.That(Vector2.Distance(state.transform.position,hover),Is.LessThan(.005f));
                    Assert.That(hover.y-enemy.transform.position.y,Is.EqualTo(4/32f).Within(.005f));
                    Assert.That((hover.x-enemy.transform.position.x)*(mirrored?-1:1),Is.EqualTo(-58/32f).Within(.005f));
                    Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
                    Assert.That(Mathf.Sign(state.transform.lossyScale.x),Is.EqualTo(mirrored?-1:1),"The motor turns the whole actor toward the target.");
                    Assert.That(enemy.GetComponentInChildren<SpriteRenderer>().flipX,Is.EqualTo(mirrored),"Stun must preserve the enemy's attack-facing direction.");
                }
                if(Get<bool>(presentation,"IsCharged"))
                {
                    sawCharge=true;Assert.That(Vector2.Distance(state.transform.position,hover),Is.LessThan(.005f));
                    Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(enemy));
                    Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
                }
                if(Get<int>(enemy,"HitCount")>0)
                {
                    sawHit=true;Vector2 contact=Get<Vector2>(counter,"ContactPoint");
                    Assert.That(contact.x,Is.EqualTo(enemy.transform.position.x+(mirrored?1:-1)*7/32f).Within(.005f));
                    Assert.That(contact.y-enemy.transform.position.y,Is.EqualTo(6/32f).Within(.005f));
                    Assert.That(state.transform.position.y,Is.LessThan(hover.y));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
                }
            }
            Assert.That(sawArrival&&sawCharge&&sawHit,Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void RearDestinationObstacleBlocksWarpAndReleasesAllCounterOwnership(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            for(int i=0;i<230&&!Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Parry");i++) Step();
            var enemy=(Component)Get<object>(director,"RearEnemy");
            var ceiling=new GameObject("RearWarpWall");ceiling.layer=LayerMask.NameToLayer("Ground");
            ceiling.transform.position=new Vector3(enemy.transform.position.x+(mirrored?1:-1)*58/32f,1,0);
            ceiling.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3);Physics2D.SyncTransforms();
            try
            {
                for(int i=0;i<40;i++) Step();
                Assert.That(Get<string>(counter,"LastEndReason"),Is.EqualTo("ApproachBlocked"));
                Assert.That(Get<bool>(counter,"IsActive"),Is.False);Assert.That(Get<int>(enemy,"HitCount"),Is.Zero);
                Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
                Assert.That(Get<object>(counter,"SelectedTarget"),Is.Null);
                Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
                Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.False);
                Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
                Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(ceiling); }
        }
        [TestCase(false)] [TestCase(true)]
        public void EnemyKeepsContactThroughHitstopThenHopsInTheFixedDownCelFacingTheAttacker(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");var renderer=enemy.GetComponentInChildren<SpriteRenderer>();
            Sprite before=null;
            for(int i=0;i<300&&Get<int>(enemy,"HitCount")==0;i++) { before=renderer.sprite;Step(); }
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            Assert.That(renderer.sprite,Is.SameAs(before));
            Assert.That(renderer.flipX,Is.EqualTo(!mirrored),"The hitstop must already face the attacker.");
            for(int i=0;i<6;i++) { Step();Assert.That(renderer.sprite,Is.SameAs(before)); }
            for(int i=0;i<10;i++) Step();
            Assert.That(renderer.sprite.name,Does.StartWith("EnemySample_down_"));
            Assert.That(renderer.transform.localPosition,Is.EqualTo(new Vector3(0,-2f/32,0)));
            Assert.That(renderer.transform.localRotation,Is.EqualTo(Quaternion.identity));
            Assert.That(renderer.flipX,Is.EqualTo(!mirrored));
        }
        [TestCase("VerticalSlash",9,17,38)] [TestCase("HorizontalSlash",8,13,34)]
        public void SubmittedAttackPreservesItsPixelScaleAndCumulativeNativeTiming(string name,int count,int hit,int total)
        {
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var art=Get<object>(driver,"Art");var action=Call(art,"Find",name);
            var frames=Get<Sprite[]>(action,"frames");
            Assert.That(frames.Length,Is.EqualTo(count));
            Assert.That(Get<bool>(action,"provisional"),Is.True);
            Assert.That(Get<bool>(action,"includesSlashTrail"),Is.True);
            Assert.That(Get<int[]>(action,"durations"),Is.EqualTo(count==9?new[]{4,4,5,4,4,4,4,5,4}:new[]{4,4,5,4,4,4,4,5}));
            Assert.That(Get<int>(action,"hitFrame"),Is.EqualTo(hit));
            Assert.That(Get<int>(action,"TotalFrames"),Is.EqualTo(total));
            Assert.That(Get<int>(action,"branchFrame"),Is.EqualTo(29));
            Assert.That(Get<int>(action,"recoveryFrame"),Is.EqualTo(name=="VerticalSlash"?34:29));
            foreach(var sprite in frames)
            {
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(128,128)));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(32,38)));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
            }
            Assert.That(Get<bool>(art,"playerArtworkIsRough"),Is.True,"Submitted attacks do not finish the remaining artwork.");
        }
        [TestCase("CounterTransfer",8,64,12)] [TestCase("CounterKick",15,128,33)]
        public void CounterSubmissionsPreserveCanvasPivotAndReceivedStatus(string name,int count,int size,int total)
        {
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var art=Get<object>(driver,"Art");var motion=Call(art,"Find",name);
            var frames=Get<Sprite[]>(motion,"frames");
            Assert.That(frames.Length,Is.EqualTo(count));
            Assert.That(Get<int>(motion,"TotalFrames"),Is.EqualTo(total));
            Assert.That(Get<bool>(motion,"provisional"),Is.EqualTo(name=="CounterTransfer"));
            if(name=="CounterKick")
            {
                Assert.That(Get<bool>(motion,"includesCounterTrail"),Is.True);
                Assert.That(Get<Vector2>(motion,"counterContactOffset"),Is.EqualTo(new Vector2(51/32f,4/32f)));
            }
            foreach(var sprite in frames)
            {
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(size,size)));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(32,38)));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));
            }
            Assert.That(Get<bool>(art,"playerArtworkIsRough"),Is.True);
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void FirstSlashCutsItsTailOnlyForAReservedFollowup(int fps,bool queueNext)
        {
            Call(director,"StartTake",2,true);Set(director,"Running",false);
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            var started=attack.GetType().GetEvent("StepStarted");
            long first=-1,second=-1;bool reserved=false,heldTail=false;
            long Tick()=>(long)clockType.GetProperty("TickCount").GetValue(null);
            Action<int> onStep=step=>{if(step==1)first=Tick();if(step==2)second=Tick();};
            started.AddEventHandler(attack,onStep);
            try
            {
                Call(attack,"RequestAttack");
                for(int frame=0;frame<fps*2;frame++)
                {
                    if(queueNext&&!reserved&&Get<bool>(attack,"IsComboWindowOpen"))
                    { Call(attack,"RequestAttack");reserved=true; }
                    Call(director,"StepCapture",1d/fps);
                    if(first<0)continue;
                    if(!queueNext&&Tick()-first>=30&&Get<int>(attack,"ComboStep")==1)heldTail=true;
                    if(second>=0||!Get<bool>(attack,"IsAttacking"))break;
                }
                Assert.That(first,Is.GreaterThanOrEqualTo(0));
                if(queueNext)
                {
                    Assert.That(reserved,Is.True);Assert.That(second-first,Is.InRange(28L,30L));
                    Assert.That(Get<string>(attack,"LastEndReason"),Is.EqualTo("ComboBranch->Next"));
                }
                else
                {
                    Assert.That(second,Is.EqualTo(-1));Assert.That(heldTail,Is.True,"Single slash must keep its authored follow-through past the early combo branch.");
                    Assert.That(Tick()-first,Is.InRange(33L,35L));
                    Assert.That(Get<string>(attack,"LastEndReason"),Is.EqualTo("Recovery"));
                }
            }
            finally { started.RemoveEventHandler(attack,onStep); }
        }
        [TestCase(false)] [TestCase(true)]
        public void SubmittedTwoStageTakeKillsOnSecondHitWithoutLaterStagesOrDuplicateSlashTrails(bool mirror)
        {
            Set(director,"MirrorLayout",mirror);Call(director,"StartTake",2,true);
            var front=(Component)Get<object>(director,"FrontEnemy");
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            var vfx=Find("VideoVfxRenderer");int previousHits=0,maxStage=0;
            Assert.That(Get<int>(front,"CurrentHp"),Is.EqualTo(2));
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)
            {
                Step();maxStage=Mathf.Max(maxStage,Get<int>(attack,"ComboStep"));
                int hits=Get<int>(front,"HitCount");if(hits==previousHits)continue;
                Assert.That(hits,Is.EqualTo(previousHits+1));
                Assert.That(Get<bool>(front,"IsAlive"),Is.EqualTo(hits==1));
                Assert.That(vfx.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.enabled&&r.sprite!=null&&r.sprite.name.StartsWith("Fx_Slash")),Is.False,"Authored trails must not be doubled by generic slash VFX.");
                previousHits=hits;
            }
            Assert.That(Get<bool>(director,"Completed"),Is.True);Assert.That(maxStage,Is.EqualTo(2));
            Assert.That(previousHits,Is.EqualTo(2));Assert.That(Get<int>(front,"DeathCount"),Is.EqualTo(1));
            var marks=Marks();
            Assert.That(marks.Any(m=>new[]{"Slash3","Slash4"}.Contains((string)m.GetType().GetField("kind").GetValue(m))),Is.False);
            var hit=marks.Single(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Slash2");
            var death=marks.Single(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Death"&&(string)m.GetType().GetField("target").GetValue(m)==Get<string>(front,"IndividualId"));
            Assert.That((long)death.GetType().GetField("tick").GetValue(death),Is.EqualTo((long)hit.GetType().GetField("tick").GetValue(hit)));
        }
        [Test] public void KickInvulnerabilityRejectsAnotherAttackerAndEndsOnReset()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(counter,"IsActive");i++) Step();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
            int hp=Get<int>(health,"CurrentHp");
            Assert.That((bool)Call(health,"TryReceiveHit",1,(Vector2)state.transform.position+Vector2.right,0f),Is.False);
            Assert.That(Get<int>(health,"CurrentHp"),Is.EqualTo(hp));
            Call(director,"ResetTake",2);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((bool)Call(health,"TryReceiveHit",1,(Vector2)state.transform.position+Vector2.right,0f),Is.True);
        }
        [Test] public void PauseFreezesChargeAndResumesWithoutResidualOwners()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            int elapsed=Get<int>(counter,"ElapsedFrames");float charge=Get<float>(presentation,"ChargeRealElapsed");
            float dim=Get<float>(presentation,"DimAlpha");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<120;i++) Step();
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(elapsed));
            Assert.That(Get<float>(presentation,"ChargeRealElapsed"),Is.EqualTo(charge));
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.EqualTo(dim));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SlowDimCoversChargeAndClearsImmediatelyOnResetAtEveryRenderRate(int fps)
        {
            Call(director,"StartTake",2,true);
            for(int frame=0;frame<fps*5&&!Get<bool>(presentation,"IsCharged");frame++) Call(director,"StepCapture",1d/fps);
            for(int frame=0;frame<Mathf.CeilToInt(fps*.12f);frame++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.EqualTo(.965f).Within(.001f));
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.True);
            Assert.That(Get<bool>(presentation,"IsSpotlightVisible"),Is.True);
            Assert.That(Camera.main.transform.Find("VideoSlowDimmer"),Is.Not.Null);
            Call(director,"ResetTake",2);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsSpotlightVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.False);
        }
        [UnityTest] public IEnumerator SceneReloadDuringChargeReleasesAllRequestsAndReferences()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
            var previousDimmer=Camera.main.transform.Find("VideoSlowDimmer");
            var previousFocus=state.transform.Find("VideoHeroFocus");
            var previousEyeMaterial=previousFocus.Find("EyeGlowOnly").GetComponent<SpriteRenderer>().sharedMaterial;
            Assert.That(previousDimmer,Is.Not.Null);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideo.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;BindScene();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(previousDimmer==null,Is.True,"Previous scene's overlay survived unload.");
            Assert.That(previousFocus==null,Is.True,"Previous scene's spotlight survived unload.");
            Assert.That(previousEyeMaterial==null,Is.True,"Previous scene's runtime eye material survived unload.");
            Assert.That(Camera.main.GetComponentsInChildren<SpriteRenderer>(true).Count(r=>r.name=="VideoSlowDimmer"),Is.EqualTo(1));
        }
        [Test] public void PresentationDisableClearsDimAndReenableReusesItsOverlay()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            for(int i=0;i<6;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            var dimmer=Camera.main.transform.Find("VideoSlowDimmer");
            ((Behaviour)presentation).enabled=false;
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsSpotlightVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.False);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            ((Behaviour)presentation).enabled=true;
            Assert.That(Camera.main.transform.Find("VideoSlowDimmer"),Is.SameAs(dimmer));
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
        }
        [Test] public void SettingDimStrengthToZeroDuringChargeLeavesNoDarknessAfterwards()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(presentation,"IsCharged");i++) Step();
            for(int i=0;i<6;i++) Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.GreaterThan(0));
            presentation.GetType().GetField("slowDimStrength",Flags).SetValue(presentation,0f);Step();
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That(Get<bool>(presentation,"IsDimVisible"),Is.False);
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            presentation.GetType().GetField("slowDimStrength",Flags).SetValue(presentation,.965f);
            Call(director,"ResetTake",2);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ReleasePrecedesImpactAndFollowThroughReachesGroundAtEveryRenderRate(int fps)
        {
            Call(director,"StartTake",4,true);
            float impactHeight=0;bool sawRelease=false,sawLanding=false;
            int previousHits=Get<int>(counter,"HitCount");
            for(int frame=0;frame<fps*6&&!Get<bool>(director,"Completed");frame++)
            {
                Call(director,"StepCapture",1d/fps);
                if(Get<bool>(counter,"HasReleasedCharge")&&!sawRelease)
                {
                    sawRelease=true;
                    Assert.That((float)clockType.GetProperty("SlowScale").GetValue(null),Is.EqualTo(1));
                    Assert.That(Get<int>(counter,"HitCount"),Is.InRange(previousHits,previousHits+1),"A 30fps sample can include the next combat tick's impact.");
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
                }
                if(Get<int>(counter,"HitCount")>previousHits&&impactHeight==0) impactHeight=state.transform.position.y;
                if(Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Landing")&&!sawLanding)
                {
                    sawLanding=true;
                    Assert.That(impactHeight-state.transform.position.y,Is.InRange(.05f,.08f),"The supplied aerial motion already contains its vertical movement.");
                    Assert.That(state.transform.position.y,Is.InRange(.99f,1.02f));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False,"The final grounded pose releases the action.");
                }
            }
            Assert.That(sawRelease&&sawLanding,Is.True);
            var marks=((IEnumerable)director.GetType().GetField("Marks",Flags).GetValue(director)).Cast<object>().ToArray();
            object Mark(string name)=>marks.Single(m=>(string)m.GetType().GetField("kind").GetValue(m)==name);
            long Tick(string name)=>(long)Mark(name).GetType().GetField("tick").GetValue(Mark(name));
            double Real(string name)=>(double)Mark(name).GetType().GetField("realSeconds").GetValue(Mark(name));
            Assert.That(Tick("ChargeRelease")-Tick("ChargeStart"),Is.EqualTo(8));
            Assert.That(Tick("Kick")-Tick("ChargeRelease"),Is.EqualTo(1));
            Assert.That(Tick("Landing")-Tick("Kick"),Is.EqualTo(24));
            // At 30fps both 60Hz events can fall in the same sampled render frame.
            // Their tick difference above still proves a full-speed one-tick release.
            Assert.That(Real("Kick")-Real("ChargeRelease"),Is.InRange(0d,Math.Max(1d/fps,1d/60)+.001d));
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
        }
        [TestCase(false)] [TestCase(true)]
        public void ChargeReleaseOrLandingInterruptionClearsSubjectAndTime(bool atLanding)
        {
            Call(director,"StartTake",4,true);
            if(atLanding) WaitForKickRecovery();
            else for(int i=0;i<320&&!Get<bool>(counter,"HasReleasedCharge");i++) Step();
            Assert.That(Get<bool>(counter,"IsActive"),Is.EqualTo(!atLanding));
            Call(state,"ForceHit","VideoFastRecoveryInterrupt");
            for(int i=0;i<30;i++) Step();
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsSpotlightVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.False);
            Assert.That(Get<float>(presentation,"DimAlpha"),Is.Zero);
            Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That(state.GetComponent<Rigidbody2D>().gravityScale,Is.EqualTo(3));
        }
        [Test] public void OnlyTheVisibleEyeMaskGlowsAndItsTrailFreezesOnPause()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<280&&!Get<bool>(presentation,"IsCharged");i++) Step();
            for(int i=0;i<5;i++) Step();
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.True,"Use the submitted kick's newly authored Eye layer.");
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var kick=Call(Get<object>(driver,"Art"),"Find","CounterKick");
            state.GetComponent<SpriteRenderer>().sprite=Get<Sprite[]>(kick,"frames")[8];
            Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.False,"The contact cel has no Eye layer; do not invent a mask.");
            state.GetComponent<SpriteRenderer>().sprite=Get<Sprite[]>(kick,"frames")[0];
            Call(presentation,"AdvanceVisuals",0f);
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.True);
            var focus=state.transform.Find("VideoHeroFocus");
            var body=focus.GetComponent<SpriteRenderer>();var eyes=focus.Find("EyesOnly").GetComponent<SpriteRenderer>();
            Assert.That(body.sprite,Is.SameAs(state.GetComponent<SpriteRenderer>().sprite));
            Assert.That(eyes.sprite,Is.Not.SameAs(body.sprite));
            Assert.That(eyes.sortingOrder,Is.GreaterThan(Camera.main.transform.Find("VideoSlowDimmer").GetComponent<SpriteRenderer>().sortingOrder));
            var fx=Find("VideoVfxRenderer");Call(fx,"EmitEyeTrail",state.GetComponent<SpriteRenderer>(),-1);
            var trail=fx.GetComponentsInChildren<SpriteRenderer>().First(r=>r.sortingOrder==1020&&r.sprite==eyes.sprite);
            Assert.That(trail.sprite,Is.SameAs(eyes.sprite));Vector3 position=trail.transform.position;Color tint=trail.color;
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<60;i++) Step();
            Assert.That(trail.transform.position,Is.EqualTo(position));Assert.That(trail.color,Is.EqualTo(tint));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            Call(director,"ResetTake",2);
            Assert.That(Get<int>(fx,"ActiveCount"),Is.Zero);Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.False);
        }
        [TestCase(-1)] [TestCase(1)]
        public void HitbackMovesAwayAndStopsAtWallsThenResets(int direction)
        {
            Call(director,"ResetTake",2);var enemy=(Component)Get<object>(director,"FrontEnemy");
            var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            Vector3 origin=enemy.transform.position;
            var wall=new GameObject("VideoHitbackWall");wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=origin+Vector3.right*direction*.7f;
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,2);Physics2D.SyncTransforms();
            try
            {
                Call(driver,"ApplyHitback",.65f,10,Vector2.right*direction);
                for(int i=0;i<10;i++) Step();
                float distance=(enemy.transform.position.x-origin.x)*direction;
                Assert.That(distance,Is.InRange(.35f,.405f));Assert.That(Get<bool>(driver,"IsHitBacking"),Is.False);
                Call(director,"ResetTake",2);Assert.That(enemy.transform.position,Is.EqualTo(origin));
                Assert.That(Get<float>(driver,"LastHitbackDistance"),Is.Zero);
            }
            finally { Object.DestroyImmediate(wall); }
        }
        [TestCase(false)] [TestCase(true)]
        public void SubmittedSlashesStayPutWhileLaterStagesAndEnemyHitbacksGrow(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"FrontEnemy");var rear=(Component)Get<object>(director,"RearEnemy");
            float originX=enemy.transform.position.x;
            var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            int hits=0;float lastDistance=0,lastAdvance=0,lastHitX=state.transform.position.x;bool openingAttackSeen=false;
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)
            {
                Step();int now=Get<int>(enemy,"HitCount");
                if(!openingAttackSeen&&Get<bool>(attack,"IsAttacking"))
                { openingAttackSeen=true;lastHitX=state.transform.position.x; }
                if(now>hits)
                {
                    Assert.That(Get<float>(driver,"LastHitbackDistance"),Is.GreaterThan(lastDistance));
                    float advance=(state.transform.position.x-lastHitX)*(mirrored?-1:1);
                    if(now<=2)Assert.That(advance,Is.EqualTo(0).Within(.002f),"Submitted slash moved the root.");
                    else Assert.That(advance,Is.GreaterThan(lastAdvance),"Later rough stage did not advance farther.");
                    lastAdvance=advance;lastHitX=state.transform.position.x;
                    lastDistance=Get<float>(driver,"LastHitbackDistance");hits=now;
                }
            }
            Assert.That(hits,Is.EqualTo(4));Assert.That(Get<int>(rear,"HitCount"),Is.EqualTo(1));
            Assert.That((enemy.transform.position.x-originX)*(mirrored?-1:1),Is.InRange(2.16f,2.20f));
        }
        [TestCase(2,false,60)] [TestCase(4,true,60)] [TestCase(4,false,30)] [TestCase(4,true,120)]
        public void AuthoredTransferPrecedesTwoTickRearBlinkAndRevealsTheSubmittedKick(int stages,bool mirrored,int fps)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",stages,true);
            for(int i=0;i<fps*5&&!Get<bool>(counter,"IsActive");i++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<bool>(counter,"UsesBlinkApproach"),Is.True);
            Assert.That(Get<bool>(counter,"AttacksFromBehind"),Is.True);
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
            Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("CounterTransfer_"));
            Vector3 origin=state.transform.position;
            var enemy=(Component)Get<object>(counter,"SelectedTarget");
            for(int i=0;i<20&&Get<int>(counter,"ElapsedFrames")<10;i++)
            {
                Step();Assert.That(state.transform.position,Is.EqualTo(origin));
                Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
            }
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(10));
            Step();
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(11));
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.True);
            Step();
            float expected=enemy.transform.position.x+(mirrored?58/32f:-58/32f);
            Assert.That(state.transform.position.x,Is.EqualTo(expected).Within(.015f));
            Assert.That(state.transform.position.y,Is.EqualTo(enemy.transform.position.y+4/32f).Within(.02f));
            Vector3 arrival=state.transform.position;
            for(int i=0;i<fps&&!Get<bool>(presentation,"IsCharged");i++)
            {
                Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
                Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("CounterKick_"));
                Call(director,"StepCapture",1d/fps);
                Assert.That(Vector3.Distance(state.transform.position,arrival),Is.LessThan(.025f));
                Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(enemy));
            }
            Assert.That(Get<bool>(presentation,"IsCharged"),Is.True);
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
            Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.False);
            Assert.That(Get<int>(counter,"HitCount"),Is.Zero);
        }
        [TestCase("TargetLost")] [TestCase("CounterDisabled")] [TestCase("PresentationDisabled")]
        [TestCase("PlayerDead")] [TestCase("ForcedHit")] [TestCase("Reset")]
        public void BlinkInterruptionRestoresBodyAndRemovesShadow(string interruption)
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(counter,"IsActive");i++) Step();
            for(int i=0;i<20&&Get<int>(counter,"ElapsedFrames")<11;i++) Step();
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.False);
            if(interruption=="TargetLost") ((Component)Get<object>(counter,"SelectedTarget")).gameObject.SetActive(false);
            if(interruption=="CounterDisabled") ((Behaviour)counter).enabled=false;
            if(interruption=="PresentationDisabled") ((Behaviour)presentation).enabled=false;
            if(interruption=="PlayerDead") Call(state,"SetDead","VideoBlinkDeath");
            if(interruption=="ForcedHit") Call(state,"ForceHit","VideoBlinkInterrupt");
            if(interruption=="Reset") Call(director,"ResetTake",2);
            Step();
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
            Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.False);
            ((Behaviour)counter).enabled=true;((Behaviour)presentation).enabled=true;Call(director,"ResetTake",2);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
        }
        [Test] public void BlinkPauseFreezesPositionShadowAndSilhouettes()
        {
            Call(director,"StartTake",2,true);
            for(int i=0;i<240&&!Get<bool>(counter,"IsActive");i++) Step();
            for(int i=0;i<20&&Get<int>(counter,"ElapsedFrames")<11;i++) Step();
            Vector3 position=state.transform.position;int elapsed=Get<int>(counter,"ElapsedFrames");
            var fx=Find("VideoVfxRenderer");int effects=Get<int>(fx,"ActiveCount");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<60;i++) Step();
            Assert.That(state.transform.position,Is.EqualTo(position));Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(elapsed));
            Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.True);Assert.That(Get<int>(fx,"ActiveCount"),Is.EqualTo(effects));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<240&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
        }
        [TestCase(-1)] [TestCase(1)]
        public void BlinkCannotPassThroughAnObstacleThatAppearsDuringTravel(int direction)
        {
            Call(director,"ResetTake",2);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));Call(motor,"SetFacing",direction);
            var enemy=(Component)Get<object>(director,"RearEnemy");enemy.transform.position=state.transform.position+Vector3.right*direction*1.85f;
            Call(enemy.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true)),"ResetForCapture",state.transform,true);
            Physics2D.SyncTransforms();Call(counter,"RegisterParriedEnemy",enemy);Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            for(int i=0;i<20&&Get<int>(counter,"ElapsedFrames")<11;i++) Step();
            Assert.That(Get<int>(counter,"ElapsedFrames"),Is.EqualTo(11));
            float wallX=state.transform.position.x+direction*.35f;
            var wall=new GameObject("BlinkTravelWall");wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=new Vector3(wallX,2,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,6);Physics2D.SyncTransforms();
            try
            {
                for(int i=0;i<110&&Get<bool>(counter,"IsActive");i++) Step();
                Assert.That((state.transform.position.x-wallX)*direction,Is.LessThan(-.2f));
                Assert.That(Get<int>(enemy,"HitCount"),Is.Zero);Assert.That(Get<int>(counter,"HitCount"),Is.Zero);
                Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.True);
            }
            finally { Object.DestroyImmediate(wall); }
        }
        [TestCase(false)] [TestCase(true)]
        public void KickKnocksBackOnlyItsTargetAndPauseFreezesTheFixedPose(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            Vector3 resetOrigin=enemy.transform.position;
            for(int i=0;i<330&&!Get<bool>(driver,"IsLaunched");i++) Step();
            Assert.That(Get<bool>(driver,"IsLaunched"),Is.True);
            Assert.That(Get<int>(enemy,"DeathCount"),Is.EqualTo(1));
            Vector3 origin=enemy.transform.position;
            for(int i=0;i<16;i++) Step();
            Vector3 airborne=enemy.transform.position;var visual=enemy.GetComponentInChildren<SpriteRenderer>();Quaternion rotation=visual.transform.localRotation;
            double real=Get<double>(director,"RealElapsed");
            Assert.That(airborne.y-origin.y,Is.InRange(.21f,.27f));Assert.That(Mathf.Abs(airborne.x-origin.x),Is.GreaterThan(.4f));
            Assert.That(rotation,Is.EqualTo(Quaternion.identity));Assert.That(visual.flipX,Is.EqualTo(!mirrored));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<60;i++) Step();
            Assert.That(enemy.transform.position,Is.EqualTo(airborne));Assert.That(enemy.GetComponentInChildren<SpriteRenderer>().transform.localRotation,Is.EqualTo(rotation));
            Assert.That(Get<double>(director,"RealElapsed"),Is.EqualTo(real));Assert.That(Get<bool>(director,"Completed"),Is.False,"Pause must not end the take between landing and recovery start.");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            var frontDriver=((Component)Get<object>(director,"FrontEnemy")).GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            Assert.That((enemy.transform.position.x-origin.x)*(mirrored?-1:1),Is.EqualTo(Get<float>(frontDriver,"LastHitbackDistance")*2).Within(.002f));
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            Call(director,"ResetTake",4);Assert.That(Get<bool>(driver,"IsLaunched"),Is.False);
            Assert.That(enemy.transform.position,Is.EqualTo(resetOrigin));Assert.That(enemy.GetComponentInChildren<SpriteRenderer>().transform.localRotation,Is.EqualTo(Quaternion.identity));
        }
        [TestCase(-1)] [TestCase(1)]
        public void LaunchedEnemyCannotCrossATallWall(int direction)
        {
            Call(director,"ResetTake",2);var enemy=(Component)Get<object>(director,"FrontEnemy");var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            Vector3 origin=enemy.transform.position;var wall=new GameObject("LaunchWall");wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=origin+Vector3.right*direction*.7f;
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,6);Physics2D.SyncTransforms();
            try
            {
                Call(driver,"ApplyLaunch",2.25f,1.05f,20,Vector2.right*direction);
                for(int i=0;i<20;i++) Step();
                Assert.That((enemy.transform.position.x-origin.x)*direction,Is.InRange(.3f,.405f));
                Assert.That(Get<bool>(driver,"IsLaunched"),Is.False);
            }
            finally { Object.DestroyImmediate(wall); }
        }
        [TestCase(false)] [TestCase(true)]
        public void BothDefeatedEnemiesHopInFixedPosesThenStopAndResetTenTimes(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);
            for(int run=0;run<10;run++)
            {
                Call(director,"StartTake",2,true);
                var enemies=new[]{(Component)Get<object>(director,"FrontEnemy"),(Component)Get<object>(director,"RearEnemy")};
                int[] stoppedFade={0,0};bool[] began={false,false},stopped={false,false};
                Vector3[] origins=new Vector3[2],endpoints=new Vector3[2];float[] peakHeight={0,0};
                for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)
                {
                    Step();
                    for(int n=0;n<enemies.Length;n++)
                    {
                        var enemy=enemies[n];var visual=enemy.GetComponentInChildren<SpriteRenderer>();
                        if(enemy.gameObject.activeSelf&&Get<int>(enemy,"DeathCount")>0)
                        {
                            if(!began[n]){began[n]=true;origins[n]=enemy.transform.position;}
                            peakHeight[n]=Mathf.Max(peakHeight[n],enemy.transform.position.y-origins[n].y);
                            var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
                            if(!Get<bool>(driver,"IsHitBacking"))
                            {
                                if(!stopped[n]){stopped[n]=true;endpoints[n]=enemy.transform.position;}
                                Assert.That(enemy.transform.position,Is.EqualTo(endpoints[n]),"Finished knockback must stay still during the fade.");
                                Assert.That(enemy.transform.position.y,Is.EqualTo(origins[n].y).Within(.0001f));
                            }
                            Assert.That(visual.transform.localRotation,Is.EqualTo(Quaternion.identity));
                            if(!(bool)clockType.GetProperty("IsHitStopped").GetValue(null))
                                Assert.That(visual.sprite.name,Is.EqualTo("EnemySample_down_01"));
                            if(visual.color.a<.95f&&visual.color.a>.05f)
                            {Assert.That(stopped[n],Is.True);stoppedFade[n]++;}
                        }
                    }
                }
                Assert.That(Get<bool>(director,"Completed"),Is.True);Assert.That(began,Is.All.True);
                Assert.That(enemies.All(enemy=>!enemy.gameObject.activeInHierarchy),Is.True,"Do not finish the take before both defeat fades disappear after the longer stops.");
                Assert.That(stopped,Is.All.True);Assert.That(stoppedFade,Is.All.GreaterThan(5));
                Assert.That(peakHeight[0],Is.InRange(.17f,.19f));Assert.That(peakHeight[1],Is.InRange(.26f,.28f));
                float firstDistance=(endpoints[0].x-origins[0].x)*(mirrored?-1:1);
                float secondDistance=(endpoints[1].x-origins[1].x)*(mirrored?-1:1);
                Assert.That(firstDistance,Is.EqualTo(.8f).Within(.001f));
                Assert.That(secondDistance,Is.EqualTo(firstDistance*2).Within(.001f));
                Assert.That(Get<int>(enemies[0],"HitCount"),Is.EqualTo(2));Assert.That(Get<int>(enemies[1],"HitCount"),Is.EqualTo(1));
                Assert.That(Get<bool>(presentation,"IsParryImpactVisible"),Is.False);Assert.That(Get<object>(presentation,"BacklitEnemy"),Is.Null);Assert.That(Get<bool>(presentation,"IsCounterLightVisible"),Is.False);
                Assert.That(Get<string>(Marks().Single(m=>Get<string>(m,"kind")=="Kick"),"target"),Is.EqualTo(Get<string>(enemies[1],"IndividualId")));
                Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);Assert.That(Get<float>(presentation,"ImpactZoom"),Is.Zero);
                Call(director,"ResetTake",2);
                foreach(var enemy in enemies)
                {
                    var visual=enemy.GetComponentInChildren<SpriteRenderer>();var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
                    Assert.That(Get<bool>(driver,"IsLaunched"),Is.False);Assert.That(visual.transform.localPosition,Is.EqualTo(Vector3.zero));
                    Assert.That(visual.transform.localRotation,Is.EqualTo(Quaternion.identity));Assert.That(visual.color,Is.EqualTo(Color.white));
                }
                Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
                Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
                Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
            }
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void KickImpactBrieflyPushesCloserWithStrongerShakeThenReturnsAndResets(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            var target=(Component)Get<object>(director,"RearEnemy");bool parried=false,framed=false;
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            for(int i=0;i<fps*5&&Get<int>(target,"HitCount")==0;i++)
            {
                Call(director,"StepCapture",1d/fps);
                // The authored third cel carries the impact after the success event.
                if(!parried&&Get<int>(driver,"ParryImpactCount")>0)
                {parried=true;Assert.That(Get<Vector2>(presentation,"ShakeOffset").magnitude,Is.InRange(.18f,.23f));}
                if(Get<bool>(presentation,"IsCharged")&&Get<float>(presentation,"Zoom")>=.9999f)
                {
                    float center=(state.transform.position.x+target.transform.position.x)*.5f+Get<int>(counter,"KickFacingSign")*.30f;
                    Assert.That(Camera.main.transform.position.x-Get<Vector2>(presentation,"ShakeOffset").x,Is.EqualTo(center).Within(.032f));framed=true;
                }
            }
            Assert.That(parried,Is.True);Assert.That(framed,Is.True);Assert.That(Get<int>(target,"HitCount"),Is.EqualTo(1));
            // Preserve the stronger kick at every capture rate and in both layouts.
            Assert.That(Get<Vector2>(presentation,"ShakeOffset").magnitude,Is.GreaterThan(.42f));
            Assert.That(Get<Vector2>(presentation,"ShakeOffset").magnitude,Is.LessThanOrEqualTo(.52f));
            for(int i=0;i<Mathf.CeilToInt(fps*2/60f);i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Camera.main.orthographicSize,Is.LessThan(2.75f));
            Assert.That(Get<float>(presentation,"ImpactZoom"),Is.EqualTo(1).Within(.001f));
            var position=Camera.main.transform.position;float size=Camera.main.orthographicSize,pulse=Get<float>(presentation,"ImpactZoom");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Camera.main.transform.position,Is.EqualTo(position));Assert.That(Camera.main.orthographicSize,Is.EqualTo(size));
            Assert.That(Get<float>(presentation,"ImpactZoom"),Is.EqualTo(pulse));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<fps;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);Assert.That(Get<float>(presentation,"ImpactZoom"),Is.Zero);
            Assert.That(Camera.main.orthographicSize,Is.EqualTo(3.375f));Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);
            Call(director,"StartTake",2,true);
            for(int i=0;i<fps*5&&Get<float>(presentation,"ImpactZoom")==0;i++)Call(director,"StepCapture",1d/fps);
            Assert.That(Get<float>(presentation,"ImpactZoom"),Is.GreaterThan(0));Call(director,"ResetTake",2);
            Assert.That(Get<float>(presentation,"ImpactZoom"),Is.Zero);Assert.That(Camera.main.orthographicSize,Is.EqualTo(3.375f));
        }
        [Test] public void ReusedVanishEffectKeepsItsOwnMovingEnemy()
        {
            Call(director,"ResetTake",2);var fx=Find("VideoVfxRenderer");
            var a=(Component)Get<object>(director,"FrontEnemy");var b=(Component)Get<object>(director,"RearEnemy");
            Call(fx,"EmitFollowing","Vanish",a.transform,Vector2.zero,.6f,0f);
            Call(fx,"EmitFollowing","Vanish",b.transform,Vector2.zero,.6f,1f);
            Call(fx,"AdvanceVisuals",0f);Call(fx,"AdvanceVisuals",.5f);
            Call(fx,"EmitFollowing","Vanish",a.transform,Vector2.zero,.6f,0f);
            a.transform.position+=Vector3.right;b.transform.position+=Vector3.left;
            Call(fx,"AdvanceVisuals",0f);Call(fx,"AdvanceVisuals",.01f);
            var active=fx.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.sortingOrder==30&&r.gameObject.activeInHierarchy).ToArray();
            Assert.That(active.Length,Is.EqualTo(2));
            Assert.That(active.Any(r=>r.transform.position==a.transform.position),Is.True);
            Assert.That(active.Any(r=>r.transform.position==b.transform.position),Is.True);
        }
        [TestCase(-1f,0f,true,true)] [TestCase(1f,0f,true,true)]
        [TestCase(-2.2f,0f,true,false)] [TestCase(-1f,1.3f,true,false)] [TestCase(-1f,0f,false,false)]
        public void SourceParryUsesBothDirectionsRangeHeightAndParryableFlag(float dx,float dy,bool parryable,bool expected)
        {
            Call(director,"ResetTake",2);
            var source=(Component)Get<object>(director,"RearEnemy");source.transform.position=state.transform.position+new Vector3(dx,dy,0);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            Call(state,"RequestAction",PlayerActionRequest.Parry);Step();
            var context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),source,51L,parryable);
            Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
            Assert.That(Get<object>(parry,"LastParriedEnemy"),expected?Is.SameAs(source):Is.Null);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            Assert.That(Get<int>(motor,"FacingSign"),Is.EqualTo(expected&&dx<0?-1:1));
            int count=Get<int>(parry,"SuccessCount");
            Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(count));
            var other=(Component)Get<object>(director,"FrontEnemy");
            Assert.That(Get<bool>(other,"IsStunned"),Is.False);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(expected?1:0));
        }
        [Test] public void EmptySlashProducesNoHitstopOrHitEffects()
        {
            Call(director,"ResetTake",2);
            ((Component)Get<object>(director,"FrontEnemy")).transform.position+=Vector3.right*6;
            Physics2D.SyncTransforms();Call(state,"RequestAction",PlayerActionRequest.Attack);
            for(int frame=0;frame<36;frame++) Step();
            Assert.That((int)clockType.GetProperty("HitstopOwnerCount").GetValue(null),Is.Zero);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"EmissionCount"),Is.Zero);
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
        }

        [TestCase("Light")] [TestCase("Heavy")]
        public void DedicatedParryReactionSelectsItsOwnContactSpriteAndAnimatorState(string weight)
        {
            Call(director,"ResetTake",2);
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var animator=state.GetComponent<Animator>();var oldController=animator.runtimeAnimatorController;
            var oldLibrary=(ScriptableObject)Get<object>(driver,"Art");
            var library=Object.Instantiate(oldLibrary);
            var controller=new UnityEditor.Animations.AnimatorController();controller.AddLayer("Base Layer");
            var clips=new System.Collections.Generic.List<AnimationClip>();
            var layers=controller.layers;
            var motionType=oldLibrary.GetType().GetNestedType("Motion");
            var original=((Array)oldLibrary.GetType().GetField("motions").GetValue(oldLibrary)).Cast<object>()
                .Where(m=>!new[]{"ParrySuccessLight","ParrySuccessHeavy"}.Contains((string)motionType.GetField("name").GetValue(m))).ToArray();
            var motions=Array.CreateInstance(motionType,original.Length+2);Array.Copy(original,motions,original.Length);
            var legacy=oldLibrary.GetType().GetMethod("Find").Invoke(oldLibrary,new object[]{"ParryDeflect"});
            var frames=(Sprite[])motionType.GetField("frames").GetValue(legacy);
            try
            {
                for(int i=0;i<2;i++)
                {
                    string name="ParrySuccess"+(i==0?"Light":"Heavy");
                    var motion=Activator.CreateInstance(motionType);
                    motionType.GetField("name").SetValue(motion,name);
                    motionType.GetField("frames").SetValue(motion,new[]{frames[i+2]});
                    motionType.GetField("durations").SetValue(motion,new[]{10});motions.SetValue(motion,original.Length+i);
                    var clip=new AnimationClip{name=name};clips.Add(clip);
                    UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip,
                        new UnityEditor.EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},
                        new[]{new UnityEditor.ObjectReferenceKeyframe{time=0,value=frames[i+2]}});
                    layers[0].stateMachine.AddState("PlayerVideo_"+name).motion=clip;
                }
                var idle=layers[0].stateMachine.AddState("PlayerVideo_Idle");idle.motion=clips[0];layers[0].stateMachine.defaultState=idle;
                layers[0].stateMachine.AddState("PlayerVideo_Parry").motion=clips[0];
                library.GetType().GetField("motions").SetValue(library,motions);
                driver.GetType().GetField("library",Flags).SetValue(driver,library);
                animator.runtimeAnimatorController=controller;Call(driver,"ResetVisual");
                var enemy=(Component)Get<object>(director,"RearEnemy");enemy.transform.position=state.transform.position+Vector3.left;
                Call(state,"RequestAction",PlayerActionRequest.Parry);Step();
                object context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),enemy,991L,true,
                    Enum.Parse(Type.GetType("ParryReactionWeight, Assembly-CSharp",true),weight));
                Call(health,"TryReceiveContextHit",1,(Vector2)enemy.transform.position,0f,context);
                var expected=frames[weight=="Light"?2:3];
                Assert.That(state.GetComponent<SpriteRenderer>().sprite,Is.SameAs(expected),"Contact pose must be selected before hitstop.");
                animator.Update(0);Call(driver,"CombatTick");animator.Update(0);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("PlayerVideo_ParrySuccess"+weight),Is.True);
                for(int i=0;i<5;i++) Step();
                Assert.That(state.GetComponent<SpriteRenderer>().sprite,Is.SameAs(expected));
            }
            finally
            {
                driver.GetType().GetField("library",Flags).SetValue(driver,oldLibrary);
                animator.runtimeAnimatorController=oldController;Call(driver,"ResetVisual");
                Object.DestroyImmediate(controller);foreach(var clip in clips)Object.DestroyImmediate(clip);Object.DestroyImmediate(library);
            }
        }
        [Test] public void SkippedOldStockIsPreservedAndOverlappingOtherEnemyIsNeverKicked()
        {
            Call(director,"ResetTake",2);
            var a=(Component)Get<object>(director,"FrontEnemy");var b=(Component)Get<object>(director,"RearEnemy");
            a.transform.position=state.transform.position+Vector3.right*5;b.transform.position=state.transform.position+Vector3.right;
            Call(counter,"RegisterParriedEnemy",a);Call(counter,"RegisterParriedEnemy",a);Call(counter,"RegisterParriedEnemy",b);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(2));
            Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(b));
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
            a.transform.position=b.transform.position;Physics2D.SyncTransforms();int hp=Get<int>(a,"CurrentHp");
            for(int i=0;i<100&&Get<bool>(counter,"IsActive");i++) Step();
            Assert.That(Get<int>(a,"CurrentHp"),Is.EqualTo(hp));
            Assert.That(Get<int>(b,"CurrentHp"),Is.Zero);
            Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
        }
        private void Step()=>Call(director,"StepCapture",1d/60d);
        private object[] Marks()=>((IEnumerable)director.GetType().GetField("Marks",Flags).GetValue(director)).Cast<object>().ToArray();
        private Component WaitForKickRecovery()
        { return WaitForRecovery("AirRoundhouse"); }
        private Component WaitForRecovery(string motion)
        {
            // Exercise the retained optional artwork explicitly; the capture default skips attack returns.
            Set(state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true)),"SkipAttackRecovery",false);
            var recovery=state.GetComponent(Type.GetType("PlayerVisualRecovery2D, Assembly-CSharp",true));
            for(int i=0;i<360&&(!Get<bool>(recovery,"IsPlaying")||Get<string>(recovery,"StateName")!="PlayerVideo_Recovery_"+motion);i++) Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.True);return recovery;
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SwordAttacksGoStraightToTheSuppliedIdleWhenReturnsAreSkipped(int stage)
        {
            Call(director,"ResetTake",4);
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            Assert.That(Get<bool>(driver,"SkipAttackRecovery"),Is.True);
            Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            if(stage>1)Call(attack,"StartAttackStep",stage);
            for(int i=0;i<90&&Get<bool>(attack,"IsAttacking");i++)Step();
            Step();
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<bool>(driver,"IsRecovering"),Is.False);
            Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("IdleReference_"));
            Assert.That(Get<bool>(state,"BlocksStandardMovement"),Is.False);
        }
        [TestCase(2,false,30)] [TestCase(2,true,60)] [TestCase(4,false,60)] [TestCase(4,true,120)]
        public void SpacedTakeApproachesBothTargetsAndReturnsDirectlyToIdleAfterLanding(int stages,bool mirror,int fps)
        {
            UseApproachesForComparison();
            Set(director,"MirrorLayout",mirror);Call(director,"StartTake",stages,true);
            var front=(Component)Get<object>(director,"FrontEnemy");var rear=(Component)Get<object>(director,"RearEnemy");
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            float rearStart=rear.transform.position.x;bool walking=false,landedIdle=false,rearClosed=false;
            for(int frame=0;frame<fps*6&&!Get<bool>(director,"Completed");frame++)
            {
                Call(director,"StepCapture",1d/fps);
                walking|=state.GetComponent<SpriteRenderer>().sprite.name.StartsWith("WalkReference_");
                if(Get<bool>(rear,"IsAlive")&&(rear.transform.position.x-rearStart)*(mirror?-1:1)>.5f)rearClosed=true;
                if(!landedIdle&&Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Landing")&&!Get<bool>(counter,"IsActive"))
                {
                    // At 120fps one render advances only half a combat tick. Allow
                    // one complete combat tick for the animation driver to select idle.
                    Step();
                    Assert.That(Get<bool>(driver,"IsRecovering"),Is.False);
                    Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Does.StartWith("IdleReference_"));landedIdle=true;
                }
            }
            Assert.That(Get<bool>(director,"Completed"),Is.True);Assert.That(walking&&landedIdle,Is.True);
            Assert.That(Get<int>(front,"HitCount"),Is.EqualTo(stages));Assert.That(Get<int>(rear,"HitCount"),Is.EqualTo(1));
            var kinds=Marks().Select(m=>(string)m.GetType().GetField("kind").GetValue(m)).ToArray();
            Assert.That(kinds,Does.Contain("OpeningApproach").And.Contain("RearApproach"));
            Assert.That(kinds.Any(k=>new[]{"VerticalSlash","HorizontalSlash","RapidSheathe","IaiSlash","AirRoundhouse"}.Any(n=>k=="RecoveryStart:PlayerVideo_Recovery_"+n)),Is.False);
            Assert.That(rearClosed,Is.True);
        }
        [Test] public void OpeningFootworkResetsWithoutResidualInput()
        {
            UseApproachesForComparison();
            Call(director,"StartTake",2,true);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            for(int i=0;i<24&&!Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="OpeningApproach");i++)Step();
            Assert.That(Mathf.Abs(Get<float>(motor,"MoveInput")),Is.EqualTo(1));
            Call(director,"ResetTake",2);Assert.That(Get<float>(motor,"MoveInput"),Is.Zero);
            Assert.That(state.transform.position.x,Is.EqualTo(0));Call(director,"StartTake",2,true);
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);Assert.That(Get<float>(motor,"MoveInput"),Is.Zero);
        }
        [UnityTest] public IEnumerator OpeningFootworkFreezesOnPauseAndRetriesItsSuppressedAttackAfterResume()
        {
            UseApproachesForComparison();
            Call(director,"StartTake",2,true);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            for(int i=0;i<24&&!Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="OpeningApproach");i++)Step();
            Vector3 position=state.transform.position;double time=Get<double>(director,"RealElapsed");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<30;i++)Step();
            Assert.That(state.transform.position,Is.EqualTo(position));Assert.That(Get<double>(director,"RealElapsed"),Is.EqualTo(time));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            // Deterministic captures may advance several combat ticks in this render.
            // Resume must suppress that render's attack without consuming the request.
            for(int i=0;i<12;i++)Step();
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
            yield return null;
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);Assert.That(Get<float>(motor,"MoveInput"),Is.Zero);
        }
        [TestCase(1,-1,false)] [TestCase(1,1,false)] [TestCase(2,-1,false)] [TestCase(2,1,false)]
        [TestCase(1,-1,true)] [TestCase(1,1,true)] [TestCase(2,-1,true)] [TestCase(2,1,true)]
        public void SubmittedTrailReachesItsVisibleEdgeWithoutRootTravelOrBackwardDamage(int stage,int direction,bool outside)
        {
            Call(director,"ResetTake",2);
            var front=(Component)Get<object>(director,"FrontEnemy");var rear=(Component)Get<object>(director,"RearEnemy");
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));Call(motor,"SetFacing",direction);
            float reach=stage==1?2.3125f:2.65625f;
            float half=front.GetComponent<BoxCollider2D>().size.x/2;
            Vector3 origin=state.transform.position;
            front.transform.position=origin+Vector3.right*direction*(reach+half+(outside?.04f:-.04f));
            rear.transform.position=origin-Vector3.right*direction*1.1f;Physics2D.SyncTransforms();
            Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            if(stage==2)Call(attack,"StartAttackStep",2);
            for(int i=0;i<90&&Get<bool>(attack,"IsAttacking");i++)
            { Step();Assert.That(state.transform.position.x,Is.EqualTo(origin.x).Within(.002f)); }
            Assert.That(Get<int>(front,"HitCount"),Is.EqualTo(outside?0:1));
            Assert.That(Get<int>(rear,"HitCount"),Is.Zero);
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void SelectedTwoStageTakeStaysOnTheSpotUntilItsCounter(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            Assert.That(Get<bool>(director,"UsesSpacingApproaches"),Is.False);
            float originX=state.transform.position.x;
            for(int i=0;i<fps*6&&!Get<bool>(director,"Completed");i++)
            {
                Call(director,"StepCapture",1d/fps);
                if(!Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="CounterStart"))
                    Assert.That(state.transform.position.x,Is.EqualTo(originX).Within(.002f));
            }
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<int>((Component)Get<object>(director,"FrontEnemy"),"HitCount"),Is.EqualTo(2));
            Assert.That(Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="OpeningApproach"),Is.False);
            Assert.That(Marks().Count(m=>(string)m.GetType().GetField("kind").GetValue(m)=="RearApproach"),Is.EqualTo(1));
        }
        [TestCase(-1)] [TestCase(1)]
        public void SubmittedParryReadyReachesItsHeldPoseBeforeContactWithoutChangingAcceptance(int direction)
        {
            Call(director,"ResetTake",2);Call(state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true)),"SetFacing",direction);
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var ready=Call(Get<object>(driver,"Art"),"Find","ParryReady");
            Assert.That(Get<int[]>(ready,"durations"),Is.EqualTo(new[]{2,2,6,6,6,6,6,6}));
            Assert.That(Get<Sprite[]>(ready,"frames").Length,Is.EqualTo(8));
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            Assert.That(Get<int>(parry,"AcceptanceEndFrame"),Is.EqualTo(12));
            Call(state,"RequestAction",PlayerActionRequest.Parry);
            for(int i=0;i<4;i++)Step();
            var sprite=state.GetComponent<SpriteRenderer>().sprite;
            Assert.That(sprite.name,Is.EqualTo("ParryReady_03"));
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(64,64)));
            Assert.That(sprite.pivot,Is.EqualTo(new Vector2(32,38)));
            Assert.That(Get<bool>(parry,"IsParryActive"),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void SubmittedLightParryShowsItsSwingAndSettleBeforeTheSameSourceCounter(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",2,true);
            float originX=state.transform.position.x;
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var light=Call(Get<object>(driver,"Art"),"Find","ParrySuccessLight");
            Assert.That(Get<int[]>(light,"durations"),Is.EqualTo(new[]{2,1,1,1,1,1,1,2}));
            var sprites=Get<Sprite[]>(light,"frames");Assert.That(sprites.Length,Is.EqualTo(8));
            Assert.That(sprites.All(s=>s.pivot==new Vector2(32,38)&&s.rect.size==new Vector2(128,128)&&s.pixelsPerUnit==32),Is.True);
            Assert.That(Get<bool>(light,"includesParryEffect"),Is.True);
            Assert.That(Get<bool>(light,"hasParryContact"),Is.True);
            Assert.That(Get<Vector2>(light,"parryContactOffset"),Is.EqualTo(new Vector2(45/32f,6/32f)));
            var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<300&&!Get<bool>(counter,"IsActive");i++)
            {
                Step();
                if(Get<PlayerActionState>(state,"ActionState")==PlayerActionState.ParrySuccess)
                {
                    seen.Add(state.GetComponent<SpriteRenderer>().sprite.name);
                    Assert.That(state.transform.position.x,Is.EqualTo(originX).Within(.002f),"The light reaction keeps its planted root before blinking.");
                }
            }
            Assert.That(seen,Is.EquivalentTo(sprites.Select(s=>s.name)),"Do not keep only the first contact cel through the entire success reaction.");
            Assert.That(Get<bool>(counter,"IsActive"),Is.True);
            Assert.That(Get<object>(counter,"SelectedTarget"),Is.SameAs(Get<object>(director,"RearEnemy")));
        }
        [Test] public void AuthoredParrySuppressesTheOldBurstWhileUnfinishedReactionsKeepTheirFallback()
        {
            Call(director,"ResetTake",2);
            var vfx=Find("VideoVfxRenderer");
            var driver=state.GetComponent(Type.GetType("VideoPlayerAnimationDriver, Assembly-CSharp",true));
            var light=Call(Get<object>(driver,"Art"),"Find","ParrySuccessLight");
            object weight=Enum.Parse(Type.GetType("ParryReactionWeight, Assembly-CSharp",true),"Light");
            int before=Get<int>(vfx,"EmissionCount");
            Call(vfx,"EmitParry",Vector2.zero,1,weight);
            Assert.That(Get<int>(vfx,"EmissionCount"),Is.EqualTo(before));
            try
            {
                light.GetType().GetField("includesParryEffect").SetValue(light,false);
                Call(vfx,"EmitParry",Vector2.zero,1,weight);
                Assert.That(Get<int>(vfx,"EmissionCount"),Is.GreaterThan(before));
                Assert.That(Get<int>(vfx,"ActiveCount"),Is.GreaterThan(0));
            }
            finally { light.GetType().GetField("includesParryEffect").SetValue(light,true);Call(vfx,"ResetEffects"); }
            Assert.That(Get<int>(vfx,"ActiveCount"),Is.Zero);
        }
        [TestCase(1,"VerticalSlash")] [TestCase(2,"HorizontalSlash")]
        [TestCase(3,"RapidSheathe")] [TestCase(4,"IaiSlash")]
        public void EachUnqueuedSwordStageShowsItsOwnFreeRecovery(int stage,string motion)
        {
            Call(director,"ResetTake",4);Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
            var attack=state.GetComponent(Type.GetType("PlayerAttack2D, Assembly-CSharp",true));
            if(stage>1) Call(attack,"StartAttackStep",stage);
            var recovery=WaitForRecovery(motion);
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<bool>(state,"BlocksStandardMovement"),Is.False);
            Assert.That(Get<string>(recovery,"StateName"),Is.EqualTo("PlayerVideo_Recovery_"+motion));
            Assert.That((bool)Call(state,"RequestAction",PlayerActionRequest.Jump),Is.True);Step();
            Assert.That(Get<PlayerActionRequest>(state,"LastAcceptedRequest"),Is.EqualTo(PlayerActionRequest.Jump));
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void SuccessfulAndMissedParriesReturnWithoutLockingAgain(bool success)
        {
            Call(director,"ResetTake",2);Call(state,"RequestAction",PlayerActionRequest.Parry);Step();
            if(success)
            {
                var source=(Component)Get<object>(director,"RearEnemy");
                // This fixture injects a contact directly, so place its source inside melee reach.
                // The actual capture take now starts that enemy farther away and walks it in.
                source.transform.position=state.transform.position-Vector3.right*1.6f;Physics2D.SyncTransforms();
                var context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),source,971L,true);
                Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
                Assert.That(Get<object>(health,"LastParryResult").ToString(),Is.Not.EqualTo("None"));
            }
            var recovery=WaitForRecovery("ParryDeflect");
            Assert.That(Get<float>(state,"RemainingLock"),Is.Zero);Assert.That(Get<bool>(state,"BlocksStandardMovement"),Is.False);
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));Call(motor,"SetMoveInput",Vector2.left);Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(state.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash,Is.EqualTo(Animator.StringToHash("PlayerVideo_Move")));
        }
        [Test] public void NoInputShowsAFreeReturnFromCrouchToTheSuppliedIdle()
        {
            Call(director,"StartTake",4,true);var recovery=WaitForKickRecovery();
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(Get<bool>(state,"BlocksStandardMovement"),Is.False);
            Assert.That(Get<float>(state,"RemainingLock"),Is.Zero);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            var crouch=state.GetComponent<SpriteRenderer>().sprite;
            for(int i=0;i<10;i++) Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.True);
            Assert.That(state.GetComponent<SpriteRenderer>().sprite,Is.Not.SameAs(crouch));
            for(int i=0;i<60&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That(Get<bool>(director,"Completed"),Is.True);
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(state.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash,Is.EqualTo(Animator.StringToHash("PlayerVideo_Idle")));
        }
        [TestCase(-1)] [TestCase(1)] public void WalkingCancelsRecoveryOnItsFirstTick(int direction)
        {
            Call(director,"StartTake",4,true);var recovery=WaitForKickRecovery();
            var motor=state.GetComponent(Type.GetType("PlayerMotor2D, Assembly-CSharp",true));
            float before=state.transform.position.x;Call(motor,"SetMoveInput",Vector2.right*direction);Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That((state.transform.position.x-before)*direction,Is.GreaterThan(0));
            Assert.That(state.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash,Is.EqualTo(Animator.StringToHash("PlayerVideo_Move")));
            Call(motor,"SetMoveInput",Vector2.zero);Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.True);
            Assert.That(Get<string>(recovery,"StateName"),Is.EqualTo("PlayerVideo_Recovery_Move"));
        }
        [TestCase(PlayerActionRequest.Attack)] [TestCase(PlayerActionRequest.Parry)]
        [TestCase(PlayerActionRequest.Dash)] [TestCase(PlayerActionRequest.Jump)]
        public void AvailableActionsStartWithoutWaitingForRecovery(PlayerActionRequest request)
        {
            Call(director,"StartTake",4,true);var recovery=WaitForKickRecovery();
            Assert.That((bool)Call(state,"RequestAction",request),Is.True);Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(Get<PlayerActionRequest>(state,"LastAcceptedRequest"),Is.EqualTo(request));
            Assert.That(Get<object>(state,"LastRejectionReason").ToString(),Is.EqualTo("None"));
        }
        [TestCase(PlayerActionRequest.Special)] [TestCase(PlayerActionRequest.Heal)] [TestCase(PlayerActionRequest.Interact)]
        public void OtherGameplayRequestsCancelOnlyTheVisualWithoutConsumingInput(PlayerActionRequest request)
        {
            Call(director,"StartTake",2,true);var recovery=WaitForKickRecovery();
            Assert.That((bool)Call(state,"RequestAction",request),Is.True);Call(recovery,"CombatTick");
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(Get<PlayerActionRequest>(state,"PendingRequests")&request,Is.EqualTo(request));
            Assert.That(Get<PlayerActionState>(state,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
        }
        [Test] public void RecoveryPauseAndResetLeaveNoPoseOwnership()
        {
            Call(director,"StartTake",2,true);var recovery=WaitForKickRecovery();
            int elapsed=Get<int>(recovery,"ElapsedFrames");var pose=state.GetComponent<SpriteRenderer>().sprite;
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<60;i++) Step();
            Assert.That(Get<int>(recovery,"ElapsedFrames"),Is.EqualTo(elapsed));Assert.That(state.GetComponent<SpriteRenderer>().sprite,Is.SameAs(pose));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            Call(director,"ResetTake",2);Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(Get<int>(recovery,"ElapsedFrames"),Is.Zero);Assert.That(Get<string>(recovery,"StateName"),Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void RecoveryDisableOrDeathCancelsImmediately(bool death)
        {
            Call(director,"StartTake",2,true);var recovery=WaitForKickRecovery();
            if(death) Call(state,"SetDead","RecoveryDeathTest");else ((Behaviour)recovery).enabled=false;
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);Assert.That(Get<int>(recovery,"ElapsedFrames"),Is.Zero);
            Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
            ((Behaviour)recovery).enabled=true;Call(director,"ResetTake",2);
        }
        [UnityTest] public IEnumerator SceneReloadDuringRecoveryDestroysTheOldReturnAndFlash()
        {
            Call(director,"StartTake",2,true);var previousRecovery=WaitForKickRecovery();
            var previousFlash=Camera.main.transform.Find("VideoImpactFlash");
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CombatVideo.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;BindScene();
            Assert.That(previousRecovery==null&&previousFlash==null,Is.True);
            var recovery=state.GetComponent(Type.GetType("PlayerVisualRecovery2D, Assembly-CSharp",true));
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.False);
            Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
        }
        [Test] public void StrongImpactShakeAndFlashFreezeOnPauseAndSettleCompletely()
        {
            Call(director,"StartTake",4,true);int before=Get<int>(counter,"HitCount");
            for(int i=0;i<340&&Get<int>(counter,"HitCount")==before;i++) Step();
            Assert.That(Get<int>(counter,"HitCount"),Is.GreaterThan(before));
            Assert.That(Get<Vector2>(presentation,"ShakeOffset").magnitude,Is.GreaterThan(.12f));
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.GreaterThan(0));
            Assert.That(GameObject.Find("VideoImpactFlash").GetComponent<SpriteRenderer>().color.r,Is.EqualTo(1),"Kick must clear the parry's dark flash tint.");
            var position=Camera.main.transform.position;float flash=Get<float>(presentation,"FlashAlpha");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<60;i++) Step();
            Assert.That(Camera.main.transform.position,Is.EqualTo(position));Assert.That(Get<float>(presentation,"FlashAlpha"),Is.EqualTo(flash));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});for(int i=0;i<90;i++) Step();
            Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);Assert.That(Get<Vector2>(presentation,"ShakeOffset"),Is.EqualTo(Vector2.zero));
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void WallBlocksStartOrImpactWithoutWrongDamageOrResidualOwnership(bool afterStart)
        {
            Call(director,"ResetTake",2);var a=(Component)Get<object>(director,"FrontEnemy");Call(counter,"RegisterParriedEnemy",a);
            if(afterStart) { Call(state,"RequestAction",PlayerActionRequest.Attack);Step();Assert.That(Get<bool>(counter,"IsActive"),Is.True); }
            // After the first blink tick, put the obstacle ahead of the current body.
            // A fixed world x can end up behind it when the capture layout is widened.
            var wall=new GameObject("VideoCounterWall");wall.layer=LayerMask.NameToLayer("Ground");
            wall.transform.position=new Vector3((state.transform.position.x+a.transform.position.x)*.5f,1.2f,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3.4f);Physics2D.SyncTransforms();
            try
            {
                if(!afterStart)
                {
                    Assert.That(Get<bool>(counter,"CanStartAction"),Is.False);
                    Assert.That(Get<int>(counter,"StockCount"),Is.EqualTo(1));
                }
                else
                {
                    for(int i=0;i<180&&Get<bool>(counter,"IsActive");i++) Step();
                    Assert.That(Get<int>(a,"CurrentHp"),Is.EqualTo(2));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False);
                    Assert.That(Get<bool>(a,"IsStunned"),Is.False);
                    Assert.That((int)clockType.GetProperty("SlowOwnerCount").GetValue(null),Is.Zero);
                    Assert.That(Get<float>(presentation,"Zoom"),Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(wall); }
        }
        private void UseApproachesForComparison()
        {
            director.GetType().GetField("useSpacingApproaches",Flags).SetValue(director,true);
            var rear=(Component)Get<object>(director,"RearEnemy");
            var ai=rear.GetComponent(Type.GetType("EnemyMeleeChaser2D, Assembly-CSharp",true));
            ai.GetType().GetField("attackRange",Flags).SetValue(ai,1.35f);
        }
        private static Component Find(string name)=>(Component)Object.FindFirstObjectByType(Type.GetType(name+", Assembly-CSharp",true));
        private static T Get<T>(object target,string name)
        {
            var property=target.GetType().GetProperty(name,Flags);
            return (T)(property!=null?property.GetValue(target):target.GetType().GetField(name,Flags).GetValue(target));
        }
        private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        private static object Call(object target,string name,params object[] values)
        { return target.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==values.Length).Invoke(target,values); }
    }
}
