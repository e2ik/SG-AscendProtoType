using UnityEngine;

[CreateAssetMenu(fileName = "MemoryFlipBonus", menuName = "Minigames/Memory Flip Bonus")]
public class MemoryFlipBonus : ScriptableObject
{
    [Min(0)] public int baseExtraFlips = 2;
    [Min(0f)] public float extraFlipsPerEndurance = 1f;
    [Min(0)] public int maxExtraFlips = 10;

    public int BonusFor(int endurance)
    {
        int bonus = baseExtraFlips + Mathf.FloorToInt(Mathf.Max(0, endurance - PlayerStats.StartingValue) * extraFlipsPerEndurance);
        return Mathf.Min(maxExtraFlips, bonus);
    }

    public int MaxUsefulEndurance
    {
        get
        {
            if (extraFlipsPerEndurance <= 0f) return PlayerStats.StartingValue;

            int pointsNeeded = Mathf.CeilToInt(Mathf.Max(0, maxExtraFlips - baseExtraFlips) / extraFlipsPerEndurance);
            return PlayerStats.StartingValue + pointsNeeded;
        }
    }
}