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
    [SerializeField] private string pointsFormat = "{0} point{1} to spend!";
    [SerializeField] private float flashSpeed = 3f;
    [SerializeField] [Range(0f, 1f)] private float flashMinAlpha = 0.25f;

    private readonly int[] _shown = { -1, -1, -1, -1, -1 };
    private CanvasGroup _noticeGroup;

    private void Awake()
    {
        if (pointsNotice != null)
        {
            _noticeGroup = pointsNotice.GetComponent<CanvasGroup>();
            pointsNotice.SetActive(false);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < _shown.Length; i++)
            _shown[i] = -1;
    }

    private void Update()
    {
        var stats = PlayerStatsController.Active;
        if (stats == null) return;

        UpdateStat(0, stats, StatType.Strength, strengthText, strengthFormat);
        UpdateStat(1, stats, StatType.Agility, agilityText, agilityFormat);
        UpdateStat(2, stats, StatType.Intelligence, intelligenceText, intelligenceFormat);
        UpdateStat(3, stats, StatType.Endurance, enduranceText, enduranceFormat);

        UpdatePoints(stats.Stats.unallocatedPoints);
        Flash();
    }

    private void UpdateStat(int slot, PlayerStatsController stats, StatType stat, TMP_Text text, string format)
    {
        if (text == null) return;

        int value = stats.Get(stat);
        if (_shown[slot] == value) return;
        _shown[slot] = value;

        int max = stats.GetMaxStat(stat);
        text.text = string.Format(format, value, max == int.MaxValue ? "-" : max.ToString());
    }

    private void UpdatePoints(int points)
    {
        if (_shown[4] == points) return;
        _shown[4] = points;

        if (pointsNotice == null) return;

        pointsNotice.SetActive(points > 0);
        if (points > 0 && pointsNoticeText != null)
            pointsNoticeText.text = string.Format(pointsFormat, points, points == 1 ? "" : "s");
    }

    private void Flash()
    {
        if (pointsNotice == null || !pointsNotice.activeSelf) return;

        float pulse = (Mathf.Sin(Time.unscaledTime * flashSpeed * Mathf.PI) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(flashMinAlpha, 1f, pulse);

        if (_noticeGroup != null)
        {
            _noticeGroup.alpha = alpha;
        }
        else if (pointsNoticeText != null)
        {
            var color = pointsNoticeText.color;
            color.a = alpha;
            pointsNoticeText.color = color;
        }
    }
}