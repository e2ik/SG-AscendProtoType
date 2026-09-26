using System;
using UnityEngine;

public enum StatType
{
    Strength,
    Agility,
    Intelligence,
    Endurance
}

[Serializable]
public class PlayerStats
{
    public int strength;
    public int agility;
    public int intelligence;
    public int endurance;
    public int unallocatedPoints; // earned from minigames, spent via TryAllocate

    public int GetStat(StatType type) => type switch
    {
        StatType.Strength => strength,
        StatType.Agility => agility,
        StatType.Intelligence => intelligence,
        StatType.Endurance => endurance,
        _ => 0
    };

    public void GrantPoints(int amount)
    {
        if (amount > 0) unallocatedPoints += amount;
    }

    public bool TryAllocate(StatType type, int amount = 1)
    {
        if (amount <= 0 || unallocatedPoints < amount) return false;

        switch (type)
        {
            case StatType.Strength: strength += amount; break;
            case StatType.Agility: agility += amount; break;
            case StatType.Intelligence: intelligence += amount; break;
            case StatType.Endurance: endurance += amount; break;
        }

        unallocatedPoints -= amount;
        return true;
    }
}