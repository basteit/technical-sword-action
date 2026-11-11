using System;
using UnityEngine;

public enum StudioEnemyMode { Passive, SingleAttack, RepeatAttack, Chase }
public enum StudioGimmickKind { Block, OneWayPlatform, MovingPlatform }

[Serializable]
public sealed class StudioEnemySetup
{
    public GameObject prefab;
    public Vector2 position = new Vector2(1.25f, 1);
    [Min(1)] public int hp = 20;
    public StudioEnemyMode mode;
    [Min(.1f)] public float windupSeconds = .65f;
    [Min(.1f)] public float intervalSeconds = 1.2f;
    [Min(0)] public int damage = 1;
    [Min(0)] public float moveSpeed = 1.5f;
    public StudioEnemySetup Copy() => (StudioEnemySetup)MemberwiseClone();
}

[Serializable]
public sealed class StudioGimmickSetup
{
    public StudioGimmickKind kind;
    public Vector2 position = new Vector2(3, .5f);
    public Vector2 size = new Vector2(2, 1);
    public Vector2 travel = new Vector2(2, 0);
    [Min(.2f)] public float periodSeconds = 3;
    public StudioGimmickSetup Copy() => (StudioGimmickSetup)MemberwiseClone();
}

/// <summary>Reusable staging data. Applying a set replaces only the studio-managed roots.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Video/Studio Set", fileName = "StudioSet")]
public sealed class StudioPreset : ScriptableObject
{
    public string displayName = "新しいセット";
    [TextArea] public string description;
    public Vector2 playerPosition = Vector2.up;
    public bool faceLeft;
    public bool followCamera = true;
    public float cameraSize = VideoCombatPresentation.DefaultNormalSize;
    public Vector2 cameraCenter = new Vector2(0,1.05f);
    public StudioEnemySetup[] enemies = Array.Empty<StudioEnemySetup>();
    public StudioGimmickSetup[] gimmicks = Array.Empty<StudioGimmickSetup>();
}
