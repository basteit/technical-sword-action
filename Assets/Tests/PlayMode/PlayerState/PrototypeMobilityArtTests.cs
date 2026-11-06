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
        [TestCase(-1)] [TestCase(1)] public void GroundDashShowsAllThreeReceivedCelsAndKeepsItsOriginalDistance(int direction)
        {
            Call(motor,"SetFacing",direction);Request(PlayerActionRequest.Dash);
            float start=body.position.x;var seen=new HashSet<string>();int limit=50;
            do {Frames(1);if(Get<bool>(motor,"IsDashing"))seen.Add(source.sprite.name);}while(Get<bool>(motor,"IsDashing")&&--limit>0);
            Assert.That(limit,Is.GreaterThan(0));Assert.That(seen.Count,Is.EqualTo(3));
            Assert.That(direction*(body.position.x-start),Is.InRange(3.3f,3.8f));
            Assert.That(Get<bool>(motor,"IsDashInvincible"),Is.False);
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
