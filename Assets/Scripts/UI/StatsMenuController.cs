using System.Collections;
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
    [SerializeField] private bool closeOnConfirm = true;

    [System.Serializable]
    private class StatBarkLines
    {
        public StatType stat;
        [TextArea(1, 2)] public List<string> lines = new List<string>();
    }

    [Header("Upgrade Barks")]
    [SerializeField] private bool barkOnUpgrade = true;
    [SerializeField] [Min(0f)] private float upgradeBarkDelay = 0.35f;
    [SerializeField] [Min(0f)] private float upgradeBarkDuration = 1.4f;
    [SerializeField] [Min(0.01f)] private float upgradeBarkScale = 1f;
    [SerializeField] private List<StatBarkLines> upgradeLines = new List<StatBarkLines>
    {
        new StatBarkLines { stat = StatType.Strength, lines = new List<string> { "I feel stronger!", "My arms feel solid.", "Bring on those gates." } },
        new StatBarkLines { stat = StatType.Agility, lines = new List<string> { "I feel lighter on my feet!", "Feeling quick!", "I could outrun anything." } },
        new StatBarkLines { stat = StatType.Intelligence, lines = new List<string> { "My head feels clearer.", "Easier to focus now.", "Sharper already." } },
        new StatBarkLines { stat = StatType.Endurance, lines = new List<string> { "I could keep going all day.", "Feeling steady.", "I won't give up so easily." } }
    };
    [SerializeField] [TextArea(1, 2)] private List<string> mixedUpgradeLines = new List<string>
    {
        "I feel so much better!",
        "Everything feels a bit easier.",
        "Okay. I can do this."
    };

    [Header("Open Action")]
    [SerializeField] private string openActionPath = "Player/Stats";
    [SerializeField] private bool actionClosesMenu = true;

    [Header("Effect Previews")]
    [SerializeField] private MemoryFlipBonus memoryFlipBonus;
    [SerializeField] private RhythmZoneBonus rhythmZoneBonus;
    [SerializeField] private string jumpLabel = "Jump Height";
    [SerializeField] private string speedLabel = "Run Speed";
    [SerializeField] private string concentrationLabel = "Concentration";
    [SerializeField] private string cardFlipsLabel = "Extra Card Flips";
    [SerializeField] private string arrow = ">>";
    [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.2f);

    public bool CanOpen => PlayerStatsController.Active != null;
    public bool IsOpen => _locked;

    private readonly Dictionary<StatType, int> _pending = new Dictionary<StatType, int>();
    private PlayerStatsController _stats;
    private WorldMovementController _movement;
    private bool _locked;
    private bool _frozeMovement;
    private float _previousTimeScale = 1f;
    private UnityEngine.InputSystem.InputAction _openAction;

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
        {
            uiPanel.OnBack += HandleBack;
            uiPanel.InterceptBack = ClearPendingOnBack;
        }
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(openActionPath) || InputManager.Instance == null) return;

        _openAction = InputManager.Instance.FindAction(openActionPath, this);
        if (_openAction != null)
            _openAction.performed += OnOpenAction;
    }

    private void OnOpenAction(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (_locked)
        {
            if (actionClosesMenu && IsTopLayer())
                Close();
            return;
        }

        TryOpen();
    }

    private bool IsTopLayer()
    {
        var ui = UIManager.Instance;
        if (ui == null || ui.TopLayerRoot == null) return true;
        var top = ui.TopLayerRoot;
        return top == panel.transform || top.IsChildOf(panel.transform) || panel.transform.IsChildOf(top);
    }

    public bool TryOpen()
    {
        if (_locked || !CanOpen) return false;

        var game = GameManager.Instance;
        if (game == null || game.IsTransitioning || game.CurrentState != GameState.World) return false;

        var ui = UIManager.Instance;
        if (ui != null && (ui.IsAnyUIOpen || ui.CancelHandledThisFrame)) return false;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return false;
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused) return false;

        Open();
        return _locked;
    }

    private void OnDestroy()
    {
        if (_openAction != null)
            _openAction.performed -= OnOpenAction;

        Unlock();
        if (Instance == this)
            Instance = null;
    }

    private void HandleBack()
    {
        ClearPending();
        Unlock();
    }

    private void Lock()
    {
        if (_locked) return;
        _locked = true;

        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        _frozeMovement = _movement != null && _movement.InputEnabled;
        if (_frozeMovement)
            _movement.SetInputEnabled(false);
    }

    private void Unlock()
    {
        if (!_locked) return;
        _locked = false;

        Time.timeScale = _previousTimeScale;

        if (_frozeMovement && _movement != null)
            _movement.SetInputEnabled(true);
        _frozeMovement = false;
    }

    private bool ClearPendingOnBack()
    {
        if (PendingTotal <= 0) return false;

        ResetPending();
        return true;
    }

    public void Open()
    {
        if (_locked) return;

        _stats = PlayerStatsController.Active;
        if (_stats == null)
        {
            Debug.LogWarning("No active player stats - the stats menu only works in the world", this);
            return;
        }

        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            PauseMenuController.Instance.Resume();

        _movement = _stats.GetComponent<WorldMovementController>();
        Lock();
        ClearPending();
        Refresh();
        UIPanelAnimator.SetVisible(panel, true);
        TutorialEvents.Raise(TutorialEvents.StatsMenuOpened);
    }

    public void Close()
    {
        if (!_locked) return;

        ClearPending();
        UIPanelAnimator.SetVisible(panel, false);
        Unlock();
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

        bool spent = false;
        var raised = new List<StatType>();
        foreach (var pair in new List<KeyValuePair<StatType, int>>(_pending))
        {
            if (pair.Value > 0 && _stats.TryAllocate(pair.Key, pair.Value))
            {
                spent = true;
                raised.Add(pair.Key);
            }
        }

        if (spent && barkOnUpgrade)
            QueueUpgradeBark(raised);

        ClearPending();
        Refresh();

        if (spent)
            TutorialEvents.Raise(TutorialEvents.StatPointsSpent);

        if (spent && closeOnConfirm)
            Close();
    }

    private void QueueUpgradeBark(List<StatType> raised)
    {
        var barker = _stats != null ? _stats.GetComponentInChildren<Barker>() : null;
        if (barker == null || raised.Count == 0) return;

        List<string> lines = null;
        if (raised.Count > 1)
        {
            lines = mixedUpgradeLines;
        }
        else
        {
            foreach (var entry in upgradeLines)
            {
                if (entry.stat == raised[0])
                {
                    lines = entry.lines;
                    break;
                }
            }
        }

        if (lines == null || lines.Count == 0) return;
        string line = lines[Random.Range(0, lines.Count)];
        barker.StartCoroutine(UpgradeBarkRoutine(barker, line));
    }

    private IEnumerator UpgradeBarkRoutine(Barker barker, string line)
    {
        yield return null;
        while (barker != null && (Time.timeScale <= 0f || (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen)))
            yield return null;

        if (upgradeBarkDelay > 0f)
            yield return new WaitForSeconds(upgradeBarkDelay);

        if (barker != null)
            barker.Say(line, upgradeBarkDuration, upgradeBarkScale);
    }

    private void Refresh()
    {
        if (_stats == null) return;

        var previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (pointsText != null)
            pointsText.text = $"Points: {Available}";

        foreach (var row in rows)
        {
            if (row == null) continue;

            var stat = row.Stat;
            int current = _stats.Get(stat);
            int pending = GetPending(stat);

            row.Display(current, pending, _stats.GetMaxStat(stat), Describe(stat, current, current + pending));

            row.SetButtonStates(CanAdd(stat), pending > 0);
        }

        bool hasPending = PendingTotal > 0;
        if (confirmButton != null) confirmButton.interactable = hasPending;
        if (resetButton != null) resetButton.interactable = hasPending;

        RescueSelection(previousSelection);
    }

    private string Describe(StatType stat, int now, int next)
    {
        switch (stat)
        {
            case StatType.Agility when _movement != null:
                return FormatChange(speedLabel, _movement.MoveSpeedAt(now), _movement.MoveSpeedAt(next));
            case StatType.Strength when _movement != null:
                return FormatChange(jumpLabel, _movement.JumpHeightAt(now), _movement.JumpHeightAt(next));
            case StatType.Endurance when memoryFlipBonus != null:
                return FormatBonus(cardFlipsLabel, memoryFlipBonus.BonusFor(now), memoryFlipBonus.BonusFor(next));
            case StatType.Intelligence when rhythmZoneBonus != null:
                return FormatPercentBonus(concentrationLabel, rhythmZoneBonus.BonusFor(now), rhythmZoneBonus.BonusFor(next));
            default:
                return null;
        }
    }

    private string Next(string value) => $" <color=#{ColorUtility.ToHtmlStringRGBA(arrowColor)}><b>{arrow} {value}</b></color>";

    private string FormatPercentBonus(string label, float now, float next)
    {
        int nowPercent = Mathf.RoundToInt(now * 100f);
        int nextPercent = Mathf.RoundToInt(next * 100f);
        return nowPercent == nextPercent ? $"{label} {nowPercent}%" : $"{label} {nowPercent}%{Next($"{nextPercent}%")}";
    }

    private string FormatBonus(string label, int now, int next) =>
        now == next ? $"{label} {now}" : $"{label} {now}{Next(next.ToString())}";

    private string FormatChange(string label, float now, float next) =>
        Mathf.Approximately(now, next) ? $"{label} {now:0.00}" : $"{label} {now:0.00}{Next(next.ToString("0.00"))}";

    private void RescueSelection(GameObject selected)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || selected == null) return;

        var selectable = selected.GetComponent<Selectable>();
        if (selectable == null) return;

        if (selectable.IsInteractable())
        {
            if (eventSystem.currentSelectedGameObject != selected)
                Select(selectable);
            return;
        }

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