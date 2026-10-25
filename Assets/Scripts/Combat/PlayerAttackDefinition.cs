using System;
using UnityEngine;

public enum AttackPhase { None, Startup, Active, Cancel, Recovery }

[CreateAssetMenu(menuName = "Technical Sword Action/Player Four Step Attack")]
public sealed class PlayerAttackDefinition : ScriptableObject
{
    [Serializable]
    public sealed class Step
    {
        public AnimationClip clip;
        public string animatorState;
        [Min(1)] public int damage = 10;
        [Min(0)] public int activeStart = 15;
        [Min(0)] public int activeEnd = 16;
        [Min(0)] public int cancelStart = 17;
        [Min(0)] public int recoveryStart = 35;
        [Min(1)] public int endFrame = 50;
        [Min(0)] public int gaugeGain = 2;
        [Min(.01f)] public float radius = .75f;
        public float motionDistance = .1f;
        [Min(1)] public int motionFrames = 8;
        public bool IsValid => damage > 0 && activeStart >= 0 && activeEnd >= activeStart &&
            cancelStart == activeEnd + 1 && recoveryStart > cancelStart && endFrame > recoveryStart && radius > 0;
    }

    public Step[] steps = { new Step(), new Step(), new Step { damage = 15 }, new Step { damage = 25, gaugeGain = 5 } };
    public bool IsValid
    {
        get
        {
            if (steps == null || steps.Length != 4) return false;
            foreach (Step step in steps) if (step == null || !step.IsValid || step.clip == null) return false;
            return steps[3].damage > Mathf.Max(steps[0].damage, Mathf.Max(steps[1].damage, steps[2].damage));
        }
    }
}
