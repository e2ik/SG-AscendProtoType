using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuController : MonoBehaviour
{
    public static OptionsMenuController Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private UIPanel uiPanel;
    [SerializeField] private Button backButton;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider uiSlider;

    [Header("Feedback")]
    [SerializeField] private string previewSound = "UIclick";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panel.SetActive(false);

        Bind(masterSlider, VolumeChannel.Master);
        Bind(bgmSlider, VolumeChannel.BGM);
        Bind(sfxSlider, VolumeChannel.SFX);
        Bind(uiSlider, VolumeChannel.UI);

        if (backButton != null)
            backButton.onClick.AddListener(Close);

        if (uiPanel != null)
            uiPanel.OnBack += SaveSettings;
    }

    public void Open()
    {
        Refresh(masterSlider, VolumeChannel.Master);
        Refresh(bgmSlider, VolumeChannel.BGM);
        Refresh(sfxSlider, VolumeChannel.SFX);
        Refresh(uiSlider, VolumeChannel.UI);

        UIPanelAnimator.SetVisible(panel, true);
    }

    public void Close()
    {
        SaveSettings();
        UIPanelAnimator.SetVisible(panel, false);
    }

    private void Bind(Slider slider, VolumeChannel channel)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(value => OnSliderChanged(channel, value));
    }

    private static void Refresh(Slider slider, VolumeChannel channel)
    {
        if (slider == null || VolumeSettings.Instance == null) return;
        slider.SetValueWithoutNotify(VolumeSettings.Instance.Get(channel));
    }

    private void OnSliderChanged(VolumeChannel channel, float value)
    {
        if (VolumeSettings.Instance != null)
            VolumeSettings.Instance.Set(channel, value);

        if (!string.IsNullOrEmpty(previewSound) && ASpawner.Instance != null)
            ASpawner.Play(previewSound);
    }

    private static void SaveSettings()
    {
        if (VolumeSettings.Instance != null)
            VolumeSettings.Instance.Save();
    }
}