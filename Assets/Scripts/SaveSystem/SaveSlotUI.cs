using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text saveNameText;
    [SerializeField] private TMP_Text lastPlayedText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button deleteButton;

    public void Setup(SaveData save, Action<SaveData> onDeleteRequested)
    {
        saveNameText.text = save.saveName;
        lastPlayedText.text = DateTime.TryParse(save.lastPlayedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime().ToString("g")
            : string.Empty;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => GameManager.Instance.LoadGame(save.saveId));

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDeleteRequested?.Invoke(save));
        }
    }
}