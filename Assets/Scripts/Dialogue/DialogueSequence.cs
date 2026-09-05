using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueKind
{
    Required,
    Npc,
    Inspect,
    Sword
}

[Serializable]
public sealed class DialogueLine
{
    [SerializeField, HideInInspector] private string stableId;
    [SerializeField] private DialogueSpeakerDefinition speaker;
    [SerializeField, TextArea(3, 8)] private string body;
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private string expressionId;
    [SerializeField] private AudioClip voice;
    [SerializeField, TextArea(1, 3)] private string authorNote;

    public string StableId => stableId;
    public DialogueSpeakerDefinition Speaker => speaker;
    public string Body => body ?? string.Empty;
    public Sprite Portrait => portraitOverride != null
        ? portraitOverride
        : speaker != null ? speaker.DefaultPortrait : null;
    public string ExpressionId => expressionId ?? string.Empty;
    public AudioClip Voice => voice;
    public string AuthorNote => authorNote ?? string.Empty;

    internal bool EnsureUniqueStableId(HashSet<string> usedIds)
    {
        if (!string.IsNullOrWhiteSpace(stableId) && usedIds.Add(stableId))
        {
            return false;
        }

        do
        {
            stableId = Guid.NewGuid().ToString("N");
        }
        while (!usedIds.Add(stableId));

        return true;
    }

    internal void RegenerateStableId(HashSet<string> usedIds)
    {
        stableId = string.Empty;
        EnsureUniqueStableId(usedIds);
    }
}

[CreateAssetMenu(menuName = "Keraunos/Dialogue/Sequence", fileName = "Dialogue_")]
public sealed class DialogueSequence : ScriptableObject
{
    [SerializeField, HideInInspector] private string stableId;
    [SerializeField] private string authoringTitle = "会話";
    [SerializeField] private DialogueKind kind = DialogueKind.Npc;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private List<DialogueLine> lines = new();

    public string StableId => stableId;
    public string AuthoringTitle => authoringTitle;
    public DialogueKind Kind => kind;
    public bool AllowSkip => allowSkip;
    public IReadOnlyList<DialogueLine> Lines => lines;
    public int LineCount => lines != null ? lines.Count : 0;

    public bool TryGetLine(int index, out DialogueLine line)
    {
        if (lines != null && index >= 0 && index < lines.Count)
        {
            line = lines[index];
            return line != null;
        }

        line = null;
        return false;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        SynchronizeStableIdsForEditor();
#endif
    }

#if UNITY_EDITOR
    public bool SynchronizeStableIdsForEditor()
    {
        bool changed = false;
        bool regenerateLineIds = false;
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        string assetGuid = string.IsNullOrEmpty(assetPath)
            ? string.Empty
            : UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrWhiteSpace(assetGuid) && stableId != assetGuid)
        {
            stableId = assetGuid;
            regenerateLineIds = true;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (lines == null)
        {
            lines = new List<DialogueLine>();
            changed = true;
        }

        HashSet<string> usedIds = new();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == null)
            {
                lines[i] = new DialogueLine();
                changed = true;
            }

            if (regenerateLineIds)
            {
                lines[i].RegenerateStableId(usedIds);
                changed = true;
            }
            else
            {
                changed |= lines[i].EnsureUniqueStableId(usedIds);
            }
        }

        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }

        return changed;
    }
#endif
}
