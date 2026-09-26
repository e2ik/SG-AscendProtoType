using System;
using UnityEngine;

// runtime convenience wrapper around SaveManager.CurrentSave.stats - lives on the
// Player object so it's reachable via PlayerController, but the actual data is
// owned by SaveManager (persistent) so it survives scene reloads
public class PlayerStatsController : MonoBehaviour
{
    public PlayerStats Stats => SaveManager.Instance.CurrentSave.stats;
    public event Action OnStatsChanged;

    public bool TryAllocate(StatType type, int amount = 1)
    {
        bool success = Stats.TryAllocate(type, amount);
        if (success)
        {
            OnStatsChanged?.Invoke();
            SaveManager.Instance.SaveCurrent();
        }
        return success;
    }
}