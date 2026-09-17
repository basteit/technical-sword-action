using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TechnicalSwordAction.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class PlayerAttackMotionTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> objects = new();
        private Component state;
        private Component motor;
        private Component attack;
        private Rigidbody2D body;
        private Behaviour timeController;
        private Type timeType;
        private bool savedManualAdvance;
        private float savedScale;
        private float savedFixedStep;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedScale = Time.timeScale;
            savedFixedStep = Time.fixedDeltaTime;
            timeType = Type.GetType("CombatTimeController, Assembly-CSharp", true);
            timeController = (Behaviour)Object.FindFirstObjectByType(timeType);
            Assert.That(timeController, Is.Not.Null);
            savedManualAdvance = Get<bool>(timeController, "ManualAdvanceOnly");
            timeType.GetProperty("ManualAdvanceOnly", Flags).SetValue(timeController, true);
            CallStatic(timeType, "SetPaused", false);
            CallStatic(timeType, "ResetSession");

            GameObject player = new("PlayerAttackMotionTestPlayer");
            objects.Add(player);
            player.SetActive(false);
            body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            player.AddComponent<BoxCollider2D>().isTrigger = true;
            state = Add(player, "PlayerStateMachine");
            motor = Add(player, "PlayerMotor2D");
            attack = Add(player, "PlayerAttack2D");
            player.SetActive(true);
            yield return null;
            ResetFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
            {
                if (item != null) Object.DestroyImmediate(item);
            }

            objects.Clear();
            if (timeController != null)
            {
                timeController.enabled = true;
                CallStatic(timeType, "SetPaused", false);
                CallStatic(timeType, "ResetSession");
                timeType.GetProperty("ManualAdvanceOnly", Flags).SetValue(timeController, savedManualAdvance);
            }

            Time.timeScale = savedScale;
            Time.fixedDeltaTime = savedFixedStep;
            yield return null;
        }

        [Test]
        public void GroundAttackOwnsHorizontalMotionAndIgnoresInput()
        {
            SetField(motor, "isGrounded", true);
            SetField(motor, "moveInput", -1f);
            StartAttack();

            float moveSpeed = (float)GetField(motor, "moveSpeed").GetValue(motor);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f), "The first attack step should move toward facing direction.");
            Assert.That(body.linearVelocity.x, Is.LessThan(moveSpeed), "Ground attack motion should use its configured step speed.");

            for (int i = 0; i < 20; i++)
            {
                Call(attack, "CombatTickTimers");
            }

            Call(state, "CombatTick");
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.0001f),
                "Movement input must not take over after the step motion window closes.");
        }

        [Test]
        public void AirAttackRetainsReducedSteering()
        {
            SetField(motor, "isGrounded", false);
            SetField(motor, "moveInput", 1f);
            StartAttack();

            float moveSpeed = (float)GetField(motor, "moveSpeed").GetValue(motor);
            float multiplier = Get<float>(attack, "AirMoveSpeedMultiplier");
            Assert.That(multiplier, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(body.linearVelocity.x, Is.EqualTo(moveSpeed * multiplier).Within(0.0001f));
            Assert.That(body.linearVelocity.x, Is.LessThan(moveSpeed));
        }

        [Test]
        public void PerStepMotionSettingsSupportAConfigurableBackwardStep()
        {
            Array steps = (Array)GetField(attack, "attackSteps").GetValue(attack);
            object firstStep = steps.GetValue(0);
            Field(firstStep, "groundMotionDistance", -0.25f);
            steps.SetValue(firstStep, 0);
            SetField(attack, "attackSteps", steps);

            SetField(motor, "isGrounded", true);
            SetField(motor, "moveInput", 1f);
            StartAttack();

            Assert.That(Get<float>(attack, "GroundMotionDistance"), Is.EqualTo(-0.25f).Within(0.0001f));
            Assert.That(body.linearVelocity.x, Is.LessThan(0f),
                "A negative per-step distance should move back from the facing direction.");
        }

        [Test]
        public void CurveSupportsDelayedAdvancePauseAndRetreat()
        {
            Array steps = (Array)GetField(attack, "attackSteps").GetValue(attack);
            object firstStep = steps.GetValue(0);
            Field(firstStep, "useGroundMotionCurve", true);
            Field(firstStep, "groundMotionCurve", new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.1f, 0f, 0f, 2f),
                new Keyframe(0.2f, 0.2f, 2f, 0f),
                new Keyframe(0.3f, 0.2f, 0f, -1f),
                new Keyframe(0.4f, 0.1f, -1f, 0f)));
            steps.SetValue(firstStep, 0);
            SetField(attack, "attackSteps", steps);
            SetField(motor, "isGrounded", true);
            StartAttack();
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(0.0001f));
            SetField(attack, "groundMotionElapsed", 0.12f);
            Assert.That(Get<float>(attack, "GroundMotionVelocity"), Is.GreaterThan(0f));
            SetField(attack, "groundMotionElapsed", 0.22f);
            Assert.That(Get<float>(attack, "GroundMotionVelocity"), Is.EqualTo(0f).Within(0.0001f));
            SetField(attack, "groundMotionElapsed", 0.32f);
            Assert.That(Get<float>(attack, "GroundMotionVelocity"), Is.LessThan(0f));
            Call(attack, "CancelAttack", "TestCancel");
            Assert.That(Get<float>(attack, "GroundMotionVelocity"), Is.EqualTo(0f));
        }

        private void StartAttack()
        {
            Assert.That((bool)Call(state, "RequestAction", PlayerActionRequest.Attack), Is.True);
            Call(state, "CombatTick");
            Assert.That(Get<PlayerActionState>(state, "ActionState"), Is.EqualTo(PlayerActionState.Attack));
        }

        private void ResetFixture()
        {
            Call(state, "ResetToSafeState", "MotionFixtureReset", true);
            SetField(motor, "isGrounded", true);
            SetField(motor, "moveInput", 0f);
            body.position = Vector2.zero;
            body.linearVelocity = Vector2.zero;
        }

        private static Component Add(GameObject item, string typeName)
        {
            return item.AddComponent(Type.GetType(typeName + ", Assembly-CSharp", true));
        }

        private static T Get<T>(object item, string name)
        {
            PropertyInfo property = item.GetType().GetProperty(name, Flags);
            return (T)property.GetValue(item);
        }

        private static FieldInfo GetField(object item, string name)
        {
            return item.GetType().GetField(name, Flags);
        }

        private static void SetField(object item, string name, object value)
        {
            GetField(item, name).SetValue(item, value);
        }

        private static void Field(object item, string name, object value)
        {
            GetField(item, name).SetValue(item, value);
        }

        private static object Call(object item, string name, params object[] args)
        {
            foreach (MethodInfo method in item.GetType().GetMethods(Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                bool match = true;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return method.Invoke(item, args);
                }
            }

            throw new MissingMethodException(item.GetType().Name, name);
        }

        private static object CallStatic(Type type, string name, params object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | Flags))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                bool match = true;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return method.Invoke(null, args);
                }
            }

            throw new MissingMethodException(type.Name, name);
        }
    }
}
