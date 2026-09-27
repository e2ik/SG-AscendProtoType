using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum VolumeChannel
{
    Master,
    BGM,
    SFX,
    UI
}

public class VolumeSettings : MonoBehaviour
{
    public static VolumeSettings Instance { get; private set; }

    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParameter = "MasterVolume";
    [SerializeField] private string bgmParameter = "BGMVolume";
    [SerializeField] private string sfxParameter = "SFXVolume";
    [SerializeField] private string uiParameter = "UIVolume";
    [SerializeField] [Range(0f, 1f)] private float defaultVolume = 0.8f;

    private const string PrefsPrefix = "Volume_";
    private const float MinDecibels = -80f;

    private readonly Dictionary<VolumeChannel, float> _values = new Dictionary<VolumeChannel, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (VolumeChannel channel in System.Enum.GetValues(typeof(VolumeChannel)))
            _values[channel] = PlayerPrefs.GetFloat(PrefsPrefix + channel, defaultVolume);
    }

    private void Start()
    {
        foreach (var pair in _values)
            Apply(pair.Key, pair.Value);
    }

    public float Get(VolumeChannel channel) => _values.TryGetValue(channel, out float value) ? value : defaultVolume;

    public void Set(VolumeChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        _values[channel] = value;
        PlayerPrefs.SetFloat(PrefsPrefix + channel, value);
        Apply(channel, value);
    }

    public void Save() => PlayerPrefs.Save();

    private void Apply(VolumeChannel channel, float value)
    {
        if (mixer == null) return;

        string parameter = ParameterFor(channel);
        if (!mixer.SetFloat(parameter, ToDecibels(value)))
            Debug.LogWarning($"Audio Mixer has no exposed parameter named '{parameter}'", this);
    }

    private string ParameterFor(VolumeChannel channel) => channel switch
    {
        VolumeChannel.Master => masterParameter,
        VolumeChannel.BGM => bgmParameter,
        VolumeChannel.SFX => sfxParameter,
        _ => uiParameter
    };

    private static float ToDecibels(float value) =>
        value <= 0.0001f ? MinDecibels : Mathf.Log10(value) * 20f;
}