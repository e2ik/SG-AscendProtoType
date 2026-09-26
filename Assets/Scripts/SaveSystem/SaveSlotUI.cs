using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text saveNameText;
    [SerializeField] private TMP_Text lastPlayedText;
    [SerializeField] private Button selectButton;

    private string _saveId;

    public void Setup(SaveData save)
    {
        _saveId = save.saveId;
        saveNameText.text = save.saveName;

        if (System.DateTime.TryParse(save.lastPlayedUtc, out var parsed))
            lastPlayedText.text = parsed.ToLocalTime().ToString("g");

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => GameManager.Instance.LoadGame(_saveId));
    }
}