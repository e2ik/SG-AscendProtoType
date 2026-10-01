using System;
using UnityEngine;

public class PlayerStatsController : MonoBehaviour
{
    [SerializeField] private PlayerStats testStats = new PlayerStats();
    [SerializeField] private bool overrideSaveStats;

    public static PlayerStatsController Active { get; private set; }

    public event Action OnStatsChanged;

    private IStatCapProvider[] _capProviders;

    public PlayerStats Stats
    {
        get
        {
            if (overrideSaveStats) return testStats;

            var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            return save != null && save.stats != null ? save.stats : testStats;
        }
    }

    public bool UsingSaveStats => Stats != testStats;

    private void Awake()
    {
        _capProviders = GetComponents<IStatCapProvider>();
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    public int Get(StatType type) => Stats.GetStat(type);

    public int GetMaxStat(StatType type)
    {
        if (_capProviders == null)
            _capProviders = GetComponents<IStatCapProvider>();

        int max = int.MaxValue;
        foreach (var provider in _capProviders)
            max = Mathf.Min(max, provider.GetMaxStat(type));
        return max;
    }

    public bool IsMaxed(StatType type) => Get(type) >= GetMaxStat(type);

    public bool CanAllocate(StatType type, int amount = 1) =>
        amount > 0 && Stats.unallocatedPoints >= amount && Get(type) + amount <= GetMaxStat(type);

    public bool TryAllocate(StatType type, int amount = 1)
    {
        if (!Stats.TryAllocate(type, amount, GetMaxStat(type))) return false;

        OnStatsChanged?.Invoke();
        TelemetryManager.Log("stat_allocate", type.ToString(), Get(type));

        if (UsingSaveStats)
            SaveManager.Instance.SaveCurrent();

        return true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            OnStatsChanged?.Invoke();
    }
}