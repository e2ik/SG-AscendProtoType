using UnityEngine;

public class RhythmZoneCapProvider : MonoBehaviour, IStatCapProvider
{
    [SerializeField] private RhythmZoneBonus zoneBonus;

    public int GetMaxStat(StatType type)
    {
        if (type != StatType.Intelligence || zoneBonus == null) return int.MaxValue;
        return zoneBonus.MaxUsefulIntelligence;
    }
}