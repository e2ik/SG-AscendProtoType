using UnityEngine;

public class MemoryFlipCapProvider : MonoBehaviour, IStatCapProvider
{
    [SerializeField] private MemoryFlipBonus flipBonus;

    public int GetMaxStat(StatType type)
    {
        if (type != StatType.Endurance || flipBonus == null) return int.MaxValue;
        return flipBonus.MaxUsefulEndurance;
    }
}