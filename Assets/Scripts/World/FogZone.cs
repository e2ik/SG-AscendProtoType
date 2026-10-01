using System.Collections;
using UnityEngine;

public class FogZone : MonoBehaviour
{
    [SerializeField] private string clearedByFlag = "tier1_west_reached";
    [SerializeField] private float fadeDuration = 2.5f;
    [SerializeField] private SpriteRenderer[] renderers;

    [Header("Drift")]
    [SerializeField] private float driftDistance = 0.4f;
    [SerializeField] private float driftCyclesPerSecond = 0.1f;

    private float[] _baseAlphas;
    private Vector3 _origin;
    private bool _cleared;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        _baseAlphas = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            _baseAlphas[i] = renderers[i].color.a;

        _origin = transform.localPosition;
    }

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;

        if (!_cleared && IsFlagSet())
            ClearInstantly();
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;
    }

    private void Update()
    {
        if (_cleared || driftDistance <= 0f) return;

        float offset = Mathf.Sin(Time.time * driftCyclesPerSecond * Mathf.PI * 2f) * driftDistance;
        transform.localPosition = _origin + Vector3.right * offset;
    }

    private bool IsFlagSet()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && !string.IsNullOrEmpty(clearedByFlag) && save.GetFlag(clearedByFlag);
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (_cleared || !value || key != clearedByFlag) return;

        _cleared = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            SetAlpha(1f - t / fadeDuration);
            yield return null;
        }

        ClearInstantly();
    }

    private void ClearInstantly()
    {
        _cleared = true;
        SetAlpha(0f);
        foreach (var r in renderers)
            r.enabled = false;
    }

    private void SetAlpha(float multiplier)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var color = renderers[i].color;
            color.a = _baseAlphas[i] * multiplier;
            renderers[i].color = color;
        }
    }
}