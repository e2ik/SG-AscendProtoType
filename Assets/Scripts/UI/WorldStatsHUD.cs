using UnityEngine;
using TMPro;

public class WorldStatsHUD : MonoBehaviour
{
    [Header("Stat Texts")]
    [SerializeField] private TMP_Text strengthText;
    [SerializeField] private TMP_Text agilityText;
    [SerializeField] private TMP_Text intelligenceText;
    [SerializeField] private TMP_Text enduranceText;

    [Header("Formats ({0} = value, {1} = max)")]
    [SerializeField] private string strengthFormat = "STR {0}";
    [SerializeField] private string agilityFormat = "AGI {0}";
    [SerializeField] private string intelligenceFormat = "INT {0}";
    [SerializeField] private string enduranceFormat = "END {0}";

    [Header("Unspent Points Notice")]
    [SerializeField] private GameObject pointsNotice;
    [SerializeField] private TMP_Text pointsNoticeText;
    [SerializeField] private string noticeFormat = "{0} to spend!";
    [SerializeField] private string pointsCountFormat = "{0} point{1}";
    [SerializeField] private Color pointsCountColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private bool showNoticeInMinigames;

    [Header("Notice Bounce")]
    [SerializeField] private RectTransform bounceTarget;
    [SerializeField] private float bounceHeight = 8f;
    [SerializeField] [Min(0.05f)] private float bounceDuration = 0.35f;
    [SerializeField] [Range(0, 5)] private int settleBounces = 2;
    [SerializeField] [Range(0.05f, 0.9f)] private float settleRatio = 0.3f;
    [SerializeField] [Min(0f)] private float restDuration = 1.2f;
    [SerializeField] private bool snapToPixels = true;

    [Header("World Only")]
    [SerializeField] private GameObject[] worldOnlyObjects;

