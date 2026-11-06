using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TechnicalSwordAction.PlayerState.Tests
{
    /// <summary>Runtime-only dialogue placement. Never installs or saves anything into the playable stage.</summary>
    internal static class PrototypeDialogueFixture
    {
        private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        internal static Component Create(Component player)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PrototypeDialogueSystem.prefab");
            if (prefab == null) throw new InvalidOperationException("Retained dialogue prefab is missing.");
            var host = Object.Instantiate(prefab);
            host.name = "PrototypeDialogueSystem";
            SceneManager.MoveGameObjectToScene(host, player.gameObject.scene);
            var controller = host.GetComponent(T("DialogueController"));
            Set(controller, "playerStateMachine", player);
            Set(controller, "playerBody", player.GetComponent<Rigidbody2D>());
            Set(controller, "gameplayBehavioursToDisable", Array.Empty<Behaviour>());

            var target = new GameObject("InteractCounter");
            SceneManager.MoveGameObjectToScene(target, player.gameObject.scene);
            target.SetActive(false);
            target.transform.position = new Vector3(-6, .5f);
            var trigger = target.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(2, 2); trigger.isTrigger = true;
            var interactable = target.AddComponent(T("DialogueInteractable2D"));
            Set(interactable, "controller", controller);
            Set(interactable, "sequence", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/PrototypeDialogue/Dialogue_PrototypeTest.asset"));
            target.SetActive(true);
            Physics2D.SyncTransforms();
            return controller;
        }
    }
}
