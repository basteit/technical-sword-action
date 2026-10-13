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
            Assert.That(Get<double>(director,"RealElapsed"),Is.InRange(3.87d,3.95d));
            Assert.That(Get<float>(presentation,"ChargeRealElapsed"),Is.InRange(.63f,.71f));
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
                    Assert.That(visual.sprite.name,Is.EqualTo("AirRoundhouse_06"),"Slow must start with the inverted coil, not hidden anticipation.");
                }
                if(!release&&Get<bool>(counter,"HasReleasedCharge"))
                {
                    release=true;
                    // A 30fps render can observe frame 25 after advancing two combat frames.
                    int releaseFrame=Get<int>(counter,"ElapsedFrames");
                    Assert.That(releaseFrame,Is.InRange(24,25));
                    Assert.That(visual.sprite.name,Is.EqualTo(releaseFrame==24?"AirRoundhouse_12":"AirRoundhouse_13"),"Release must show the matching extended rotating leg.");
                }
                if(Get<int>(counter,"HitCount")>previousHits)
                {
                    hit=true;
                    Assert.That(visual.sprite.name,Is.EqualTo("AirRoundhouse_16"),"Hitstop must hold the contact pose.");
                    Vector2 shake=Get<Vector2>(presentation,"ShakeOffset");
                    Assert.That(shake.y,Is.LessThan(-Mathf.Abs(shake.x)),"The downward kick must drive a downward impact.");
                    Call(director,"StepCapture",1d/fps);
                    Assert.That(visual.sprite.name,Is.EqualTo("AirRoundhouse_16"));
                }
            }
            Assert.That(charge&&release&&hit,Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void EverySwordHitHoldsItsContactPoseInsteadOfItsWindup(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"FrontEnemy");int checkedHits=0;
            string[] expected={"VerticalSlash_04","HorizontalSlash_04","RapidSheathe_05","IaiSlash_03"};
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
        public void LightParryFreezesTheBladeContactAndItsShortImpactThenResets(int fps,bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var parry=state.GetComponent(Type.GetType("PlayerParry2D, Assembly-CSharp",true));
            for(int i=0;i<fps*5&&Get<int>(parry,"SuccessCount")==0;i++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<int>(parry,"SuccessCount"),Is.EqualTo(1));
            var visual=state.GetComponent<SpriteRenderer>();
            Assert.That(visual.sprite.name,Is.EqualTo("ParryDeflect_03"));
            var effect=Find("VideoVfxRenderer").GetComponentsInChildren<SpriteRenderer>()
                .First(r=>r.sprite!=null&&r.sprite.name=="Fx_Parry_01");
            int facing=mirrored?1:-1;
            Vector3 contact=state.transform.position+new Vector3(facing*19/32f,8/32f,0);
            Assert.That(Vector3.Distance(effect.transform.position,contact),Is.LessThan(.001f));
            var flash=GameObject.Find("VideoImpactFlash").GetComponent<SpriteRenderer>();
            Assert.That(flash.color.r+flash.color.g+flash.color.b,Is.Zero);
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.GreaterThan(0));
            var position=Camera.main.transform.position;float alpha=Get<float>(presentation,"FlashAlpha");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});
            for(int i=0;i<fps;i++) Call(director,"StepCapture",1d/fps);
            Assert.That(visual.sprite.name,Is.EqualTo("ParryDeflect_03"));
            Assert.That(Camera.main.transform.position,Is.EqualTo(position));
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.EqualTo(alpha));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            Call(director,"ResetTake",4);
            Assert.That(Get<float>(presentation,"FlashAlpha"),Is.Zero);
            Assert.That(Get<int>(presentation,"ActiveShakeCount"),Is.Zero);
            Assert.That(Get<int>(Find("VideoVfxRenderer"),"ActiveCount"),Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void DownwardKickImpactUsesTheDrawnFootInsideTheSelectedTarget(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");
            Bounds beforeContact=default;
            for(int i=0;i<340&&Get<int>(enemy,"HitCount")==0;i++)
            { beforeContact=enemy.GetComponent<Collider2D>().bounds;Step(); }
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            var kick=Find("VideoVfxRenderer").GetComponentsInChildren<SpriteRenderer>()
                .First(r=>r.sprite!=null&&r.sprite.name=="Fx_Kick_01");
            int facing=mirrored?1:-1;
            // Contact key's toe is (54,48) in the 64px canvas, pivot (32,26 top-origin).
            Vector3 expected=state.transform.position+new Vector3(facing*22f/32f,-22f/32f,0);
            Assert.That(Vector3.Distance(kick.transform.position,expected),Is.LessThan(.001f));
            Assert.That(beforeContact.Contains(expected),Is.True);
            Assert.That(state.GetComponent<SpriteRenderer>().sprite.name,Is.EqualTo("AirRoundhouse_16"));
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
        public void ReleasePrecedesImpactAndLandingIsFastAtEveryRenderRate(int fps)
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
                    Assert.That(Get<int>(counter,"HitCount"),Is.EqualTo(previousHits));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.True);
                }
                if(Get<int>(counter,"HitCount")>previousHits&&impactHeight==0) impactHeight=state.transform.position.y;
                if(Marks().Any(m=>(string)m.GetType().GetField("kind").GetValue(m)=="Landing")&&!sawLanding)
                {
                    sawLanding=true;
                    Assert.That(impactHeight-state.transform.position.y,Is.GreaterThan(.45f));
                    Assert.That(state.transform.position.y,Is.InRange(.99f,1.02f));
                    Assert.That(Get<bool>(counter,"IsCounterInvincible"),Is.False,"Landing releases the action before free visual recovery.");
                }
            }
            Assert.That(sawRelease&&sawLanding,Is.True);
            var marks=((IEnumerable)director.GetType().GetField("Marks",Flags).GetValue(director)).Cast<object>().ToArray();
            object Mark(string name)=>marks.Single(m=>(string)m.GetType().GetField("kind").GetValue(m)==name);
            long Tick(string name)=>(long)Mark(name).GetType().GetField("tick").GetValue(Mark(name));
            double Real(string name)=>(double)Mark(name).GetType().GetField("realSeconds").GetValue(Mark(name));
            Assert.That(Tick("ChargeRelease")-Tick("ChargeStart"),Is.EqualTo(6));
            Assert.That(Tick("Kick")-Tick("ChargeRelease"),Is.EqualTo(4));
            Assert.That(Tick("Landing")-Tick("Kick"),Is.EqualTo(8));
            Assert.That(Real("Kick")-Real("ChargeRelease"),Is.InRange(.045d,.085d));
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
            for(int i=0;i<280&&!Get<bool>(presentation,"AreEyesVisible");i++) Step();
            Assert.That(Get<bool>(presentation,"AreEyesVisible"),Is.True);
            var focus=state.transform.Find("VideoHeroFocus");
            var body=focus.GetComponent<SpriteRenderer>();var eyes=focus.Find("EyesOnly").GetComponent<SpriteRenderer>();
            Assert.That(body.sprite,Is.SameAs(state.GetComponent<SpriteRenderer>().sprite));
            Assert.That(eyes.sprite,Is.Not.SameAs(body.sprite));
            Assert.That(eyes.sortingOrder,Is.GreaterThan(Camera.main.transform.Find("VideoSlowDimmer").GetComponent<SpriteRenderer>().sortingOrder));
            var fx=Find("VideoVfxRenderer");Call(fx,"EmitEyeTrail",state.GetComponent<SpriteRenderer>(),-1);
            var trail=fx.GetComponentsInChildren<SpriteRenderer>().First(r=>r.sortingOrder==1020);
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
        public void StrongerSwordStagesAdvanceAndHitbackMoreInBothDirections(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"FrontEnemy");var rear=(Component)Get<object>(director,"RearEnemy");
            var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            int hits=0;float lastDistance=0,lastAdvance=0,lastHitX=state.transform.position.x;
            for(int i=0;i<360&&!Get<bool>(director,"Completed");i++)
            {
                Step();int now=Get<int>(enemy,"HitCount");
                if(now>hits)
                {
                    Assert.That(Get<float>(driver,"LastHitbackDistance"),Is.GreaterThan(lastDistance));
                    float advance=(state.transform.position.x-lastHitX)*(mirrored?-1:1);
                    Assert.That(advance,Is.GreaterThan(lastAdvance),"Stronger stage did not advance farther.");
                    lastAdvance=advance;lastHitX=state.transform.position.x;
                    lastDistance=Get<float>(driver,"LastHitbackDistance");hits=now;
                }
            }
            Assert.That(hits,Is.EqualTo(4));Assert.That(Get<int>(rear,"HitCount"),Is.EqualTo(1));
            Assert.That((enemy.transform.position.x-1.1f*(mirrored?-1:1))*(mirrored?-1:1),Is.InRange(1.14f,1.18f));
        }
        [TestCase(2,false,60)] [TestCase(4,true,60)] [TestCase(4,false,30)] [TestCase(4,true,120)]
        public void BlinkReachesActualTargetInTwoTicksAndRevealsOnlyAtCharge(int stages,bool mirrored,int fps)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",stages,true);
            for(int i=0;i<fps*5&&!Get<bool>(counter,"IsActive");i++) Call(director,"StepCapture",1d/fps);
            Assert.That(Get<bool>(counter,"UsesBlinkApproach"),Is.True);
            Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.False);
            Assert.That(Get<bool>(presentation,"IsTransitShadowVisible"),Is.True);
            var enemy=(Component)Get<object>(counter,"SelectedTarget");
            for(int i=0;i<8&&Get<int>(counter,"ElapsedFrames")<2;i++) Call(director,"StepCapture",1d/fps);
            float expected=enemy.transform.position.x+(mirrored?-.85f:.85f);
            Assert.That(state.transform.position.x,Is.EqualTo(expected).Within(.015f));
            Assert.That(state.transform.position.y,Is.EqualTo(1.55f).Within(.02f));
            Vector3 arrival=state.transform.position;
            for(int i=0;i<fps&&!Get<bool>(presentation,"IsCharged");i++)
            {
                Assert.That(Get<bool>(presentation,"IsPlayerBodyVisible"),Is.False,"A windup body was exposed before slow charge.");
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
            Physics2D.SyncTransforms();Call(counter,"RegisterParriedEnemy",enemy);Call(state,"RequestAction",PlayerActionRequest.Attack);Step();
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
        public void KickLaunchesOnlyItsTargetAndPauseFreezesTheTrajectory(bool mirrored)
        {
            Set(director,"MirrorLayout",mirrored);Call(director,"StartTake",4,true);
            var enemy=(Component)Get<object>(director,"RearEnemy");var driver=enemy.GetComponent(Type.GetType("VideoEnemyAnimationDriver, Assembly-CSharp",true));
            Vector3 origin=enemy.transform.position;
            for(int i=0;i<330&&!Get<bool>(driver,"IsLaunched");i++) Step();
            Assert.That(Get<bool>(driver,"IsLaunched"),Is.True);Assert.That(Get<int>(enemy,"DeathCount"),Is.EqualTo(1));
            for(int i=0;i<16;i++) Step();
            Vector3 airborne=enemy.transform.position;Quaternion rotation=enemy.GetComponentInChildren<SpriteRenderer>().transform.localRotation;
            double real=Get<double>(director,"RealElapsed");
            Assert.That(airborne.y-origin.y,Is.GreaterThan(.8f));Assert.That(rotation,Is.Not.EqualTo(Quaternion.identity));
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{true});for(int i=0;i<60;i++) Step();
            Assert.That(enemy.transform.position,Is.EqualTo(airborne));Assert.That(enemy.GetComponentInChildren<SpriteRenderer>().transform.localRotation,Is.EqualTo(rotation));
            Assert.That(Get<double>(director,"RealElapsed"),Is.EqualTo(real));Assert.That(Get<bool>(director,"Completed"),Is.False,"Pause must not end the take between landing and recovery start.");
            clockType.GetMethod("SetPaused").Invoke(null,new object[]{false});
            for(int i=0;i<180&&!Get<bool>(director,"Completed");i++) Step();
            Assert.That((enemy.transform.position.x-origin.x)*(mirrored?1:-1),Is.EqualTo(2.25f).Within(.02f));
            Assert.That(Get<int>(enemy,"HitCount"),Is.EqualTo(1));
            Call(director,"ResetTake",4);Assert.That(Get<bool>(driver,"IsLaunched"),Is.False);
            Assert.That(enemy.transform.position,Is.EqualTo(origin));Assert.That(enemy.GetComponentInChildren<SpriteRenderer>().transform.localRotation,Is.EqualTo(Quaternion.identity));
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
            var recovery=state.GetComponent(Type.GetType("PlayerVisualRecovery2D, Assembly-CSharp",true));
            for(int i=0;i<360&&(!Get<bool>(recovery,"IsPlaying")||Get<string>(recovery,"StateName")!="PlayerVideo_Recovery_"+motion);i++) Step();
            Assert.That(Get<bool>(recovery,"IsPlaying"),Is.True);return recovery;
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
                var context=Activator.CreateInstance(Type.GetType("CombatHitContext2D, Assembly-CSharp",true),source,971L,true);
                Call(health,"TryReceiveContextHit",1,(Vector2)source.transform.position,0f,context);
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
            var wall=new GameObject("VideoCounterWall");wall.layer=LayerMask.NameToLayer("Ground");wall.transform.position=new Vector3(.55f,1.2f,0);
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
        private static Component Find(string name)=>(Component)Object.FindFirstObjectByType(Type.GetType(name+", Assembly-CSharp",true));
        private static T Get<T>(object target,string name)=>(T)target.GetType().GetProperty(name,Flags).GetValue(target);
        private static void Set(object target,string name,object value)=>target.GetType().GetProperty(name,Flags).SetValue(target,value);
        private static object Call(object target,string name,params object[] values)
        { return target.GetType().GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==values.Length).Invoke(target,values); }
    }
}
