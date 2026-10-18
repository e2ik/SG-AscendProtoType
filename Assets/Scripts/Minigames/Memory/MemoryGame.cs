using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MemoryGame : MinigameBase
{
    [Header("Grid")]
    [SerializeField] [Min(1)] private int columns = 4;
    [SerializeField] [Min(1)] private int rows = 3;
    [SerializeField] private RectTransform gridParent;
    [SerializeField] private MemoryCard cardPrefab;
    [SerializeField] private Vector2 spacing = Vector2.zero;
    [SerializeField] [Min(0.1f)] private float cardAspect = 0.72f;
    [SerializeField] private bool usePrefabSize;
    [SerializeField] [Min(0f)] private float maxCardWidth;

    [Header("Card Faces")]
    [SerializeField] private List<Sprite> faceSprites = new List<Sprite>();
    [SerializeField] [Range(0f, 1f)] private float reuseTintSaturation = 0.6f;
    [SerializeField] private List<Color> faceColors = new List<Color>();
    [SerializeField] private bool tintEveryPair;

    [Header("Flips (only lost on a mismatch)")]
    [SerializeField] [Min(0f)] private float missesPerPair = 0.6f;
    [SerializeField] private MemoryFlipBonus flipBonus;
    [SerializeField] [Min(0)] private int enduranceOverride;

    [Header("Pacing")]
    [SerializeField] private float mismatchRevealTime = 0.8f;

    [Header("Reward")]
    [SerializeField] [Min(0)] private int maxReward = 3;
    [SerializeField] [Min(0)] private int minWinReward = 1;
    [SerializeField] [Min(0.1f)] private float secondsPerPair = 2f;
    [SerializeField] [Min(0.1f)] private float rewardFalloffSeconds = 30f;

    [Header("UI")]
    [SerializeField] private TMP_Text flipsText;
    [SerializeField] private string flipsFormat = "Flips: {0}";

    [Header("Flips Lost Feedback")]
    [SerializeField] private Color flipsLostColor = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] [Min(0.01f)] private float flipsLostDuration = 0.45f;
    [SerializeField] [Min(0f)] private float flipsShoveAmount = 4f;
    [SerializeField] [Min(1f)] private float flipsShovesPerSecond = 30f;
    [SerializeField] [Min(1f)] private float flipsPunchScale = 1.15f;

    [Header("Testing")]
    [SerializeField] private MemoryGameConfig testConfig;
    [SerializeField] private bool previewFaceUp = true;
    [SerializeField] [Min(0)] private int previewColumns;
    [SerializeField] [Min(0)] private int previewRows;

    [Header("Sounds")]
    [SerializeField] private string flipSound;
    [SerializeField] private string matchSound;
    [SerializeField] private string mismatchSound;

    public int Pairs { get; private set; }
    public int AllowedFlips { get; private set; }
    public int FlipsLeft { get; private set; }

    public event System.Action PairMatched;
    public event System.Action PairMismatched;
    public event System.Action<bool> RunFinished;

    protected override bool HigherIsBetter => false;
    protected override bool FailedRunsCount => false;

    private readonly List<MemoryCard> _cards = new List<MemoryCard>();

    private int _columns;
    private int _rows;
    private float _missesPerPair;
    private int _maxReward;
    private float _secondsPerPair;
    private float _rewardFalloffSeconds;
    private List<Sprite> _faces;
    private MemoryCard _first;
    private MemoryCard _second;
    private MemoryCard _lastFocused;
    private bool _resolving;
    private int _matchedPairs;
    private float _elapsed;
    private bool _flipsBaseCaptured;
    private Color _flipsBaseColor;
    private Vector2 _flipsBasePosition;
    private Vector3 _flipsBaseScale;
    private Coroutine _flipsFeedback;

    protected override void OnSetup()
    {
        StopAllCoroutines();
        ResetFlipsFeedback();
        ApplyConfig();
        BuildGrid();

        AllowedFlips = Mathf.CeilToInt(Pairs * _missesPerPair) + EnduranceBonus();
        FlipsLeft = AllowedFlips;
        _matchedPairs = 0;
        _elapsed = 0f;
        _first = null;
        _second = null;
        _resolving = false;

        UpdateFlipsText();
    }

    protected override MinigameConfig Config =>
        base.Config != null ? base.Config : testConfig;

    private void ApplyConfig()
    {
        var config = GetConfig<MemoryGameConfig>();

        if (base.Config != null && config == null)
            Debug.LogWarning($"MemoryGame was started with a {base.Config.GetType().Name} - expected a MemoryGameConfig, using defaults", this);

        _columns = config != null ? config.columns : columns;
        _rows = config != null ? config.rows : rows;
        _missesPerPair = config != null ? config.missesPerPair : missesPerPair;
        _maxReward = config != null ? config.maxReward : maxReward;
        _secondsPerPair = config != null ? config.secondsPerPair : secondsPerPair;
        _rewardFalloffSeconds = config != null ? config.rewardFalloffSeconds : rewardFalloffSeconds;
        _faces = config != null && config.faceSprites != null && config.faceSprites.Count > 0 ? config.faceSprites : faceSprites;
    }

    protected override void OnBegin()
    {
        if (_cards.Count > 0)
            Focus(_cards[0]);
    }

    protected override void OnEnd()
    {
        _resolving = false;
    }

    private void Update()
    {
        if (CurrentPhase != Phase.Playing) return;

        _elapsed += Time.deltaTime;
        KeepFocusOnCards();
    }

    private int EnduranceBonus()
    {
        int endurance = enduranceOverride > 0 ? enduranceOverride : SavedEndurance();

        if (flipBonus == null)
        {
            Debug.LogWarning("MemoryGame has no Flip Bonus asset assigned - no Endurance bonus applied", this);
            return 0;
        }

        return flipBonus.BonusFor(endurance);
    }

    private static int SavedEndurance()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && save.stats != null ? save.stats.endurance : PlayerStats.StartingValue;
    }

    private void BuildGrid()
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            card.gameObject.SetActive(false);
            Destroy(card.gameObject);
        }
        _cards.Clear();

        if (gridParent != null)
        {
            for (int i = gridParent.childCount - 1; i >= 0; i--)
            {
                var child = gridParent.GetChild(i);
                if (child.GetComponent<MemoryCard>() == null) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        int cells = _columns * _rows;
        if (cells % 2 != 0)
            Debug.LogWarning($"{_columns}x{_rows} has an odd number of cells - the last cell will be left empty", this);

        Pairs = cells / 2;
        ConfigureLayout();

        var ids = new List<int>(Pairs * 2);
        for (int i = 0; i < Pairs; i++)
        {
            ids.Add(i);
            ids.Add(i);
        }
        Shuffle(ids);

        foreach (int id in ids)
        {
            var card = Instantiate(cardPrefab, gridParent);
            GetFace(id, out var sprite, out var tint);
            card.Setup(id, sprite, tint);
            card.Clicked += OnCardClicked;
            _cards.Add(card);
        }
    }

    private void ConfigureLayout()
    {
        if (gridParent == null || !gridParent.TryGetComponent<GridLayoutGroup>(out var grid)) return;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = _columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.spacing = spacing;

        if (usePrefabSize && cardPrefab != null)
        {
            grid.cellSize = ((RectTransform)cardPrefab.transform).rect.size;
            return;
        }

        var rect = gridParent.rect;
        float availableWidth = rect.width - grid.padding.horizontal - spacing.x * (_columns - 1);
        float availableHeight = rect.height - grid.padding.vertical - spacing.y * (_rows - 1);

        float cellWidth = Mathf.Min(availableWidth / _columns, availableHeight / _rows * cardAspect);
        if (maxCardWidth > 0f)
            cellWidth = Mathf.Min(cellWidth, maxCardWidth);
        cellWidth = Mathf.Max(1f, cellWidth);
        grid.cellSize = new Vector2(cellWidth, cellWidth / cardAspect);
    }

    private void GetFace(int pairId, out Sprite sprite, out Color tint)
    {
        bool hasColours = faceColors != null && faceColors.Count > 0;

        if (_faces.Count == 0)
        {
            sprite = null;
            tint = hasColours
                ? faceColors[pairId % faceColors.Count]
                : Color.HSVToRGB((float)pairId / Mathf.Max(1, Pairs), reuseTintSaturation, 1f);
            return;
        }

        sprite = _faces[pairId % _faces.Count];
        int reuse = pairId / _faces.Count;
        int reuseRounds = Mathf.CeilToInt((float)Pairs / _faces.Count);

        if (tintEveryPair && hasColours)
        {
            tint = faceColors[pairId % faceColors.Count];
            return;
        }

        if (reuse == 0)
        {
            tint = Color.white;
            return;
        }

        tint = hasColours
            ? faceColors[(reuse - 1) % faceColors.Count]
            : Color.HSVToRGB((float)reuse / Mathf.Max(1, reuseRounds), reuseTintSaturation, 1f);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void OnCardClicked(MemoryCard card)
    {
        if (CurrentPhase != Phase.Playing || _resolving || card.State != MemoryCard.CardState.Hidden) return;

        card.Reveal();
        PlaySound(flipSound);

        if (_first == null)
        {
            _first = card;
            return;
        }

        _second = card;
        StartCoroutine(ResolvePair());
    }

    private IEnumerator ResolvePair()
    {
        _resolving = true;

        var a = _first;
        var b = _second;
        _first = null;
        _second = null;

        if (a.PairId == b.PairId)
        {
            yield return new WaitForSeconds(0.15f);

            a.MarkMatched();
            b.MarkMatched();
            _matchedPairs++;
            PlaySound(matchSound);

            _resolving = false;

            if (_matchedPairs >= Pairs)
            {
                Score = _elapsed;
                RunFinished?.Invoke(true);
                EndRun(true);
            }
            else
            {
                PairMatched?.Invoke();
            }
            yield break;
        }

        FlipsLeft--;
        UpdateFlipsText();
        PlayFlipsLost();
        PlaySound(mismatchSound);
        while (a.IsFlipping || b.IsFlipping)
            yield return null;
        a.FlashWrong();
        b.FlashWrong();
        if (FlipsLeft > 0)
            PairMismatched?.Invoke();
        yield return new WaitForSeconds(mismatchRevealTime);

        a.Conceal();
        b.Conceal();
        _resolving = false;

        if (FlipsLeft <= 0)
        {
            Score = _matchedPairs;
            RunFinished?.Invoke(false);
            EndRun(false);
        }
    }

    private void UpdateFlipsText()
    {
        if (flipsText != null)
            flipsText.text = string.Format(flipsFormat, FlipsLeft, AllowedFlips);
    }

    private void CaptureFlipsBase()
    {
        if (_flipsBaseCaptured || flipsText == null) return;
        _flipsBaseCaptured = true;
        _flipsBaseColor = flipsText.color;
        _flipsBasePosition = flipsText.rectTransform.anchoredPosition;
        _flipsBaseScale = flipsText.rectTransform.localScale;
    }

    private void ResetFlipsFeedback()
    {
        if (_flipsFeedback != null)
        {
            StopCoroutine(_flipsFeedback);
            _flipsFeedback = null;
        }

        if (flipsText == null || !_flipsBaseCaptured) return;
        flipsText.color = _flipsBaseColor;
        flipsText.rectTransform.anchoredPosition = _flipsBasePosition;
        flipsText.rectTransform.localScale = _flipsBaseScale;
    }

    private void PlayFlipsLost()
    {
        if (flipsText == null) return;
        CaptureFlipsBase();
        ResetFlipsFeedback();
        _flipsFeedback = StartCoroutine(FlipsLostRoutine());
    }

    private IEnumerator FlipsLostRoutine()
    {
        var rect = flipsText.rectTransform;
        float shoveTimer = 0f;
        Vector2 offset = Vector2.zero;

        for (float t = 0f; t < flipsLostDuration; t += Time.deltaTime)
        {
            float p = t / flipsLostDuration;
            float fade = 1f - p;

            shoveTimer += Time.deltaTime;
            if (shoveTimer >= 1f / flipsShovesPerSecond)
            {
                shoveTimer = 0f;
                float strength = flipsShoveAmount * fade;
                offset = new Vector2(Mathf.Round(Random.Range(-strength, strength)), Mathf.Round(Random.Range(-strength, strength)));
            }

            float pulse = p < 0.15f ? p / 0.15f : 1f - (p - 0.15f) / 0.85f;
            rect.anchoredPosition = _flipsBasePosition + offset;
            rect.localScale = _flipsBaseScale * Mathf.Lerp(1f, flipsPunchScale, pulse);
            flipsText.color = Color.Lerp(_flipsBaseColor, flipsLostColor, pulse);
            yield return null;
        }

        rect.anchoredPosition = _flipsBasePosition;
        rect.localScale = _flipsBaseScale;
        flipsText.color = _flipsBaseColor;
        _flipsFeedback = null;
    }

    private void KeepFocusOnCards()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var selected = eventSystem.currentSelectedGameObject;
        if (selected != null && selected.TryGetComponent<MemoryCard>(out var focused))
        {
            _lastFocused = focused;
            return;
        }

        if (selected == null && _lastFocused != null)
            Focus(_lastFocused);
    }

    private static void Focus(MemoryCard card)
    {
        if (card != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(card.gameObject);
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }

#if UNITY_EDITOR
    [ContextMenu("Preview Grid")]
    private void PreviewGrid()
    {
        if (Application.isPlaying || gridParent == null || cardPrefab == null) return;

        ClearPreview();
        ApplyConfig();

        if (previewColumns > 0) _columns = previewColumns;
        if (previewRows > 0) _rows = previewRows;

        Pairs = _columns * _rows / 2;
        ConfigureLayout();

        for (int i = 0; i < Pairs * 2; i++)
        {
            int id = i / 2;
            var card = Instantiate(cardPrefab, gridParent);
            card.gameObject.name = $"PreviewCard {i}";
            card.gameObject.hideFlags = HideFlags.DontSave;
            GetFace(id, out var sprite, out var tint);
            card.Setup(id, sprite, tint);
            card.ShowFaceInstant(previewFaceUp);
        }

        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Clear Preview")]
    private void ClearPreview()
    {
        if (Application.isPlaying || gridParent == null) return;

        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            var child = gridParent.GetChild(i);
            if ((child.gameObject.hideFlags & HideFlags.DontSave) != 0 && child.GetComponent<MemoryCard>() != null)
                DestroyImmediate(child.gameObject);
        }

        UnityEditor.SceneView.RepaintAll();
    }
#endif

    protected override int CalculateReward(float bestTime)
    {
        float target = Pairs * _secondsPerPair;
        if (bestTime <= target) return _maxReward;

        float t = Mathf.Clamp01((bestTime - target) / _rewardFalloffSeconds);
        return Mathf.RoundToInt(Mathf.Lerp(_maxReward, Mathf.Min(minWinReward, _maxReward), t));
    }

    protected override string FormatScore(float seconds) => $"{seconds:0.0}s";

    protected override string FormatRunResult(float score, bool success) =>
        success ? FormatScore(score) : "DSQ";

    protected override string BuildInstructions(string template) =>
        (template ?? string.Empty)
            .Replace("{pairs}", Pairs.ToString())
            .Replace("{flips}", AllowedFlips.ToString());
}