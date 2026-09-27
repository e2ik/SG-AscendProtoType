using UnityEngine;

[CreateAssetMenu(fileName = "RhythmZoneBonus", menuName = "Minigames/Rhythm Zone Bonus")]
public class RhythmZoneBonus : ScriptableObject
{
    [Range(0f, 0.2f)] public float sizePerIntelligence = 0.01f;
    [Range(0f, 1f)] public float maxBonus = 0.1f;

    public float BonusFor(int intelligence)
    {
        float bonus = Mathf.Max(0, intelligence - PlayerStats.StartingValue) * sizePerIntelligence;
        return Mathf.Min(maxBonus, bonus);
    }

    public int MaxUsefulIntelligence
    {
        get
        {
            if (sizePerIntelligence <= 0f) return PlayerStats.StartingValue;
            return PlayerStats.StartingValue + Mathf.CeilToInt(maxBonus / sizePerIntelligence - 0.0001f);
        }
    }
}