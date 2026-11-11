using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    public sealed class ParryShockwaveContractTests
    {
        private readonly List<object> pulses = new();
        private readonly List<GameObject> cameras = new();
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
        private static object Static(string name, params object[] args) => T("ParryShockwaveFeature").GetMethod(name).Invoke(null, args);
        private static V Get<V>(object obj, string field) => (V)(obj.GetType().GetField(field)?.GetValue(obj) ?? obj.GetType().GetProperty(field).GetValue(obj));
        private static void Pause(bool paused) => T("CombatTimeController").GetMethod("SetPaused").Invoke(null, new object[] { paused });
        private Camera Camera()
        {
            var go = new GameObject("ShockwaveTestCamera"); cameras.Add(go);
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10);
            return camera;
        }
        private object Pulse() { var pulse = Activator.CreateInstance(T("ParryShockwavePulse")); pulses.Add(pulse); return pulse; }
        [SetUp] public void Setup() => Pause(false);
        [TearDown] public void Teardown()
        {
            Pause(false);
            foreach (var pulse in pulses) Call(pulse, "Reset"); pulses.Clear();
            foreach (var go in cameras) Object.DestroyImmediate(go); cameras.Clear();
        }

        [Test] public void ParryShockwaveKeepsContactWorldPositionAndExpiresWithoutChangingCamera()
        {
            var camera = Camera(); var pulse = Pulse(); var contact = new Vector3(-1.25f, 2.75f);
            Assert.That(Call(pulse, "Begin", camera, contact, .65f, .24f), Is.True);
            var sample = Static("GetSample", camera);
            Assert.That(Get<Vector3>(sample, "worldCenter"), Is.EqualTo(contact));
            Assert.That(Get<float>(sample, "progress"), Is.Zero);
            Call(pulse, "Advance", .12f, true);
            Assert.That(Get<float>(Static("GetSample", camera), "progress"), Is.EqualTo(.5f).Within(.0001));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0, 0, -10)));
            Call(pulse, "Advance", .12f, true);
            Assert.That(Static("IsVisible", camera), Is.False);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void ParryShockwaveRejectsNonPositiveOrNonFiniteStrength(float strength)
        { var camera = Camera(); Assert.That(Call(Pulse(), "Begin", camera, Vector3.zero, strength, .24f), Is.False); Assert.That(Static("IsVisible", camera), Is.False); }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)]
        public void ParryShockwaveRejectsInvalidLifetime(float seconds)
        { Assert.That(Call(Pulse(), "Begin", Camera(), Vector3.zero, .65f, seconds), Is.False); }
        [Test] public void ParryShockwaveRejectsMissingCameraAndInvalidContact()
        {
            var pulse = Pulse(); Assert.That(Call(pulse, "Begin", null, Vector3.zero, .65f, .24f), Is.False);
            Assert.That(Call(pulse, "Begin", Camera(), new Vector3(float.NaN, 0), .65f, .24f), Is.False);
        }
        [Test] public void ParryShockwaveDisablingClearsEvenWithoutAdvancingTime()
        {
            var camera = Camera(); var pulse = Pulse(); Call(pulse, "Begin", camera, Vector3.zero, .65f, .24f);
            Call(pulse, "Advance", 0f, false); Assert.That(Static("IsVisible", camera), Is.False);
        }
        [Test] public void ParryShockwaveIsCameraLocalAndOldOwnerCannotClearANewerPulse()
        {
            var camera = Camera(); var other = Camera(); var first = Pulse(); var second = Pulse();
            Call(first, "Begin", camera, Vector3.left, .5f, .24f);
            Assert.That(Static("IsVisible", other), Is.False);
            Call(second, "Begin", camera, Vector3.right, 1f, .24f);
            Call(first, "Advance", .1f, true); Call(first, "Reset");
            Assert.That(Static("IsVisible", camera), Is.True);
            Assert.That(Get<Vector3>(Static("GetSample", camera), "worldCenter"), Is.EqualTo(Vector3.right));
            Call(second, "Reset"); Assert.That(Static("IsVisible", camera), Is.False);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void ParryShockwaveIgnoresInvalidElapsedTime(float seconds)
        {
            var camera = Camera(); var pulse = Pulse(); Call(pulse, "Begin", camera, Vector3.zero, .65f, .24f);
            Call(pulse, "Advance", seconds, true); Assert.That(Get<float>(pulse, "Elapsed"), Is.Zero);
            Assert.That(Get<float>(Static("GetSample", camera), "progress"), Is.Zero);
        }
        [Test] public void ParryShockwaveRendererHasAValidMaterialAndIndependentIntensity()
        {
            var feature = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Renderer2D.asset").Single(x => x.GetType().Name == "ParryShockwaveFeature");
            var material = Get<Material>(feature, "passMaterial");
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Is.EqualTo("TechnicalSwordAction/ParryShockwave"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(Get<bool>(feature, "fetchColorBuffer"), Is.True);
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeCombat/FeedbackSettings.asset");
            Assert.That(Get<float>(settings, "parryShockwave"), Is.EqualTo(.65f));
        }
    }
}
