using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class StatsMenuController : MonoBehaviour
{
    public static StatsMenuController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private List<StatRowUI> rows = new List<StatRowUI>();
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;
    [SerializeField] private string confirmMessage = "Spend {0} point{1}?";

    [Header("Effect Previews")]
    [SerializeField] private MemoryFlipBonus memoryFlipBonus;

    public bool CanOpen => PlayerStatsController.Active != null;

    private readonly Dictionary<StatType, int> _pending = new Dictionary<StatType, int>();
    private PlayerStatsController _stats;
    private WorldMovementController _movement;

    private int PendingTotal
    {
        get
        {
            int total = 0;
            foreach (var amount in _pending.Values)
                total += amount;
            return total;
        }
    }

    private int Available => _stats != null ? _stats.Stats.unallocatedPoints - PendingTotal : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel.SetActive(false);

        foreach (var row in rows)
        {
            if (row == null) continue;
            var stat = row.Stat;
            if (row.Plus != null) row.Plus.onClick.AddListener(() => Add(stat));
            if (row.Minus != null) row.Minus.onClick.AddListener(() => Remove(stat));
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (resetButton != null) resetButton.onClick.AddListener(ResetPending);
        if (backButton != null) backButton.onClick.AddListener(Close);

        if (uiPanel != null)
            uiPanel.OnBack += ClearPending;
    }

    public void Open()
    {
        _stats = PlayerStatsController.Active;
        if (_stats == null)
        {
            Debug.LogWarning("No active player stats - the stats menu only works in the world", this);
            return;
        }

        _movement = _stats.GetComponent<WorldMovementController>();
        ClearPending();
        Refresh();
        UIPanelAnimator.SetVisible(panel, true);
    }

    public void Close()
    {
        ClearPending();
        UIPanelAnimator.SetVisible(panel, false);
    }

    private void Add(StatType stat)
    {
        if (!CanAdd(stat)) return;
        _pending[stat] = GetPending(stat) + 1;
        Refresh();
    }

    private void Remove(StatType stat)
    {
        int pending = GetPending(stat);
        if (pending <= 0) return;
        _pending[stat] = pending - 1;
        Refresh();
    }

    private bool CanAdd(StatType stat) =>
        _stats != null && Available > 0 && _stats.Get(stat) + GetPending(stat) + 1 <= _stats.GetMaxStat(stat);

    private int GetPending(StatType stat) => _pending.TryGetValue(stat, out int amount) ? amount : 0;

    private void ClearPending() => _pending.Clear();

    private void ResetPending()
    {
        ClearPending();
        Refresh();
    }

    private void Confirm()
    {
        int total = PendingTotal;
        if (total <= 0) return;

        string message = string.Format(confirmMessage, total, total == 1 ? "" : "s");

        if (ConfirmationDialogueController.Instance != null)
            ConfirmationDialogueController.Instance.Show(message, ApplyPending);
        else
            ApplyPending();
    }

    private void ApplyPending()
    {
        if (_stats == null) return;

        foreach (var pair in new List<KeyValuePair<StatType, int>>(_pending))
        {
            if (pair.Value > 0)
                _stats.TryAllocate(pair.Key, pair.Value);
        }

        ClearPending();
        Refresh();
    }

    private void Refresh()
    {
        if (_stats == null) return;

        if (pointsText != null)
            pointsText.text = $"Points: {Available}";

        foreach (var row in rows)
        {
            if (row == null) continue;

            var stat = row.Stat;
            int current = _stats.Get(stat);
            int pending = GetPending(stat);

            row.Display(current, pending, _stats.GetMaxStat(stat), Describe(stat, current, current + pending));

            if (row.Plus != null) row.Plus.interactable = CanAdd(stat);
            if (row.Minus != null) row.Minus.interactable = pending > 0;
        }

        bool hasPending = PendingTotal > 0;
        if (confirmButton != null) confirmButton.interactable = hasPending;
        if (resetButton != null) resetButton.interactable = hasPending;

        RescueSelection();
    }

    private string Describe(StatType stat, int now, int next)
    {
        switch (stat)
        {
            case StatType.Agility when _movement != null:
                return FormatChange("Speed", _movement.MoveSpeedAt(now), _movement.MoveSpeedAt(next));
            case StatType.Strength when _movement != null:
                return FormatChange("Jump", _movement.JumpHeightAt(now), _movement.JumpHeightAt(next));
            case StatType.Endurance when memoryFlipBonus != null:
                return FormatBonus("Memory flips", memoryFlipBonus.BonusFor(now), memoryFlipBonus.BonusFor(next));
            default:
                return null;
        }
    }

    private static string FormatBonus(string label, int now, int next) =>
        now == next ? $"{label} +{now}" : $"{label} +{now} → +{next}";

    private static string FormatChange(string label, float now, float next) =>
        Mathf.Approximately(now, next) ? $"{label} {now:0.00}" : $"{label} {now:0.00} → {next:0.00}";

    private void RescueSelection()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var selected = eventSystem.currentSelectedGameObject;
        if (selected == null) return;

        var selectable = selected.GetComponent<Selectable>();
        if (selectable == null || selectable.IsInteractable()) return;

        foreach (var row in rows)
        {
            if (row == null) continue;

            if (row.Plus != null && selected == row.Plus.gameObject)
            {
                Select(row.Minus != null && row.Minus.IsInteractable() ? row.Minus : FallbackButton());
                return;
            }

            if (row.Minus != null && selected == row.Minus.gameObject)
            {
                Select(row.Plus != null && row.Plus.IsInteractable() ? row.Plus : FallbackButton());
                return;
            }
        }

        bool onConfirm = confirmButton != null && selected == confirmButton.gameObject;
        bool onReset = resetButton != null && selected == resetButton.gameObject;
        if (onConfirm || onReset)
            Select(FallbackButton());
    }

    private Selectable FallbackButton()
    {
        if (confirmButton != null && confirmButton.IsInteractable()) return confirmButton;
        return backButton;
    }

    private static void Select(Selectable target)
    {
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }
}