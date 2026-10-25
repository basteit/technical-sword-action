using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueAssetValidator
{
    private const string MenuPath = "Tools/Dialogue/Validate All Dialogue";
    private const int RecommendedMaxBodyCharacters = 90;
    private const int RecommendedMaxBodyLines = 4;

    [MenuItem(MenuPath)]
    public static void ValidateFromMenu()
    {
        ValidateAll(logSuccess: true);
    }

    public static bool ValidateAll(bool logSuccess = false)
    {
        List<string> problems = new();
        Dictionary<string, Object> sequenceIds = new();
        Dictionary<string, Object> lineIds = new();
        Dictionary<string, Object> speakerIds = new();

        ValidateSpeakers(speakerIds, problems);
        ValidateSequences(sequenceIds, lineIds, problems);
        ValidateLoadedSceneControllers(problems);

        if (problems.Count == 0)
        {
            if (logSuccess)
            {
                Debug.Log("Dialogue validation passed: stable IDs are unique and every line has body text.");
            }

            return true;
        }

        for (int i = 0; i < problems.Count; i++)
        {
            Debug.LogError(problems[i]);
        }

        Debug.LogError($"Dialogue validation failed with {problems.Count} problem(s).");
        return false;
    }

    private static void ValidateSpeakers(Dictionary<string, Object> usedIds, List<string> problems)
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueSpeakerDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DialogueSpeakerDefinition speaker = AssetDatabase.LoadAssetAtPath<DialogueSpeakerDefinition>(path);
            if (speaker == null)
            {
                continue;
            }

            speaker.SynchronizeStableIdForEditor();
            EditorUtility.SetDirty(speaker);
            AssetDatabase.SaveAssetIfDirty(speaker);

            ValidateStableId("Speaker", speaker.StableId, path, speaker, usedIds, problems);
            if (string.IsNullOrWhiteSpace(speaker.DisplayName))
            {
                problems.Add($"Dialogue Speaker has an empty Display Name: {path}");
            }
        }
    }

    private static void ValidateSequences(
        Dictionary<string, Object> sequenceIds,
        Dictionary<string, Object> lineIds,
        List<string> problems)
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueSequence");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DialogueSequence sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (sequence == null)
            {
                continue;
            }

            sequence.SynchronizeStableIdsForEditor();
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssetIfDirty(sequence);

            ValidateStableId("Dialogue Sequence", sequence.StableId, path, sequence, sequenceIds, problems);
            if (string.IsNullOrWhiteSpace(sequence.AuthoringTitle))
            {
                problems.Add($"Dialogue Sequence has an empty Authoring Title: {path}");
            }

            if (sequence.LineCount <= 0)
            {
                problems.Add($"Dialogue Sequence has no lines: {path}");
                continue;
            }

            for (int lineIndex = 0; lineIndex < sequence.LineCount; lineIndex++)
            {
                if (!sequence.TryGetLine(lineIndex, out DialogueLine line) || line == null)
                {
                    problems.Add($"Dialogue Sequence has a null line at index {lineIndex}: {path}");
                    continue;
                }

                ValidateStableId(
                    $"Dialogue Line {lineIndex}",
                    line.StableId,
                    path,
                    sequence,
                    lineIds,
                    problems);

                if (string.IsNullOrWhiteSpace(line.Body))
                {
                    problems.Add($"Dialogue Line {lineIndex} has empty body text: {path}");
                    continue;
                }

                int explicitLineCount = line.Body.Split('\n').Length;
                if (line.Body.Length > RecommendedMaxBodyCharacters ||
                    explicitLineCount > RecommendedMaxBodyLines)
                {
                    problems.Add(
                        $"Dialogue Line {lineIndex} may not fit the prototype panel " +
                        $"({line.Body.Length} characters, {explicitLineCount} explicit lines; " +
                        $"recommended <= {RecommendedMaxBodyCharacters} characters and <= {RecommendedMaxBodyLines} lines): {path}");
                }
            }
        }
    }

    private static void ValidateStableId(
        string kind,
        string stableId,
        string path,
        Object context,
        Dictionary<string, Object> usedIds,
        List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            problems.Add($"{kind} has an empty stable ID: {path}");
            return;
        }

        if (usedIds.ContainsKey(stableId))
        {
            problems.Add($"{kind} has a duplicate stable ID '{stableId}': {path}");
            return;
        }

        usedIds.Add(stableId, context);
    }

    private static void ValidateLoadedSceneControllers(List<string> problems)
    {
        DialogueController[] controllers = Object.FindObjectsByType<DialogueController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (controller == null || EditorUtility.IsPersistent(controller))
            {
                continue;
            }

            SerializedObject serializedController = new(controller);
            SerializedProperty behaviours = serializedController.FindProperty("gameplayBehavioursToDisable");
            SerializedProperty playerBody = serializedController.FindProperty("playerBody");
            SerializedProperty unifiedState = serializedController.FindProperty("playerStateMachine");
            string location = $"{controller.gameObject.scene.name}/{controller.gameObject.name}";

            if (unifiedState?.objectReferenceValue is PlayerStateMachine state)
            {
                if (playerBody?.objectReferenceValue != state.GetComponent<Rigidbody2D>())
                    problems.Add($"Dialogue Controller's state gate and Rigidbody2D refer to different Players: {location}");
                continue;
            }

            if (behaviours == null || behaviours.arraySize <= 0)
            {
                problems.Add($"Dialogue Controller has no gameplay behaviours to lock: {location}");
            }
            else
            {
                HashSet<Behaviour> assignedBehaviours = new();
                for (int behaviourIndex = 0; behaviourIndex < behaviours.arraySize; behaviourIndex++)
                {
                    Object assignedObject = behaviours.GetArrayElementAtIndex(behaviourIndex).objectReferenceValue;
                    if (assignedObject is not Behaviour assignedBehaviour)
                    {
                        problems.Add(
                            $"Dialogue Controller has an empty gameplay lock reference at index " +
                            $"{behaviourIndex}: {location}");
                        continue;
                    }

                    assignedBehaviours.Add(assignedBehaviour);
                }

                if (playerBody != null && playerBody.objectReferenceValue is Rigidbody2D body)
                {
                    ValidatePlayerLock<PlayerMotor2D>(body, assignedBehaviours, location, problems);
                    ValidatePlayerLock<PlayerAttack2D>(body, assignedBehaviours, location, problems);
                    ValidatePlayerLock<PlayerParry2D>(body, assignedBehaviours, location, problems);
                    ValidatePlayerLock<PlayerSpecialSkill2D>(body, assignedBehaviours, location, problems);
                }
            }

            if (playerBody == null || playerBody.objectReferenceValue == null)
            {
                problems.Add($"Dialogue Controller has no Player Rigidbody2D assigned: {location}");
            }
        }
    }

    private static void ValidatePlayerLock<T>(
        Rigidbody2D playerBody,
        HashSet<Behaviour> assignedBehaviours,
        string location,
        List<string> problems)
        where T : Behaviour
    {
        T playerBehaviour = playerBody.GetComponent<T>();
        if (playerBehaviour != null && !assignedBehaviours.Contains(playerBehaviour))
        {
            problems.Add(
                $"Dialogue Controller does not lock {typeof(T).Name} on the assigned Player: {location}");
        }
    }
}