    [Header("Hide While UI Open")]
    [SerializeField] private bool hideWhileUIOpen = true;
    [SerializeField] private bool hideDuringDialogue = true;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] [Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float hiddenAlpha = 0f;

    private readonly int[] _shown = { -1, -1, -1, -1, -1 };
    private float _visibleAlpha = 1f;
    private Vector2 _bounceBase;
    private bool _hasBounceBase;
    private float _bounceStart;

    private void Awake()
    {
        if (fadeGroup == null)
            fadeGroup = GetComponent<CanvasGroup>();
        if (fadeGroup == null)
            fadeGroup = gameObject.AddComponent<CanvasGroup>();
        _visibleAlpha = fadeGroup.alpha;
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;

        if (pointsNotice != null)
        {
            if (bounceTarget == null)
                bounceTarget = pointsNotice.transform as RectTransform;

            var noticeGroup = pointsNotice.GetComponent<CanvasGroup>();
            if (noticeGroup != null)
                noticeGroup.alpha = 1f;

            pointsNotice.SetActive(false);
        }

        if (bounceTarget != null)
        {
            _bounceBase = bounceTarget.anchoredPosition;
            _hasBounceBase = true;
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < _shown.Length; i++)
            _shown[i] = -1;
    }

    private void OnDisable() => ResetBounce();

    private void Update()
    {
        UpdateFade();
        UpdateWorldOnly();

        var controller = PlayerStatsController.Active;
        var data = controller != null ? controller.Stats : SavedStats();
        if (data == null) return;

        UpdateStat(0, controller, data, StatType.Strength, strengthText, strengthFormat);
        UpdateStat(1, controller, data, StatType.Agility, agilityText, agilityFormat);
        UpdateStat(2, controller, data, StatType.Intelligence, intelligenceText, intelligenceFormat);
        UpdateStat(3, controller, data, StatType.Endurance, enduranceText, enduranceFormat);

        bool inMinigame = GameManager.Instance != null && GameManager.IsMinigameState(GameManager.Instance.CurrentState);
        UpdatePoints(inMinigame && !showNoticeInMinigames ? 0 : data.unallocatedPoints);
        Bounce();
    }

    private void UpdateFade()
    {
        if (fadeGroup == null) return;

        bool uiOpen = UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen;
        bool dialogueOpen = hideDuringDialogue && DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
        bool hidden = hideWhileUIOpen && (uiOpen || dialogueOpen);
        if (!ShouldShowForState())
        {
            fadeGroup.alpha = 0f;
            return;
        }

        float target = hidden ? hiddenAlpha : _visibleAlpha;

        fadeGroup.alpha = fadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(fadeGroup.alpha, target, Mathf.Max(_visibleAlpha, 0.01f) / fadeDuration * Time.unscaledDeltaTime);
    }

    private void UpdateWorldOnly()
    {
        if (worldOnlyObjects == null) return;

        bool inWorld = GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.World;
        foreach (var target in worldOnlyObjects)
        {
            if (target != null && target.activeSelf != inWorld)
                target.SetActive(inWorld);
        }
    }

    private static bool ShouldShowForState()
    {
        var game = GameManager.Instance;
        if (game == null) return true;

        var state = game.CurrentState;
        if (state == GameState.World) return true;
        if (!GameManager.IsMinigameState(state)) return false;

        var minigame = MinigameBase.Current;
        if (minigame == null) return false;

        var phase = minigame.CurrentPhase;
        return phase != MinigameBase.Phase.Idle && phase != MinigameBase.Phase.Intro;
    }

    private static PlayerStats SavedStats()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null ? save.stats : null;
    }

    private void UpdateStat(int slot, PlayerStatsController controller, PlayerStats data, StatType stat, TMP_Text text, string format)
    {
        if (text == null) return;

        int value = data.GetStat(stat);
        if (_shown[slot] == value) return;
        _shown[slot] = value;

        int max = controller != null ? controller.GetMaxStat(stat) : int.MaxValue;
        text.text = string.Format(format, value, max == int.MaxValue ? "-" : max.ToString());
    }

    private void UpdatePoints(int points)
    {
        if (_shown[4] == points) return;

        bool wasShowing = _shown[4] > 0;
        _shown[4] = points;

        if (pointsNotice == null) return;

        pointsNotice.SetActive(points > 0);

        if (points > 0 && !wasShowing)
            _bounceStart = Time.unscaledTime;
        if (points <= 0)
            ResetBounce();

        if (points > 0 && pointsNoticeText != null)
        {
            string count = string.Format(pointsCountFormat, points, points == 1 ? "" : "s");
            string coloured = $"<color=#{ColorUtility.ToHtmlStringRGBA(pointsCountColor)}>{count}</color>";
            pointsNoticeText.text = string.Format(noticeFormat, coloured);
        }
    }

    private void Bounce()
    {
        if (bounceTarget == null || !_hasBounceBase || pointsNotice == null || !pointsNotice.activeSelf) return;

        float offset = BounceOffset(Time.unscaledTime - _bounceStart) * bounceHeight;
        if (snapToPixels)
            offset = Mathf.Round(offset);

        bounceTarget.anchoredPosition = _bounceBase + new Vector2(0f, offset);
    }

    private float BounceOffset(float time)
    {
        float cycle = restDuration;
        float height = 1f;
        for (int i = 0; i <= settleBounces; i++)
        {
            cycle += bounceDuration * Mathf.Sqrt(height);
            height *= settleRatio;
        }

        float t = Mathf.Repeat(time, cycle);
        height = 1f;
        for (int i = 0; i <= settleBounces; i++)
        {
            float duration = bounceDuration * Mathf.Sqrt(height);
            if (t < duration)
            {
                float p = t / duration;
                return height * 4f * p * (1f - p);
            }
            t -= duration;
            height *= settleRatio;
        }

        return 0f;
    }

    private void ResetBounce()
    {
        if (bounceTarget != null && _hasBounceBase)
            bounceTarget.anchoredPosition = _bounceBase;
    }
}