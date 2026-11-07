using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PrototypeMobilityArtTests
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private Component clock,player,motor,art,arena;
        private Rigidbody2D body;
        private SpriteRenderer source;
        private bool manual;
        private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
        private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        private static V Get<V>(object target,string name)=>(V)(target.GetType().GetProperty(name,Flags)?.GetValue(target)??target.GetType().GetField(name,Flags).GetValue(target));
        private static void Pause(bool value)=>T("CombatTimeController").GetMethod("SetPaused").Invoke(null,new object[]{value});
        private void Frames(int count,int fps=60){for(int i=0;i<count;i++)Call(clock,"AdvanceFrame",1d/fps);Call(art,"RefreshVisual");}
        private void Request(PlayerActionRequest action)=>Assert.That(Call(player,"RequestAction",action),Is.True);
        private SpriteRenderer[] Ghosts()=>player.gameObject.scene.GetRootGameObjects()
            .Where(root=>root.name=="PrototypeDashAfterimages")
            .SelectMany(root=>root.GetComponentsInChildren<SpriteRenderer>())
            .Where(renderer=>renderer.enabled).ToArray();
        [UnitySetUp] public IEnumerator Setup()
        {
            clock=(Component)Object.FindFirstObjectByType(T("CombatTimeController"));manual=Get<bool>(clock,"ManualAdvanceOnly");
            clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,true);
            yield return EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/PrototypeCombatValidation.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player=(Component)Object.FindFirstObjectByType(T("PlayerStateMachine"));motor=player.GetComponent(T("PlayerMotor2D"));
            art=player.GetComponent(T("PrototypePlayerArtPresentation"));arena=(Component)Object.FindFirstObjectByType(T("PrototypeCombatArena"));
            body=player.GetComponent<Rigidbody2D>();source=player.GetComponent<SpriteRenderer>();
            ((Behaviour)player.GetComponent(T("PlayerInputRouter"))).enabled=false;
            body.position=new Vector2(0,.81f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            Call(motor,"SetMoveInput",Vector2.zero);Physics2D.SyncTransforms();Frames(4);yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {Pause(false);Call(arena,"ResetActionsOnly");clock.GetType().GetProperty("ManualAdvanceOnly").SetValue(clock,manual);yield return null;}

        [TestCase(30,-1)] [TestCase(60,1)] [TestCase(120,-1)]
        public void HeldJumpShowsAllSevenAuthoredCelsInTheCorrectPhases(int fps,int direction)
        {
            Call(motor,"SetFacing",direction);Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);
            var seen=new HashSet<int>();var phases=new HashSet<string>();int limit=fps*3;
            do
            {
                Frames(1,fps);int cel=Get<int>(art,"MobilitySourceFrame");string phase=Get<string>(art,"MobilityPhase");
                if(cel>0)
                {
                    seen.Add(cel);phases.Add(phase);
                    Assert.That(AssetDatabase.GetAssetPath(source.sprite),Does.Contain($"Jump_{cel:00}.png"));
                    var drawing=Get<SpriteRenderer>(art,"Artwork");
                    Assert.That(Mathf.Sign(drawing.transform.lossyScale.x)*(drawing.flipX?-1:1),Is.EqualTo(direction));
                    Assert.That(cel,phase=="Takeoff"?Is.InRange(1,3):phase=="Rise"?Is.EqualTo(4):phase=="Apex"?Is.InRange(5,6):Is.EqualTo(7));
                }
            }while((!Get<bool>(motor,"IsGrounded")||seen.Count==0)&&--limit>0);
            Assert.That(limit,Is.GreaterThan(0));CollectionAssert.AreEquivalent(Enumerable.Range(1,7),seen);
            CollectionAssert.AreEquivalent(new[]{"Takeoff","Rise","Apex","Fall"},phases);
            Frames(2);Assert.That(Get<string>(art,"MobilityPhase"),Is.EqualTo("None"));
            Assert.That(AssetDatabase.GetAssetPath(source.sprite),Does.Contain("IdleReference"));
        }
        [Test] public void EarlyReleaseUsesTheActualShortJumpWithoutArtificialHover()
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(2);Call(motor,"SetJumpHeld",false);
            var seen=new HashSet<int>();float peak=body.position.y;int ticks=0;
            while(!Get<bool>(motor,"IsGrounded")&&ticks++<120)
            {Frames(1);peak=Mathf.Max(peak,body.position.y);seen.Add(Get<int>(art,"MobilitySourceFrame"));}
            Assert.That(ticks,Is.InRange(8,65));Assert.That(peak,Is.LessThan(2.8f));CollectionAssert.Contains(seen,7);
            Assert.That(Get<bool>(motor,"IsGrounded"),Is.True);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ApexTransitionsMatchActualUpwardAndDownwardSpeedAtEveryRenderRate(int fps)
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);
            var apex=new List<int>();int limit=fps*3;bool sawRisingSix=false,sawFallingSix=false,sawFall=false;
            do
            {
                Frames(1,fps);int cel=Get<int>(art,"MobilitySourceFrame");float speed=body.linearVelocity.y;
                if(Get<string>(art,"MobilityPhase")=="Rise")Assert.That(speed,Is.GreaterThan(4f));
                if(Get<string>(art,"MobilityPhase")=="Apex")
                {
                    apex.Add(cel);
                    if(cel==5)Assert.That(speed,Is.InRange(1.25f,4f));
                    if(cel==6)
                    {
                        Assert.That(speed,Is.InRange(-1.25f,1.25f));
                        sawRisingSix|=speed>0;sawFallingSix|=speed<0;
                    }
                }
                if(sawFall)Assert.That(cel,Is.EqualTo(7).Or.EqualTo(0),"Landing collision sets vy to zero before the next ground check; do not flash apex cel 6 again.");
                if(cel==7&&!sawFall){Assert.That(speed,Is.LessThan(-1.25f));sawFall=true;}
            }while((!Get<bool>(motor,"IsGrounded")||apex.Count==0)&&--limit>0);
            Assert.That(limit,Is.GreaterThan(0));Assert.That(apex,Is.Ordered.Ascending);
            Assert.That(apex.Distinct(),Is.EqualTo(new[]{5,6}));
            Assert.That(sawRisingSix&&sawFallingSix,Is.True,"Cel 6 must straddle the real apex, not precede or lag behind it.");
        }
        [TestCase(5f,4)] [TestCase(2.5f,5)] [TestCase(.6f,6)] [TestCase(-.6f,6)] [TestCase(-2.5f,7)]
        public void TheSameVerticalSpeedKeepsItsPoseWithoutTheOldSixTickCelTimer(float speed,int cel)
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(12);
            body.gravityScale=0;body.linearDamping=0;body.linearVelocity=new Vector2(0,speed);
            for(int i=0;i<12;i++)
            {
                Frames(1);Assert.That(Get<int>(art,"MobilitySourceFrame"),Is.EqualTo(cel));
            }
        }
        [TestCase(-1)] [TestCase(1)] public void FasterWalkingCoversTwentyPercentMoreDistanceWithoutRetimingTheArt(int direction)
        {
            Assert.That(Get<float>(motor,"MoveSpeed"),Is.EqualTo(5.28228f).Within(.00001f));
            Call(motor,"SetMoveInput",Vector2.right*direction);float start=body.position.x;Frames(30);
            float tuned=direction*(body.position.x-start);
            Call(arena,"ResetActionsOnly");body.position=new Vector2(0,.81f);player.transform.position=body.position;
            motor.GetType().GetField("moveSpeed",Flags).SetValue(motor,4.4019f);
            Call(motor,"SetMoveInput",Vector2.right*direction);Physics2D.SyncTransforms();Frames(4);
            start=body.position.x;Frames(30);float baseline=direction*(body.position.x-start);
            Assert.That(tuned/baseline,Is.EqualTo(1.2f).Within(.015f));
        }
        [TestCase(-1)] [TestCase(1)] public void GroundDashEndsAtFourteenTicksAndHandsOffToHeldMovement(int direction)
        {
            Call(motor,"SetMoveInput",Vector2.right*direction);Request(PlayerActionRequest.Dash);Frames(12);
            Assert.That(Get<bool>(motor,"IsDashing"),Is.True);Assert.That(Get<bool>(motor,"IsDashInvincible"),Is.False);
            Frames(1);Assert.That(Get<bool>(motor,"IsDashing"),Is.True);
            Assert.That(body.linearVelocity.x,Is.Zero.Within(.001f),"Only the two recovery ticks remain, with no extra dash travel.");
            Frames(1);Assert.That(Get<bool>(motor,"IsDashing"),Is.False);
            Assert.That(Get<PlayerActionState>(player,"ActionState"),Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(body.linearVelocity.x,Is.EqualTo(direction*Get<float>(motor,"MoveSpeed")).Within(.001f));
            Assert.That(Get<float>(motor,"DashCooldownRemaining")*60,Is.EqualTo(14f).Within(.001f));
            float end=body.position.x;Frames(1);Assert.That(direction*(body.position.x-end),Is.GreaterThan(.07f));
        }
        [Test] public void AirDashStillUsesTwentyTicksAndCannotRestartBeforeLanding()
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(5);
            float start=body.position.x;
            Request(PlayerActionRequest.Dash);Frames(12);
            Assert.That(Mathf.Abs(body.position.x-start),Is.EqualTo(4.5f).Within(.12f));
            Assert.That(Get<bool>(motor,"IsDashInvincible"),Is.False);
            Frames(2);Assert.That(Get<bool>(motor,"IsDashing"),Is.True);
            Frames(6);Assert.That(Get<bool>(motor,"IsDashing"),Is.False);Assert.That(Get<bool>(motor,"AirDashUsed"),Is.True);
            Assert.That(Get<float>(motor,"DashCooldownRemaining")*60,Is.EqualTo(14f).Within(.001f));
            Frames(14);Assert.That(Get<bool>(motor,"CanStartDash"),Is.False);
        }
        [Test] public void LandingDuringAirDashDoesNotAdoptTheShorterGroundDuration()
        {
            body.position=new Vector2(0,1.6f);player.transform.position=body.position;
            body.linearVelocity=new Vector2(0,-10);Physics2D.SyncTransforms();Frames(1);
            Assert.That(Get<bool>(motor,"IsGrounded"),Is.False);
            Request(PlayerActionRequest.Dash);Frames(14);
            Assert.That(Get<bool>(motor,"IsGrounded"),Is.True);Assert.That(Get<bool>(motor,"IsDashing"),Is.True);
            Frames(6);Assert.That(Get<bool>(motor,"IsDashing"),Is.False);
        }
        [TestCase("Hit")] [TestCase("Dead")] [TestCase("Reset")] [TestCase("Disable")]
        public void InterruptedGroundDashNeverAppliesTheNaturalMovementHandoff(string reason)
        {
            Call(motor,"SetMoveInput",Vector2.right);Request(PlayerActionRequest.Dash);Frames(13);
            Assert.That(Ghosts(),Is.Not.Empty);
            if(reason=="Hit")Call(player,"ForceHit","MobilityRefinementTest");
            if(reason=="Dead")Call(player,"SetDead","MobilityRefinementTest");
            if(reason=="Reset")Call(arena,"ResetActionsOnly");
            if(reason=="Disable")((Behaviour)motor).enabled=false;
            Assert.That(Get<bool>(motor,"IsDashing"),Is.False);Assert.That(Get<bool>(motor,"IsDashInvincible"),Is.False);
            Assert.That(body.linearVelocity.x,Is.Zero.Within(.001f));
            Call(player.GetComponent(T("PrototypeDashTrail")),"LateUpdate");
            Assert.That(Ghosts(),Is.Empty);
            ((Behaviour)motor).enabled=true;
        }
        [Test] public void RemovedDialogueTestLeavesSharedBAsDashAtItsFormerLocation()
        {
            Assert.That(Object.FindFirstObjectByType(T("DialogueController")),Is.Null);
            Assert.That(Object.FindFirstObjectByType(T("DialogueInteractable2D")),Is.Null);
            body.position=new Vector2(-6,.81f);player.transform.position=body.position;Physics2D.SyncTransforms();Frames(2);
            Call(player.GetComponent(T("PlayerInteractor2D")),"RefreshAfterTeleport");
            Assert.That(Call(player,"RequestSharedDashInteract"),Is.True);Frames(1);
            Assert.That(Get<PlayerActionRequest>(player,"LastSharedInputResolution"),Is.EqualTo(PlayerActionRequest.Dash));
            Assert.That(Get<bool>(motor,"IsDashing"),Is.True);
        }
        [TestCase(PlayerActionRequest.Attack)] [TestCase(PlayerActionRequest.Parry)]
        [TestCase(PlayerActionRequest.Special)] [TestCase(PlayerActionRequest.Dash)]
        public void AirActionsOwnTheirArtworkInsteadOfBeingOverwrittenByJump(PlayerActionRequest action)
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(5);Request(action);Frames(2);
            Assert.That(Get<string>(art,"MobilityPhase"),Is.EqualTo(action==PlayerActionRequest.Dash?"Dash":"None"));
            Assert.That(AssetDatabase.GetAssetPath(source.sprite),Does.Not.Contain("/Jump_"));
            if(action==PlayerActionRequest.Dash)
            {
                Assert.That(AssetDatabase.GetAssetPath(source.sprite),Does.Contain("/MobilitySubmitted/Dash_"));
                Assert.That(Get<bool>(motor,"AirDashUsed"),Is.True);
            }
            Call(arena,"ResetActionsOnly");Frames(2);Assert.That(Get<string>(art,"MobilityPhase"),Is.EqualTo("None"));
            Assert.That(AssetDatabase.GetAssetPath(source.sprite),Does.Contain("IdleReference"));
        }
        [TestCase(-1)] [TestCase(1)] public void GroundDashShowsAllThreeReceivedCelsAndTravelsFourPointFiveUnits(int direction)
        {
            Call(motor,"SetFacing",direction);Request(PlayerActionRequest.Dash);
            float start=body.position.x;var seen=new HashSet<string>();int limit=50;
            do {Frames(1);if(Get<bool>(motor,"IsDashing"))seen.Add(source.sprite.name);}while(Get<bool>(motor,"IsDashing")&&--limit>0);
            Assert.That(limit,Is.GreaterThan(0));Assert.That(seen.Count,Is.EqualTo(3));
            Assert.That(direction*(body.position.x-start),Is.EqualTo(4.5f).Within(.12f));
            Assert.That(Get<bool>(motor,"IsDashInvincible"),Is.False);
        }
        [TestCase(-1,false)] [TestCase(1,true)]
        public void DashGhostsSnapshotActualTravelAndTheAuthoredPose(int direction,bool air)
        {
            if(air){Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(5);}
            Call(motor,"SetFacing",direction);float start=body.position.x;
            Request(PlayerActionRequest.Dash);Frames(1);
            var first=Ghosts().Single();var drawing=Get<SpriteRenderer>(art,"Artwork");
            Assert.That(first.sprite,Is.SameAs(drawing.sprite));
            Assert.That(AssetDatabase.GetAssetPath(first.sprite),Does.Contain("/MobilitySubmitted/Dash_"));
            Assert.That(Mathf.Sign(first.transform.lossyScale.x)*(first.flipX?-1:1),Is.EqualTo(direction));
            Assert.That(first.sortingLayerID,Is.EqualTo(drawing.sortingLayerID));
            Assert.That(first.sortingOrder,Is.LessThan(drawing.sortingOrder));
            Assert.That(first.GetComponent<Collider2D>(),Is.Null);
            Assert.That(first.transform.position.x,Is.EqualTo(start).Within(.02f));
            float initialAlpha=first.color.a;Vector3 previous=first.transform.position;
            Frames(4);
            Assert.That(Ghosts().Length,Is.EqualTo(3));
            Assert.That(first.transform.position,Is.EqualTo(previous));
            Assert.That(first.color.a,Is.GreaterThan(0).And.LessThan(initialAlpha));
            Assert.That(Ghosts().All(g=>direction*(body.position.x-g.transform.position.x)>.02f),Is.True);
            Frames(20);
            Assert.That(Ghosts(),Is.Empty);
        }
        [Test] public void DashGhostsFreezeOnPauseAndHitstopAndClearAtZeroVfx()
        {
            Request(PlayerActionRequest.Dash);Frames(5);
            var ghosts=Ghosts();Assert.That(ghosts,Is.Not.Empty);
            var alpha=ghosts.Select(g=>g.color.a).ToArray();var position=body.position;
            Pause(true);Frames(20);
            CollectionAssert.AreEqual(alpha,Ghosts().Select(g=>g.color.a).ToArray());
            Assert.That(body.position,Is.EqualTo(position));Pause(false);
            T("CombatTimeController").GetMethod("RequestHitstop").Invoke(null,new object[]{this,4f/60});
            Frames(2);
            CollectionAssert.AreEqual(alpha,Ghosts().Select(g=>g.color.a).ToArray());
            Assert.That(body.position,Is.EqualTo(position));
            var settings=Get<object>(Object.FindFirstObjectByType(T("CombatFeedback2D")),"Settings");
            settings.GetType().GetField("vfx").SetValue(settings,0f);
            Call(player.GetComponent(T("PrototypeDashTrail")),"LateUpdate");
            Assert.That(Ghosts(),Is.Empty);
            Frames(30);Assert.That(Ghosts(),Is.Empty);
        }
        [TestCase(-1)] [TestCase(1)]
        public void LongerDashCannotPassAWallOrEmitStationaryGhosts(int direction)
        {
            var wall=new GameObject("DashTestWall");wall.layer=LayerMask.NameToLayer("Ground");
            wall.transform.position=new Vector3(direction*1.4f,1);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,2f);
            Physics2D.SyncTransforms();
            try
            {
                Call(motor,"SetFacing",direction);Request(PlayerActionRequest.Dash);Frames(6);
                float stopped=body.position.x;Assert.That(direction*stopped,Is.InRange(.85f,1.05f));
                var existing=Ghosts().Select(g=>g.transform.position).ToArray();Assert.That(existing,Is.Not.Empty);
                Frames(6);
                Assert.That(body.position.x,Is.EqualTo(stopped).Within(.01f));
                Assert.That(Ghosts().All(g=>existing.Contains(g.transform.position)),Is.True);
                Frames(10);Assert.That(Ghosts(),Is.Empty);
            }
            finally {Object.Destroy(wall);}
        }
        [Test] public void PauseFreezesTheMobilityCelAndResetClearsItsPhase()
        {
            Call(motor,"SetJumpHeld",true);Request(PlayerActionRequest.Jump);Frames(5);
            var sprite=source.sprite;var position=body.position;string phase=Get<string>(art,"MobilityPhase");
            Pause(true);Frames(60);Assert.That(source.sprite,Is.SameAs(sprite));Assert.That(body.position,Is.EqualTo(position));
            Assert.That(Get<string>(art,"MobilityPhase"),Is.EqualTo(phase));Pause(false);
            Call(arena,"ResetActionsOnly");Frames(2);Assert.That(Get<int>(art,"MobilitySourceFrame"),Is.Zero);
        }
        [Test] public void FloorDropUsesFallingCelWithoutPlayingTheTakeoff()
        {
            var platform=Object.FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None).First(p=>p.useOneWay&&p.GetComponent<BoxCollider2D>()!=null);
            var bounds=platform.GetComponent<BoxCollider2D>().bounds;
            body.position=new Vector2(bounds.center.x,bounds.max.y+.805f);player.transform.position=body.position;body.linearVelocity=Vector2.zero;
            Physics2D.SyncTransforms();Frames(4);Assert.That(Get<bool>(motor,"IsGrounded"),Is.True);
            Call(motor,"SetJumpDownInput",true);Request(PlayerActionRequest.Jump);Frames(1);
            Assert.That(Get<bool>(motor,"IsDroppingThrough"),Is.True);Assert.That(Get<string>(art,"MobilityPhase"),Is.EqualTo("Fall"));
            Assert.That(Get<int>(art,"MobilitySourceFrame"),Is.EqualTo(7));
        }
    }
}
