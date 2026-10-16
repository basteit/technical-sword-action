using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrototypeCombatValidator
{
    [MenuItem("Tools/Technical Sword Action/Prototype/Validate Combat Assets")]
    public static void ValidateMenu()
    {
        string[] errors = Validate();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("Prototype combat definitions, clips and scene references validated.");
    }
    public static string[] Validate()
    {
        var errors = new List<string>();
        const string folder = "Assets/Settings/PrototypeCombat/";
        var attack = AssetDatabase.LoadAssetAtPath<PlayerAttackDefinition>(folder + "FourStepAttack.asset");
        var counter = AssetDatabase.LoadAssetAtPath<ParryCounterDefinition>(folder + "ParryCounter.asset");
        var parry = AssetDatabase.LoadAssetAtPath<ParryTimingDefinition>(folder + "ParryTiming.asset");
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyCombatDefinition>(folder + "NormalMelee.asset");
        if (attack == null || !attack.IsValid) errors.Add("Invalid/missing four-step attack data or clip.");
        if (counter == null || !counter.IsValid) errors.Add("Invalid/missing counter data.");
        if (parry == null || !parry.IsValid) errors.Add("Invalid/missing parry timing.");
        if (enemy == null || !enemy.IsValid) errors.Add("Invalid/missing enemy combat data.");
        if (attack != null && attack.IsValid)
            foreach (var step in attack.steps)
            {
                AnimationEvent hit = Array.Find(AnimationUtility.GetAnimationEvents(step.clip), e => e.functionName == "OnAttackHit");
                if (hit == null || Mathf.Abs(hit.time * 60 * (step.endFrame / (step.clip.length * 60)) - step.activeStart) > 2.001f)
                    errors.Add(step.clip.name + ": visual hit marker differs by more than 2F.");
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(step.clip))
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(step.clip, binding))
                        if (key.value == null) errors.Add(step.clip.name + ": missing object curve key.");
            }
        Scene scene = SceneManager.GetSceneByPath(PrototypeCombatSceneBuilder.ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        Scene previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(PrototypeCombatSceneBuilder.ScenePath, OpenSceneMode.Additive);
        try
        {
            int players = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        errors.Add(transform.name + ": missing script.");
                    if (transform.GetComponent<PlayerStateMachine>() != null) players++;
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var serialized = new SerializedObject(component);
                        var property = serialized.GetIterator();
                        while (property.NextVisible(true))
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                                errors.Add(transform.name + ": missing reference " + property.propertyPath);
                    }
                }
            if (players != 1) errors.Add("Validation scene must contain exactly one player.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        return errors.ToArray();
    }
}
