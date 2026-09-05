using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Keraunos/Dialogue/Speaker", fileName = "Speaker_")]
public sealed class DialogueSpeakerDefinition : ScriptableObject
{
    [SerializeField, HideInInspector] private string stableId;
    [SerializeField] private string displayName = "話者";
    [SerializeField] private Sprite defaultPortrait;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public Sprite DefaultPortrait => defaultPortrait;

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
