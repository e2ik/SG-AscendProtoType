using UnityEngine;

public class FlagVisibility : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string showWhenFlag;
    [SerializeField] private string hideWhenFlag;

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
        Refresh();
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (key == showWhenFlag || key == hideWhenFlag)
            Refresh();
    }

    private void Refresh()
    {
        if (target == null) return;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        bool shown = string.IsNullOrEmpty(showWhenFlag) || (save != null && save.GetFlag(showWhenFlag));
        bool hidden = !string.IsNullOrEmpty(hideWhenFlag) && save != null && save.GetFlag(hideWhenFlag);

        target.SetActive(shown && !hidden);
    }
}