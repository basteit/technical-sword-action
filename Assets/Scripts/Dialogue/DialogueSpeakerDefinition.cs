using System;
using UnityEngine;

public enum DialoguePortraitSide
{
    Right,
    Left
}

[CreateAssetMenu(menuName = "Keraunos/Dialogue/Speaker", fileName = "Speaker_")]
public sealed class DialogueSpeakerDefinition : ScriptableObject
{
    [SerializeField, HideInInspector] private string stableId;
    [SerializeField] private string displayName = "話者";
    [SerializeField] private Sprite defaultPortrait;
    [Tooltip("Protagonist / allied lead: Left. Other characters: Right.")]
    [SerializeField] private DialoguePortraitSide portraitSide = DialoguePortraitSide.Right;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public Sprite DefaultPortrait => defaultPortrait;
    public DialoguePortraitSide PortraitSide => portraitSide;

    private void OnValidate()
    {
#if UNITY_EDITOR
        SynchronizeStableIdForEditor();
#endif
    }

#if UNITY_EDITOR
    public bool SynchronizeStableIdForEditor()
    {
        bool changed = false;
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        string assetGuid = string.IsNullOrEmpty(assetPath)
            ? string.Empty
            : UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrWhiteSpace(assetGuid))
        {
            if (stableId != assetGuid)
            {
                stableId = assetGuid;
                changed = true;
            }
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }

        return changed;
    }
#endif
}
