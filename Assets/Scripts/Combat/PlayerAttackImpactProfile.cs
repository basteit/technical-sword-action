using UnityEngine;

/// <summary>Normal sword attacks only. -1 inherits the shared feedback; 0 explicitly disables the stop.</summary>
[CreateAssetMenu(menuName = "Technical Sword Action/Player Attack Impact")]
public sealed class PlayerAttackImpactProfile : ScriptableObject
{
    [Tooltip("Total hitstop in 60Hz frames for steps 1-4. -1 inherits the shared feedback profile.")]
    public int[] stepHitstopFrames = { 8, 8, -1, -1 };

    public bool IsValid
    {
        get
        {
            if (stepHitstopFrames == null || stepHitstopFrames.Length != 4) return false;
            foreach (int frames in stepHitstopFrames) if (frames < -1) return false;
            return true;
        }
    }
    public int Resolve(int step, int fallback) => IsValid && step >= 1 && step <= 4 && stepHitstopFrames[step - 1] >= 0
        ? stepHitstopFrames[step - 1] : Mathf.Max(0, fallback);
}
